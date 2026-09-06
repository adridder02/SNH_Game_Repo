using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// =============================================================
// AbilityHotbarSystem.cs
// -------------------------------------------------------------
// Attach next to AbilityPlacementSystem (same Player/Systems object).
// Holds a fixed set of 5 hotbar slots the player can drag an item into
// from the main inventory screen, and listens for the number keys 1-5
// to activate whichever item sits in that slot (slot 0 = '1', slot 1 =
// '2', ... slot 4 = '5').
//
// SCOPE: Anything EXCEPT a pot-targeted Consumable can go on the
// hotbar — that covers every Placeable (Spark Leaf, Waterbell, ...)
// as well as untargeted Consumables (Bubble of Holding, Glowcap
// Spore). Activating a Placeable slot starts placement mode, exactly
// like pressing its Use button in the inventory would. Pot-targeted
// Consumables (Pollen Puff, Verdant Algae, Dew Drop) still only make
// sense from inside a specific pot's own menu, since they need that
// pot as their target — CanAssign()/TryAssign() below are what
// enforce that one exclusion, so the inventory-screen drag code
// doesn't need to duplicate it — it just calls TryAssign() and checks
// the bool it gets back (e.g. to snap the dragged icon back if
// rejected).
//
// This does NOT own the item counts — a hotbar slot is just a saved
// reference to an AbilityItemData. If the player runs out of that
// item, the slot stays assigned (so restocking brings it right back)
// but GetCount() reads 0 and pressing the key silently does nothing.
// Drive your hotbar UI icon's greyed-out state off GetCount() too.
// =============================================================
public class AbilityHotbarSystem : MonoBehaviour
{
    [System.Serializable]
    public class HotbarSlot
    {
        public AbilityItemData assigned;
    }

    [Header("References")]
    [Tooltip("Auto-found on this GameObject if left empty.")]
    [SerializeField] private PlayerAbilityInventory abilityInventory;
    [Tooltip("Auto-found in the scene if left empty.")]
    [SerializeField] private AbilityPlacementSystem abilityPlacementSystem;
    [Tooltip("Passed into AbilityConsumableEffects.TryApply for player-targeted consumables. Defaults to this GameObject.")]
    [SerializeField] private GameObject player;

    [Header("Slots")]
    [Tooltip("Fixed at 5 — one per number key (slot 0 = '1', slot 1 = '2', slot 2 = '3', slot 3 = '4', slot 4 = '5').")]
    [SerializeField] private HotbarSlot[] slots = new HotbarSlot[5];

    /// <summary>Fired whenever a slot's assignment changes, so the hotbar UI can redraw.</summary>
    public event System.Action OnSlotsChanged;

    public int SlotCount => slots.Length;

    private static readonly Key[] NumberKeys =
    {
        Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5
    };

    // Tracks the last Time.time a given item was successfully consumed via the hotbar, keyed by
    // the item ASSET rather than slot index — so the cooldown follows the item even if it gets
    // reassigned to a different slot, and applies equally whether activation came from a number
    // key or a mouse click on the HUD slot (both funnel through ActivateSlot below). A single key
    // press is naturally rate-limited by wasPressedThisFrame, but mouse clicks aren't, which is
    // what let a stacked Consumable get chain-spammed with nothing but stock count as a limit.
    private readonly Dictionary<AbilityItemData, float> _lastUsedTime = new Dictionary<AbilityItemData, float>();

    private void Awake()
    {
        if (abilityInventory == null)
            abilityInventory = GetComponent<PlayerAbilityInventory>();
        if (abilityPlacementSystem == null)
            abilityPlacementSystem = FindObjectOfType<AbilityPlacementSystem>();
        if (player == null)
            player = gameObject;

        if (abilityInventory == null)
            Debug.LogWarning("[AbilityHotbarSystem] No PlayerAbilityInventory found — hotbar can't check stack counts.", this);
    }

    private void OnEnable()
    {
        if (abilityInventory != null)
            abilityInventory.OnChanged += AutoClearDepletedSlots;
        if (abilityPlacementSystem != null)
            abilityPlacementSystem.OnPlacingChanged += HandlePlacingChanged;
    }

    private void OnDisable()
    {
        if (abilityInventory != null)
            abilityInventory.OnChanged -= AutoClearDepletedSlots;
        if (abilityPlacementSystem != null)
            abilityPlacementSystem.OnPlacingChanged -= HandlePlacingChanged;
    }

    /// <summary>Re-broadcasts AbilityPlacementSystem starting/stopping/switching what it's placing as
    /// this system's own OnSlotsChanged, so the hotbar UI redraws and highlights whichever slot (if
    /// any) is now the active placement — see HotbarSlotUI.Refresh / IsSlotActive below.</summary>
    private void HandlePlacingChanged() => OnSlotsChanged?.Invoke();

    /// <summary>Whenever any stack's count changes, forget the assignment of any slot whose item has
    /// hit 0 — "used up" means gone from the hotbar too, not just dimmed. Covers being depleted from
    /// ANY source (hotbar activation, the inventory's Use button, a pot's Abilities panel), since
    /// they all funnel through PlayerAbilityInventory and fire this same event.</summary>
    private void AutoClearDepletedSlots()
    {
        if (abilityInventory == null) return;
        bool changed = false;

        for (int i = 0; i < slots.Length; i++)
        {
            AbilityItemData data = slots[i].assigned;
            if (data == null) continue;
            if (abilityInventory.GetCount(data) <= 0)
            {
                slots[i].assigned = null;
                changed = true;
            }
        }

        if (changed) OnSlotsChanged?.Invoke();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        // Only fire from the main gameplay HUD — while a menu (Inventory, pot menu, Journal, ...)
        // is open, GameInputModeManager is in MenuUI/Placement mode and those number keys shouldn't
        // double as hotbar shortcuts underneath whatever panel is on top. GameInputModeManager.Instance
        // can be null in scenes that don't use it (e.g. isolated test scenes) — don't block on that.
        if (GameInputModeManager.Instance != null &&
            GameInputModeManager.Instance.CurrentMode != GameInputModeManager.InputMode.Gameplay)
            return;

        for (int i = 0; i < slots.Length && i < NumberKeys.Length; i++)
        {
            if (Keyboard.current[NumberKeys[i]].wasPressedThisFrame)
            {
                ActivateSlot(i);
                break; // only one slot activates per frame
            }
        }
    }

    // ---------------------------------------------------------------
    // ASSIGNMENT — called by the inventory screen's drag-and-drop code.
    // ---------------------------------------------------------------

    /// <summary>Whether this item is even allowed on the hotbar. Everything qualifies EXCEPT a
    /// Consumable that needs a pot target (Pollen Puff, Verdant Algae, Dew Drop) — those only make
    /// sense from inside a specific pot's own menu, since they need that pot as their target.
    /// RequiresPotTarget only ever returns true for a handful of Consumable effect ids, so a
    /// Placeable's effectId trivially passes this check too — no separate branch needed for kind.
    /// OneOff items never sit in the inventory at all, so they'd have nothing to reference here,
    /// but excluding them explicitly costs nothing and avoids relying on that indirectly.</summary>
    public static bool CanAssign(AbilityItemData data)
    {
        if (data == null) return false;
        if (data.kind == AbilityKind.OneOff) return false;
        if (data.requiresTreeTarget) return false;
        return !AbilityConsumableEffects.RequiresPotTarget(data.effectId);
    }

    /// <summary>Assigns an item to a slot. Returns false (and does nothing) if the slot index is
    /// out of range or the item isn't hotbar-eligible — the caller (drag-drop UI) should treat a
    /// false return as "snap the icon back to the inventory panel".</summary>
    public bool TryAssign(int slotIndex, AbilityItemData data)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return false;
        if (!CanAssign(data)) return false;

        slots[slotIndex].assigned = data;
        OnSlotsChanged?.Invoke();
        return true;
    }

    public void Clear(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;
        slots[slotIndex].assigned = null;
        OnSlotsChanged?.Invoke();
    }

    public AbilityItemData GetAssigned(int slotIndex) =>
        (slotIndex >= 0 && slotIndex < slots.Length) ? slots[slotIndex].assigned : null;

    /// <summary>Whether this slot's item is the one currently being actively placed (Mode.Placing on
    /// AbilityPlacementSystem) — used by HotbarSlotUI to show a "this one's selected" highlight.
    /// Always false for a Consumable, since using one is instant with nothing left to stay "active".</summary>
    public bool IsSlotActive(int slotIndex)
    {
        AbilityItemData data = GetAssigned(slotIndex);
        return data != null && abilityPlacementSystem != null && abilityPlacementSystem.CurrentlyPlacing == data;
    }

    /// <summary>How many of the assigned item the player currently has — hotbar UI should use this
    /// to grey out a slot at 0 without clearing the assignment.</summary>
    public int GetAssignedCount(int slotIndex)
    {
        AbilityItemData data = GetAssigned(slotIndex);
        if (data == null || abilityInventory == null) return 0;
        return abilityInventory.GetCount(data);
    }

    // ---------------------------------------------------------------
    // ACTIVATION
    // ---------------------------------------------------------------
    /// <summary>Activates a slot directly — used by number-key presses above, and also callable
    /// from hotbar UI slot clicks so mouse-only play works the same way.</summary>
    public void ActivateSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;

        AbilityItemData data = slots[slotIndex].assigned;
        if (data == null) return;

        if (abilityInventory == null || abilityInventory.GetCount(data) <= 0)
            return; // stack ran out — slot stays assigned but does nothing until restocked

        if (data.kind == AbilityKind.Placeable)
        {
            if (abilityPlacementSystem == null)
            {
                Debug.LogWarning($"[AbilityHotbarSystem] No AbilityPlacementSystem — can't place '{data.displayName}'.");
                return;
            }

            // Pressing the SAME slot again while it's already the active placement DESELECTS it
            // instead of restarting placement — this is the "press 1-5 to cancel" half of the
            // deselect feature (right-click in the world does the other half, already handled by
            // AbilityPlacementSystem.Update).
            if (abilityPlacementSystem.CurrentlyPlacing == data)
            {
                abilityPlacementSystem.Cancel();
                return;
            }

            abilityPlacementSystem.BeginPlacing(data);
            return;
        }

        // Defensive re-check — CanAssign already keeps pot-targeted Consumables off the hotbar in
        // the first place, but this protects against a future data/design change slipping one in.
        if (data.kind == AbilityKind.Consumable && AbilityConsumableEffects.RequiresPotTarget(data.effectId))
        {
            Debug.LogWarning($"[AbilityHotbarSystem] '{data.displayName}' needs a pot target and " +
                              "shouldn't be assigned to the hotbar — use it from a pot's Abilities panel instead.");
            return;
        }

        // Cooldown gate — stops chain-spamming a stacked Consumable via rapid clicking/mashing.
        // Silently ignored while on cooldown (same "does nothing" treatment as an empty/depleted
        // slot above) rather than logging, since this is expected to happen constantly under
        // normal fast play, not something worth spamming the console over.
        if (data.useCooldown > 0f &&
            _lastUsedTime.TryGetValue(data, out float lastUsed) &&
            Time.time - lastUsed < data.useCooldown)
        {
            return;
        }

        bool applied = AbilityConsumableEffects.TryApply(data, player, null);
        if (applied)
        {
            abilityInventory.TryConsume(data, 1);
            _lastUsedTime[data] = Time.time;
        }
    }
}
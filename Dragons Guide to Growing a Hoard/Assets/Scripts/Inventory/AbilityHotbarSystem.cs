using UnityEngine;
using UnityEngine.InputSystem;

// =============================================================
// AbilityHotbarSystem.cs
// -------------------------------------------------------------
// Attach next to AbilityPlacementSystem (same Player/Systems object).
// Holds a fixed set of 5 hotbar slots the player can drag a Consumable
// item into from the main inventory screen, and listens for the number
// keys 1-5 to activate whichever item sits in that slot (slot 0 = '1',
// slot 1 = '2', ... slot 4 = '5').
//
// SCOPE: ONLY Consumable items can be assigned to a slot — Placeables
// are never hotbar-eligible (they're used straight from the Abilities
// panel's Use button, which drops the player into placement mode), and
// pot-targeted Consumables (Pollen Puff, Verdant Algae, Dewdrop) still
// only make sense from inside a specific pot's menu, since they need
// that pot as their target. CanAssign()/TryAssign() below enforce both
// rules, so the inventory-screen drag code doesn't need to duplicate
// them — it just calls TryAssign() and checks the bool it gets back
// (e.g. to snap the dragged icon back if rejected).
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

    /// <summary>Whether this item is even allowed on the hotbar. ONLY Consumables that don't need
    /// a pot target qualify — Placeables are never hotbar-eligible (use them from the Abilities
    /// panel's Use button instead, which starts placement mode directly), and OneOff items never
    /// sit in the inventory at all, so they'd have nothing to reference here.</summary>
    public static bool CanAssign(AbilityItemData data)
    {
        if (data == null) return false;
        if (data.kind != AbilityKind.Consumable) return false;
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

        // Only Consumables can ever be assigned (CanAssign enforces this), but a defensive
        // re-check here costs nothing and protects against a future data/design change.
        if (data.kind != AbilityKind.Consumable)
        {
            Debug.LogWarning($"[AbilityHotbarSystem] '{data.displayName}' isn't a Consumable and " +
                              "shouldn't be on the hotbar.");
            return;
        }

        if (AbilityConsumableEffects.RequiresPotTarget(data.effectId))
        {
            Debug.LogWarning($"[AbilityHotbarSystem] '{data.displayName}' needs a pot target and " +
                              "shouldn't be assigned to the hotbar — use it from a pot's Abilities panel instead.");
            return;
        }

        bool applied = AbilityConsumableEffects.TryApply(data, player, null);
        if (applied)
            abilityInventory.TryConsume(data, 1);
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// =============================================================
// HotbarSlotUI.cs
// -------------------------------------------------------------
// One hand-placed slot in a hotbar row. Purely a display + click
// target — the actual slot DATA lives in AbilityHotbarSystem (see
// AbilityHotbarSystem.cs), this just mirrors slot [SlotIndex]
// visually. The SAME component is used for both the row of slots
// tucked into the bottom of the Inventory panel (still wired up via
// InventoryUIController, for previewing assignments while browsing)
// and the persistent row on the main gameplay HUD (wired up via
// MainUIController) — hence IHotbarActivator below instead of a
// hard InventoryUIController reference, so either owner works.
//
// Assignment (drag a consumable straight from the main grid/Available)
// is resolved by InventoryUIController.TryHandleHotbarDrop, which
// rectangle-tests the drop point against each hotbar slot's own
// RectTransform — same approach InventoryUIController already uses
// for its grid/Available panels — rather than this component
// implementing IDropHandler itself, to keep drop resolution in one
// place. Only the Inventory panel's slots need to be reachable for
// dragging (the HUD row's slots typically aren't on-screen while the
// inventory book is open) — Refresh()/click still work identically on
// both rows regardless.
//
// Clicking a hotbar slot activates it directly (same effect as
// pressing its number key) — handy for mouse-only play or testing.
// RIGHT-clicking a slot instead CLEARS it (unassigns whatever's there,
// e.g. leftover items assigned while testing) — same left/right split
// as most hotbar UIs (Minecraft, etc.).
// =============================================================
[RequireComponent(typeof(RectTransform))]
public class HotbarSlotUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image icon;
    [Tooltip("Alpha applied when the assigned item's count has hit 0 (still assigned, just out of stock).")]
    [SerializeField] private float depletedAlpha = 0.35f;
    [Tooltip("Alpha applied when no item is assigned to this slot at all.")]
    [SerializeField] private float emptyAlpha = 0.15f;
    [Tooltip("Tint shown over this slot while it's the one actively selected (currently placing a " +
             "Placeable). Built automatically at runtime — no Editor setup needed.")]
    [SerializeField] private Color selectedTint = new Color(1f, 0.85f, 0.4f, 0.4f);

    /// <summary>Which AbilityHotbarSystem slot (0-based) this UI element mirrors. Set by the owning
    /// controller's Awake() from its hotbarSlotUIs list order — slot 0 = key '1', etc.</summary>
    public int SlotIndex { get; private set; }

    public RectTransform RectTransform => (RectTransform)transform;

    private IHotbarActivator controller;
    private Image selectedOverlay;

    public void Initialize(IHotbarActivator owningController, int slotIndex)
    {
        controller = owningController;
        SlotIndex = slotIndex;
    }

    public void Refresh(AbilityHotbarSystem hotbarSystem)
    {
        if (hotbarSystem == null || icon == null) return;

        AbilityItemData data = hotbarSystem.GetAssigned(SlotIndex);
        int count = hotbarSystem.GetAssignedCount(SlotIndex);

        icon.sprite = data != null ? data.icon : null;
        icon.enabled = data != null && data.icon != null;

        Color c = icon.color;
        // 100% opacity means "assigned AND you currently own at least one" — this is deliberate,
        // not a bug: depletedAlpha (assigned but count is 0) and emptyAlpha (nothing assigned) are
        // both meant to look visibly different from a slot that's actually ready to use. If a slot
        // looks dim after you've assigned something, it means the count is 0 — pick up/keep at
        // least one of that item and it'll snap to full opacity on the next Refresh().
        c.a = data == null ? emptyAlpha : (count > 0 ? 1f : depletedAlpha);
        icon.color = c;

        EnsureSelectedOverlay();
        selectedOverlay.color = hotbarSystem.IsSlotActive(SlotIndex) ? selectedTint : Color.clear;
    }

    /// <summary>Creates a full-cover tint overlay the first time this slot refreshes — same
    /// "build it at runtime, no prefab editing needed" approach as the grid's drag-highlight cells.</summary>
    private void EnsureSelectedOverlay()
    {
        if (selectedOverlay != null) return;

        GameObject go = new GameObject("SelectedOverlay", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        selectedOverlay = go.AddComponent<Image>();
        selectedOverlay.raycastTarget = false; // never intercept clicks meant for this slot
        selectedOverlay.color = Color.clear;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (controller == null) return;

        if (eventData.button == PointerEventData.InputButton.Right)
            controller.ClearHotbarSlot(SlotIndex);
        else
            controller.ActivateHotbarSlot(SlotIndex);
    }
}

/// <summary>Implemented by whichever controller owns a row of HotbarSlotUI — InventoryUIController
/// (the preview row inside the Inventory panel) and MainUIController (the persistent HUD row) both
/// implement this so HotbarSlotUI doesn't need to hard-reference either one specifically.</summary>
public interface IHotbarActivator
{
    void ActivateHotbarSlot(int slotIndex);

    /// <summary>Unassigns whatever's in this slot — called on right-click. Doesn't consume or
    /// affect the player's actual item count, just clears the hotbar reference.</summary>
    void ClearHotbarSlot(int slotIndex);
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// =============================================================
// HotbarSlotUI.cs
// -------------------------------------------------------------
// One hand-placed slot in the bottom hotbar row (the row of boxes in
// your mockup). Purely a display + click target — the actual slot
// DATA lives in AbilityHotbarSystem (see AbilityHotbarSystem.cs),
// this just mirrors slot [SlotIndex] visually.
//
// Assignment (drag from the Abilities panel) is resolved by
// InventoryUIController.HandleAbilityDrop, which rectangle-tests the
// drop point against each hotbar slot's own RectTransform — same
// approach InventoryUIController already uses for its grid/Available
// panels — rather than this component implementing IDropHandler
// itself, to keep drop resolution in one place.
//
// Clicking a hotbar slot activates it directly (same effect as
// pressing its number key) — handy for mouse-only play or testing.
// =============================================================
[RequireComponent(typeof(RectTransform))]
public class HotbarSlotUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image icon;
    [Tooltip("Alpha applied when the assigned item's count has hit 0 (still assigned, just out of stock).")]
    [SerializeField] private float depletedAlpha = 0.35f;
    [Tooltip("Alpha applied when no item is assigned to this slot at all.")]
    [SerializeField] private float emptyAlpha = 0.15f;

    /// <summary>Which AbilityHotbarSystem slot (0-based) this UI element mirrors. Set by
    /// InventoryUIController.Awake() from the hotbarSlotUIs list order — slot 0 = key '1', etc.</summary>
    public int SlotIndex { get; private set; }

    public RectTransform RectTransform => (RectTransform)transform;

    private InventoryUIController controller;

    public void Initialize(InventoryUIController owningController, int slotIndex)
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
        c.a = data == null ? emptyAlpha : (count > 0 ? 1f : depletedAlpha);
        icon.color = c;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        controller?.ActivateHotbarSlot(SlotIndex);
    }
}

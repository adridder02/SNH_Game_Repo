using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// =============================================================
// AbilityInventorySlotUI.cs
// -------------------------------------------------------------
// One entry in the main inventory's Abilities section — mirrors
// InventorySlotUI's shape and drag mechanics exactly (same
// clone-a-template approach, same reparent-to-canvas drag), just for
// AbilityItemInstance stacks instead of plants, plus a stack-count
// label since these are stackable.
//
// Click = "Use" for Placeables and untargeted Consumables, routed
// through InventoryUIController.UseAbility(). Pot-targeted
// Consumables (Pollen Puff, Verdant Algae, Dewdrop — see
// AbilityConsumableEffects.RequiresPotTarget) can't be used from
// here at all: they need a specific pot as their target, so they
// only ever work from inside that pot's own Abilities panel
// (PotMenuUIController). UseAbility() no-ops with a log message if
// one somehow ends up in this list.
//
// Drag = drop onto a hotbar slot to assign it there, resolved by
// InventoryUIController.HandleAbilityDrop (same call shape as
// InventorySlotUI.HandleDrop). Unlike planting a plant, dragging an
// ability item never actually removes/moves it from this panel — it
// only assigns a reference over in AbilityHotbarSystem — so this
// slot always snaps back to its original position after a drag,
// regardless of whether the drop was handled.
// =============================================================
[RequireComponent(typeof(RectTransform))]
public class AbilityInventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("Wire these up on the prefab")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI countLabel;

    public AbilityItemInstance Stack { get; private set; }

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas rootCanvas;
    private InventoryUIController controller;

    private Transform originalParent;
    private Vector2 originalAnchoredPosition;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Initialize(AbilityItemInstance stack, InventoryUIController owningController, Canvas canvas)
    {
        Stack = stack;
        controller = owningController;
        rootCanvas = canvas;

        if (icon != null)
        {
            icon.sprite = stack.data.icon;
            icon.enabled = stack.data.icon != null;
            icon.preserveAspect = true;
        }

        if (countLabel != null)
            countLabel.text = $"x{stack.count}";
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Stack?.data == null) return;

        originalParent = transform.parent;
        originalAnchoredPosition = rectTransform.anchoredPosition;

        transform.SetParent(rootCanvas.transform, true);
        transform.SetAsLastSibling();

        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.85f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        controller?.HandleAbilityDrop(this, eventData);

        // Always snap back — an ability slot never actually leaves the Abilities panel from a
        // drag, it only assigns a hotbar reference, so there's no "handled == stays elsewhere"
        // case the way InventorySlotUI has for planting.
        transform.SetParent(originalParent, false);
        rectTransform.anchoredPosition = originalAnchoredPosition;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Stack?.data == null || controller == null) return;
        controller.UseAbility(Stack.data);
    }
}

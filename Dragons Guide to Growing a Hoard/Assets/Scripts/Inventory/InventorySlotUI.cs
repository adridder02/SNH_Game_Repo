using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// =============================================================
// InventorySlotUI.cs
// -------------------------------------------------------------
// One entry in the main inventory's SHARED grid/Available area.
// A slot can hold EITHER a plant (InventoryItemInstance) or a
// Consumable/Placeable stack (AbilityItemInstance) — see Occupant
// below — since both now live in the same grid.
//
// CLICK: opens the shared detail panel (PlantPanel) — ShowPlantDetail
// for a plant, ShowAbilityDetail for a stack. The panel itself decides
// whether to show a Use button (plants never get one; only an
// untargeted Consumable does — see InventoryUIController).
//
// DRAG: rearranges the item within the grid/Available, same as
// before. Consumables get one extra option: dropping on a hotbar
// slot ASSIGNS it there instead of moving it — see OnEndDrag below.
// Placeables and pot-targeted Consumables (Verdant Algae, Pollen
// Puff, Dewdrop) simply aren't hotbar-eligible, so dropping one on a
// hotbar slot is treated as a miss and the item stays wherever it was
// dropped in the grid/Available instead (see AbilityHotbarSystem.CanAssign).
// =============================================================
[RequireComponent(typeof(RectTransform))]
public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("Wire these up on the prefab")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI label;
    [Tooltip("Optional. Shown as 'xN' for a consumable/placeable stack, hidden for plants (plants " +
             "are always just one). Leave unassigned if your slot prefab doesn't have one — everything " +
             "still works, you just won't see a stack count on the icon.")]
    [SerializeField] private TextMeshProUGUI countLabel;

    /// <summary>Whichever item this slot currently displays — an InventoryItemInstance (plant) or an
    /// AbilityItemInstance (consumable/placeable stack). Check the type to know which.</summary>
    public IGridPlaceable Occupant { get; private set; }

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

    public void Initialize(IGridPlaceable occupant, InventoryUIController owningController, Canvas canvas)
    {
        Occupant = occupant;
        controller = owningController;
        rootCanvas = canvas;

        if (occupant is InventoryItemInstance plant)
        {
            Sprite sprite = plant.icon != null ? plant.icon : GetIcon(plant.plantPrefab);
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;

                RectTransform iconRect = icon.rectTransform;
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
                icon.preserveAspect = true;
            }
            if (label != null)
                label.text = !string.IsNullOrEmpty(plant.displayName)
                    ? plant.displayName
                    : GetDisplayName(plant.plantPrefab); // fallback for items with no display name set

            // Plants are never stackable — hide the count label if the prefab has one.
            if (countLabel != null)
                countLabel.gameObject.SetActive(false);
        }
        else if (occupant is AbilityItemInstance stack)
        {
            if (icon != null)
            {
                icon.sprite = stack.data != null ? stack.data.icon : null;
                icon.enabled = stack.data != null && stack.data.icon != null;

                RectTransform iconRect = icon.rectTransform;
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
                icon.preserveAspect = true;
            }
            if (label != null)
                label.text = stack.data != null ? stack.data.displayName : "Unknown";

            if (countLabel != null)
            {
                countLabel.gameObject.SetActive(true);
                countLabel.text = $"x{stack.count}";
            }
        }
    }

    private static Sprite GetIcon(GameObject prefab)
    {
        if (prefab == null) return null;
        SpriteRenderer sr = prefab.GetComponentInChildren<SpriteRenderer>();
        return sr != null ? sr.sprite : null;
    }

    /// <summary>Fallback only — used if instance.displayName wasn't set (e.g. a plant with no
    /// PlantSpeciesData linked and no CollectablePlant fallback name). Derives something readable
    /// from the raw prefab's GameObject name.</summary>
    private static string GetDisplayName(GameObject prefab)
    {
        return prefab != null ? prefab.name.Replace("(Clone)", "").Trim() : "Unknown";
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Occupant == null) return;

        originalParent = transform.parent;
        originalAnchoredPosition = rectTransform.anchoredPosition;

        transform.SetParent(rootCanvas.transform, true);
        transform.SetAsLastSibling();

        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.85f;

        controller.BeginDragHighlight(Occupant);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Occupant == null) return;
        rectTransform.position = eventData.position;   // Best for root canvas dragging
        controller.UpdateDragHighlight(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (Occupant == null) return;

        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        controller.EndDragHighlight();

        // Consumables/placeables can be dropped onto a hotbar slot to ASSIGN it there — that
        // never actually moves the item's grid position, so treat a hotbar-area drop as fully
        // handled (snap back) and skip the normal grid/Available move check entirely. Plants,
        // and anything that isn't hotbar-eligible, simply never match here.
        if (controller.TryHandleHotbarDrop(this, eventData))
        {
            transform.SetParent(originalParent, false);
            rectTransform.anchoredPosition = originalAnchoredPosition;
            return;
        }

        bool handled = controller.HandleDrop(this, eventData);

        if (!handled)
        {
            transform.SetParent(originalParent, false);
            rectTransform.anchoredPosition = originalAnchoredPosition;
        }
    }

    /// <summary>
    /// A plain click (pointer down+up with no meaningful movement) opens the shared detail panel —
    /// ShowPlantDetail for a plant, ShowAbilityDetail for a consumable/placeable stack. This fires
    /// independently of the drag handlers above — Unity's EventSystem only invokes OnBeginDrag/
    /// OnDrag/OnEndDrag once the pointer moves past its drag threshold, so an actual drag never also
    /// triggers OnPointerClick, and a real click never triggers the drag events.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (Occupant == null || controller == null) return;

        if (Occupant is InventoryItemInstance plant)
            controller.ShowPlantDetail(plant);
        else if (Occupant is AbilityItemInstance stack)
            controller.ShowAbilityDetail(stack);
    }
}

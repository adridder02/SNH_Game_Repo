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

    [Header("Icon Sizing")]
    [Tooltip("Empty margin left around the icon, as a FRACTION of the slot's size on each side (0.15 " +
             "= a 15% margin on all four sides). This is a proportion, not a fixed pixel amount, so it " +
             "scales automatically with the slot itself — including the bigger multi-cell slots big-" +
             "footprint plants get — while still keeping the icon visibly smaller than the frame, " +
             "matching the Choose Plant panel's look (PotPlantOptionUI) instead of the old edge-to-edge " +
             "stretch. Tune this in the Editor with Play mode running to match Choose Plant by eye.")]
    [Range(0f, 0.45f)]
    [SerializeField] private float iconPaddingFraction = 0.15f;

    [Header("New Item Badge")]
    [Tooltip("Small 'new!' badge shown on this slot until the player clicks it (or clicks a " +
             "DIFFERENT slot holding the same item type — e.g. a second copy of the same still-new " +
             "plant species). Shown in both the main grid and Available — see GetNewItemTypeId " +
             "below for how a slot's type id is derived. Optional — leave unassigned if a given slot " +
             "prefab doesn't need one.")]
    [SerializeField] private GameObject newBadge;

    /// <summary>Whichever item this slot currently displays — an InventoryItemInstance (plant) or an
    /// AbilityItemInstance (consumable/placeable stack). Check the type to know which.</summary>
    public IGridPlaceable Occupant { get; private set; }

    /// <summary>True for a slot living in the Available panel rather than the main grid — clicking
    /// one of these should just do nothing (no detail panel), see OnPointerClick below. Available
    /// is overflow storage, not a "browse and inspect" area the way the main grid is.</summary>
    private bool isAvailableSlot;

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

    void OnEnable()
    {
        if (NewItemTracker.Instance != null)
            NewItemTracker.Instance.OnChanged += RefreshNewBadge;
        RefreshNewBadge();
    }

    void OnDisable()
    {
        if (NewItemTracker.Instance != null)
            NewItemTracker.Instance.OnChanged -= RefreshNewBadge;
    }

    public void Initialize(IGridPlaceable occupant, InventoryUIController owningController, Canvas canvas, bool isAvailableSlot = false)
    {
        Occupant = occupant;
        controller = owningController;
        rootCanvas = canvas;
        this.isAvailableSlot = isAvailableSlot;

        if (occupant is InventoryItemInstance plant)
        {
            Sprite sprite = plant.icon != null ? plant.icon : GetIcon(plant.plantPrefab);
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;

                SizeIconWithPadding();
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

                SizeIconWithPadding();
            }
            if (label != null)
                label.text = stack.data != null ? stack.data.displayName : "Unknown";

            if (countLabel != null)
            {
                countLabel.gameObject.SetActive(true);
                countLabel.text = $"x{stack.count}";
            }
        }

        RefreshNewBadge();
    }

    // Insets the icon's RectTransform by iconPaddingFraction on each side instead of stretching it
    // edge-to-edge — see the iconPaddingFraction tooltip above for why this needs to be a
    // proportion rather than either a fixed padding or leaving the RectTransform untouched.
    private void SizeIconWithPadding()
    {
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(iconPaddingFraction, iconPaddingFraction);
        iconRect.anchorMax = new Vector2(1f - iconPaddingFraction, 1f - iconPaddingFraction);
        iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
        icon.preserveAspect = true;
    }

    /// <summary>The id NewItemTracker uses for this slot's current Occupant — a plant's
    /// newItemTypeId (already computed to match PlantJournalManager's own species id), or an
    /// ability stack's underlying AbilityItemData asset name. Null if Occupant is empty/unknown.</summary>
    private string GetNewItemTypeId()
    {
        if (Occupant is InventoryItemInstance plant) return plant.newItemTypeId;
        if (Occupant is AbilityItemInstance stack) return stack.data != null ? stack.data.name : null;
        return null;
    }

    private void RefreshNewBadge()
    {
        if (newBadge == null) return;

        bool isNew = NewItemTracker.Instance != null &&
                     NewItemTracker.Instance.IsUnseenInInventory(GetNewItemTypeId());

        newBadge.SetActive(isNew);
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

        // Clears the "new" badge the moment this slot is clicked, whether it's in the main grid
        // or Available — both count as "the player has seen it" per how this was asked for, even
        // though Available doesn't open a detail panel below.
        NewItemTracker.Instance?.MarkSeenInInventory(GetNewItemTypeId());

        if (isAvailableSlot) return; // Available is overflow storage, not a browse/inspect area

        if (Occupant is InventoryItemInstance plant)
            controller.ShowPlantDetail(plant);
        else if (Occupant is AbilityItemInstance stack)
            controller.ShowAbilityDetail(stack);
    }
}
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("Wire these up on the prefab")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI label;

    public InventoryItemInstance Instance { get; private set; }

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

    public void Initialize(InventoryItemInstance instance, InventoryUIController owningController, Canvas canvas)
    {
        Instance = instance;
        controller = owningController;
        rootCanvas = canvas;

        Sprite sprite = instance.icon != null ? instance.icon : GetIcon(instance.plantPrefab);
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
            label.text = !string.IsNullOrEmpty(instance.displayName)
                ? instance.displayName
                : GetDisplayName(instance.plantPrefab); // fallback for items with no display name set
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
        originalParent = transform.parent;
        originalAnchoredPosition = rectTransform.anchoredPosition;

        transform.SetParent(rootCanvas.transform, true);
        transform.SetAsLastSibling();

        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.85f;

        controller.BeginDragHighlight(Instance);
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.position = eventData.position;   // Best for root canvas dragging
        controller.UpdateDragHighlight(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        controller.EndDragHighlight();

        bool handled = controller.HandleDrop(this, eventData);

        if (!handled)
        {
            transform.SetParent(originalParent, false);
            rectTransform.anchoredPosition = originalAnchoredPosition;
        }
    }

    /// <summary>
    /// A plain click (pointer down+up with no meaningful movement) opens the
    /// Plant detail panel. This fires independently of the drag handlers above —
    /// Unity's EventSystem only invokes OnBeginDrag/OnDrag/OnEndDrag once the
    /// pointer moves past its drag threshold, so an actual drag never also
    /// triggers OnPointerClick, and a real click never triggers the drag events.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (Instance == null || controller == null) return;
        controller.ShowPlantDetail(Instance);
    }
}
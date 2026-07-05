using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// =============================================================
// InventorySlotUI.cs
// -------------------------------------------------------------
// Lives on the slot PREFAB (uGUI, not UI Toolkit). Represents one
// InventoryItemInstance visually, whether it's parented under the
// grid panel or the Available panel, and handles the drag itself.
//
// PREFAB SETUP:
//   • Root: Image (RectTransform) — the slot background. Add a
//     CanvasGroup here too (or let Awake add one automatically).
//   • Child "Icon": Image — the plant sprite.
//   • Child "Label": TextMeshProUGUI — the plant name.
//   • Both Icon and Label should have Raycast Target UNCHECKED,
//     so drag events always hit the root slot, not its children.
// =============================================================
[RequireComponent(typeof(RectTransform))]
public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
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

        Sprite sprite = GetIcon(instance.plantPrefab);
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }
        if (label != null)
            label.text = GetDisplayName(instance.plantPrefab);
    }

    private static Sprite GetIcon(GameObject prefab)
    {
        if (prefab == null) return null;
        SpriteRenderer sr = prefab.GetComponent<SpriteRenderer>();
        return sr != null ? sr.sprite : null;
    }

    private static string GetDisplayName(GameObject prefab)
    {
        return prefab != null ? prefab.name.Replace("(Clone)", "").Trim() : "Unknown";
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        originalAnchoredPosition = rectTransform.anchoredPosition;

        // Reparent to the canvas root so the dragged icon renders above every panel.
        transform.SetParent(rootCanvas.transform, worldPositionStays: true);
        transform.SetAsLastSibling();

        canvasGroup.blocksRaycasts = false; // let drop-target rects receive the raycast underneath
        canvasGroup.alpha = 0.85f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.anchoredPosition += eventData.delta / rootCanvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        bool handled = controller.HandleDrop(this, eventData);

        if (!handled)
        {
            // Invalid drop (occupied cell, dropped outside both panels, etc.) — snap back.
            // On success, PlayerInventory.OnInventoryChanged triggers a full RefreshUI(),
            // which will destroy/rebuild this slot in its new, correct location anyway.
            transform.SetParent(originalParent, worldPositionStays: false);
            rectTransform.anchoredPosition = originalAnchoredPosition;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// =============================================================
// InventoryUIController.cs  (uGUI Canvas version)
// -------------------------------------------------------------
// Replaces UI_Inventory_Script.cs. Builds the grid + Available
// panel from PlayerInventory's data and re-renders whenever
// PlayerInventory.OnInventoryChanged fires. Drag-and-drop is
// handled per-slot by InventorySlotUI, which calls back into
// HandleDrop() here to resolve where the drop landed.
//
// SCENE / PREFAB SETUP:
//   1. This script MUST sit on a GameObject that stays ACTIVE at
//      all times (e.g. a persistent UI manager object, or the
//      Canvas root) — NOT on inventoryRoot itself. If the
//      GameObject holding this script starts inactive, Awake()/
//      OnEnable() never run and the Inventory key will silently
//      do nothing.
//   2. inventoryRoot = the GameObject that gets SetActive() on open/close
//      (your painted book panel from the mockup).
//   3. gridPanel = the RectTransform that holds grid items.
//        - Anchor + pivot = TOP-LEFT (0,1). This matters: the
//          coordinate math below assumes cell (0,0) sits at the
//          panel's top-left corner and Y grows downward.
//   4. availablePanel = the RectTransform holding overflow items.
//        - Anchor + pivot = TOP-LEFT (0,1), same as gridPanel. No
//          GridLayoutGroup or ContentSizeFitter needed — this script
//          positions every Available slot manually (wraps at
//          availableColumns), same approach as the grid panel, so
//          there's no fighting with Unity's layout system.
//   5. gridSlotTemplate / availableSlotTemplate = existing slot
//      GameObjects already in your hierarchy (e.g. 'Container',
//      'ContainerSmall') with an InventorySlotUI component added.
//      They're hidden at runtime and cloned for every item —
//      no separate prefab asset required.
// =============================================================
public class InventoryUIController : MonoBehaviour
{
    [Header("Inventory of Player")]
    [SerializeField] private PlayerInventory playerInventory;

    [Header("Canvas References")]
    [Tooltip("The panel GameObject that gets shown/hidden when toggling the inventory.")]
    [SerializeField] private GameObject inventoryRoot;
    [Tooltip("Container for grid items. Anchor/pivot must be top-left (0,1).")]
    [SerializeField] private RectTransform gridPanel;
    [Tooltip("Container for Available overflow items. Anchor/pivot must be top-left (0,1), " +
             "same as gridPanel — this script lays items out manually, no layout group needed.")]
    [SerializeField] private RectTransform availablePanel;
    [Tooltip("An existing slot GameObject already placed in your grid (e.g. 'Container' under SlotsContainer). " +
             "It gets hidden at startup and cloned for every item — no prefab asset needed.")]
    [SerializeField] private InventorySlotUI gridSlotTemplate;

    [Tooltip("An existing slot GameObject already placed in Available (e.g. 'ContainerSmall'). " +
             "Cloned the same way as gridSlotTemplate. Leave empty to reuse gridSlotTemplate for both panels.")]
    [SerializeField] private InventorySlotUI availableSlotTemplate;
    [Tooltip("Optional close/back button.")]
    [SerializeField] private Button backButton;

    [Header("Layout")]
    [Tooltip("Pixel size of one grid cell. Item visuals are drawn at footprint * cellSizePx.")]
    [SerializeField] private float cellSizePx = 90f;
    [Tooltip("Gap between cells, purely visual.")]
    [SerializeField] private float cellGapPx = 4f;
    [Tooltip("How many columns to wrap Available items at. No GridLayoutGroup needed — " +
             "this script positions Available slots manually, same as it does for the grid.")]
    [SerializeField] private int availableColumns = 4;

    private Canvas rootCanvas;
    private bool isInventoryOpen = false;
    private PlayerControls playerControls;
    private InputAction inventoryAction;
    private readonly Dictionary<string, InventorySlotUI> slotVisuals = new Dictionary<string, InventorySlotUI>();

    void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
            Debug.LogError("❌ InventoryUIController must be somewhere under a Canvas!");

        playerControls = new PlayerControls();

        // Subscribe directly to just this one action instead of calling
        // playerControls.GamePlay.SetCallbacks(this). SetCallbacks REPLACES
        // every callback on the whole map — if another script (e.g. player
        // movement) also calls SetCallbacks on GamePlay, whichever runs last
        // silently wins and the other script's bindings (including this one)
        // stop firing. Subscribing to the action directly avoids that entirely.
        inventoryAction = playerControls.GamePlay.Inventory;
        if (inventoryAction != null)
            inventoryAction.performed += OnInventoryPerformed;
        else
            Debug.LogError("❌ Inventory action NOT FOUND! Add it to the Input Action Asset (GamePlay map).");

        if (playerInventory == null)
            playerInventory = FindObjectOfType<PlayerInventory>();

        if (backButton != null)
            backButton.onClick.AddListener(ToggleInventory);

        // These are live scene objects being used as clone templates — hide the
        // originals so they don't show up as a phantom extra slot.
        if (gridSlotTemplate != null) gridSlotTemplate.gameObject.SetActive(false);
        if (availableSlotTemplate != null) availableSlotTemplate.gameObject.SetActive(false);

        SetInventoryVisible(false);

        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
    }

    void OnEnable()
    {
        playerControls?.Enable();
        if (playerInventory != null)
            playerInventory.OnInventoryChanged += RefreshUI;
    }

    void OnDisable()
    {
        playerControls?.Disable();
        if (playerInventory != null)
            playerInventory.OnInventoryChanged -= RefreshUI;
    }

    void OnDestroy()
    {
        if (inventoryAction != null)
            inventoryAction.performed -= OnInventoryPerformed;
        playerControls?.Dispose();
    }

    private void OnInventoryPerformed(InputAction.CallbackContext context)
    {
        ToggleInventory();
    }

    public void ToggleInventory()
    {
        isInventoryOpen = !isInventoryOpen;
        SetInventoryVisible(isInventoryOpen);

        if (isInventoryOpen)
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            GameInputModeManager.Instance?.SetUIMode();
            ThirdPersonCameraController.CameraLocked = true;
            RefreshUI();
        }
        else
        {
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            GameInputModeManager.Instance?.SetGameplayMode();
            ThirdPersonCameraController.CameraLocked = false;
        }
    }

    void SetInventoryVisible(bool visible)
    {
        if (inventoryRoot != null)
            inventoryRoot.SetActive(visible);
    }

    // ------------------------------------------------------------
    // RENDERING
    // ------------------------------------------------------------

    void RefreshUI()
    {
        if (playerInventory == null || gridPanel == null || availablePanel == null || gridSlotTemplate == null)
        {
            Debug.LogWarning("[InventoryUIController] Missing a required reference — check the Inspector.");
            return;
        }

        // GridPanel keeps whatever size you gave it in the Inspector (matched to
        // your background art). We derive cell size FROM the panel, not the other
        // way around — this used to forcibly shrink GridPanel to
        // (GridWidth * cellSizePx), which is why items were rendering as tiny
        // squares floating in the middle of a much bigger painted frame.
        float cellW = gridPanel.rect.width / Mathf.Max(1, playerInventory.GridWidth);
        float cellH = gridPanel.rect.height / Mathf.Max(1, playerInventory.GridHeight);

        foreach (var visual in slotVisuals.Values)
            if (visual != null) Destroy(visual.gameObject);
        slotVisuals.Clear();

        foreach (var instance in playerInventory.GetGridItems())
        {
            InventorySlotUI slot = CreateSlot(instance, gridPanel, gridSlotTemplate);
            RectTransform rt = slot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(
                instance.footprint.x * cellW - cellGapPx,
                instance.footprint.y * cellH - cellGapPx);
            rt.anchoredPosition = new Vector2(
                instance.gridX * cellW + cellGapPx * 0.5f,
                -(instance.gridY * cellH + cellGapPx * 0.5f));
        }

        InventorySlotUI availableTemplate = availableSlotTemplate != null ? availableSlotTemplate : gridSlotTemplate;
        var availableItems = playerInventory.GetAvailableItems();
        int columns = Mathf.Max(1, availableColumns);
        int rows = Mathf.CeilToInt(availableItems.Count / (float)columns);

        // Manual layout — no GridLayoutGroup/ContentSizeFitter involved, so there's
        // nothing fighting these values on the same frame. Mirrors the top-left,
        // row/column math used for gridPanel above.
        availablePanel.sizeDelta = new Vector2(columns * cellSizePx, Mathf.Max(1, rows) * cellSizePx);

        for (int i = 0; i < availableItems.Count; i++)
        {
            InventorySlotUI slot = CreateSlot(availableItems[i], availablePanel, availableTemplate);
            RectTransform rt = slot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(cellSizePx - cellGapPx, cellSizePx - cellGapPx);

            int col = i % columns;
            int row = i / columns;
            rt.anchoredPosition = new Vector2(
                col * cellSizePx + cellGapPx * 0.5f,
                -(row * cellSizePx + cellGapPx * 0.5f));
        }
    }

    InventorySlotUI CreateSlot(InventoryItemInstance instance, RectTransform parent, InventorySlotUI template)
    {
        InventorySlotUI slot = Instantiate(template, parent);
        slot.gameObject.SetActive(true); // template itself is hidden — the clone needs to be shown
        slot.Initialize(instance, this, rootCanvas);
        slotVisuals[instance.instanceId] = slot;
        return slot;
    }

    // ------------------------------------------------------------
    // DROP RESOLUTION — called by InventorySlotUI.OnEndDrag
    // ------------------------------------------------------------

    /// <summary>Returns true if the drop was handled (item moved), false if the slot should snap back.</summary>
    public bool HandleDrop(InventorySlotUI slot, PointerEventData eventData)
    {
        InventoryItemInstance instance = slot.Instance;

        if (RectTransformUtility.RectangleContainsScreenPoint(gridPanel, eventData.position, eventData.pressEventCamera))
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                gridPanel, eventData.position, eventData.pressEventCamera, out Vector2 local);

            float cellW = gridPanel.rect.width / Mathf.Max(1, playerInventory.GridWidth);
            float cellH = gridPanel.rect.height / Mathf.Max(1, playerInventory.GridHeight);

            int cellX = Mathf.FloorToInt(local.x / cellW);
            int cellY = Mathf.FloorToInt(-local.y / cellH); // gridPanel pivot is top-left, so local.y <= 0 going down

            return playerInventory.TryPlaceInGrid(instance, cellX, cellY);
        }

        if (RectTransformUtility.RectangleContainsScreenPoint(availablePanel, eventData.position, eventData.pressEventCamera))
        {
            if (instance.IsInGrid)
            {
                playerInventory.MoveToAvailable(instance);
                return true;
            }
            return true; // already in Available, dropped back onto Available — treat as a no-op success
        }

        return false;
    }
}
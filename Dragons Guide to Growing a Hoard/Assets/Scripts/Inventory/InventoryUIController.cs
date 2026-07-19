using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

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
    [Tooltip("Container for Available overflow items — assign this to the AvailableGrid child (NOT the " +
             "top-level AvailablePanel), since this is the RectTransform items are actually laid out and " +
             "drag-dropped into. Anchor/pivot must be top-left (0,1), same as gridPanel — this script lays " +
             "items out manually, no layout group needed.")]
    [SerializeField] private RectTransform availablePanel;
    [Tooltip("The top-level Available panel GameObject (the one that also contains the 'Available' header " +
             "text). Used ONLY to show/hide the whole Available section when the Plant detail panel opens — " +
             "separate from availablePanel above, which is just the grid of items.")]
    [SerializeField] private GameObject availablePanelRoot;
    [Tooltip("An existing slot GameObject already placed in your grid (e.g. 'Container' under SlotsContainer). " +
             "It gets hidden at startup and cloned for every item — no prefab asset needed.")]
    [SerializeField] private InventorySlotUI gridSlotTemplate;

    [Tooltip("An existing slot GameObject already placed in Available (e.g. 'ContainerSmall'). " +
             "Cloned the same way as gridSlotTemplate. Leave empty to reuse gridSlotTemplate for both panels.")]
    [SerializeField] private InventorySlotUI availableSlotTemplate;
    [Tooltip("Optional close/back button.")]
    [SerializeField] private Button backButton;

    [Header("Plant Detail Panel")]
    [Tooltip("The detail panel GameObject shown when the player clicks a plant (in the grid or Available). " +
             "Hidden by default — Available is what shows when the inventory first opens.")]
    [SerializeField] private GameObject plantPanel;
    [Tooltip("Image component on the Plant panel that shows the plant's larger detail image " +
             "(CollectablePlant.plantImage) — this is intentionally NOT the small slot icon.")]
    [SerializeField] private Image plantPanelImage;
    [Tooltip("Text component on the Plant panel that shows the plant's name.")]
    [SerializeField] private TMPro.TextMeshProUGUI plantPanelName;
    [Tooltip("Optional close/back button on the Plant panel that returns to the Available view. " +
             "If you don't want a dedicated button, add a Button component to the panel's " +
             "background image instead and assign that here.")]
    [SerializeField] private Button plantPanelCloseButton;

    [Header("Filter Bar")]
    [Tooltip("Infinity icon — explicitly shows everything (clears the filter). This is the one that's " +
             "selected/highlighted by default when the inventory opens with no filter active.")]
    [SerializeField] private Button allFilterButton;
    [Tooltip("Sun icon — filters both panels down to PlantType.Sunny plants.")]
    [SerializeField] private Button sunnyFilterButton;
    [Tooltip("Moon icon — filters both panels down to PlantType.Dark plants.")]
    [SerializeField] private Button darkFilterButton;
    [Tooltip("Wave icon — filters both panels down to PlantType.Water plants.")]
    [SerializeField] private Button waterFilterButton;
    [Tooltip("Skull icon — filters both panels down to PlantType.Dead plants.")]
    [SerializeField] private Button deadFilterButton;

    [Header("Layout")]
    [Tooltip("Pixel size of one grid cell. Item visuals are drawn at footprint * cellSizePx.")]
    [SerializeField] private float cellSizePx = 90f;
    [Tooltip("Gap between cells, purely visual.")]
    [SerializeField] private float cellGapPx = 4f;
    [Tooltip("How many columns to wrap Available items at. No GridLayoutGroup needed — " +
             "this script positions Available slots manually, same as it does for the grid.")]
    [SerializeField] private int availableColumns = 4;

    [Header("Drag Highlight")]
    [Tooltip("Color for empty grid cells while dragging.")]
    [SerializeField] private Color emptyCellColor = new Color(1f, 1f, 1f, 0.12f);
    [Tooltip("Color for already-occupied grid cells while dragging.")]
    [SerializeField] private Color occupiedCellColor = new Color(1f, 0.35f, 0.35f, 0.18f);
    [Tooltip("Color for the footprint under the cursor when the drop would be valid.")]
    [SerializeField] private Color validDropColor = new Color(0.4f, 1f, 0.4f, 0.55f);
    [Tooltip("Color for the footprint under the cursor when the drop would be invalid.")]
    [SerializeField] private Color invalidDropColor = new Color(1f, 0.3f, 0.3f, 0.55f);

    private Canvas rootCanvas;
    private bool isInventoryOpen = false;
    private PlayerControls playerControls;
    private InputAction inventoryAction;
    private readonly Dictionary<string, InventorySlotUI> slotVisuals = new Dictionary<string, InventorySlotUI>();

    // Grid cell highlight overlay — one Image per cell, built lazily and kept
    // in sync with the grid's current dimensions. Lives at the back of
    // gridPanel's hierarchy so item slots (instantiated after it) always
    // render on top.
    private Image[,] cellOverlays;
    private int overlayGridWidth = -1;
    private int overlayGridHeight = -1;
    private InventoryItemInstance draggedInstance;

    // null = no filter active, i.e. show everything (this is the default — there's
    // deliberately no "All" button; not selecting any filter button already means "all").
    private PlantType? activeFilter = null;

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

        if (plantPanelCloseButton != null)
            plantPanelCloseButton.onClick.AddListener(HidePlantDetail);

        if (allFilterButton != null) allFilterButton.onClick.AddListener(() => SetFilter(null));
        if (sunnyFilterButton != null) sunnyFilterButton.onClick.AddListener(() => ToggleFilter(PlantType.Sunny));
        if (darkFilterButton != null) darkFilterButton.onClick.AddListener(() => ToggleFilter(PlantType.Dark));
        if (waterFilterButton != null) waterFilterButton.onClick.AddListener(() => ToggleFilter(PlantType.Water));
        if (deadFilterButton != null) deadFilterButton.onClick.AddListener(() => ToggleFilter(PlantType.Dead));
        RefreshFilterButtonVisuals();

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

    void Update()
    {
        // Escape handling for closing the inventory now lives in ExitMenuController,
        // not here. Two scripts independently polling Escape each frame is a race —
        // Unity doesn't guarantee which Update() runs first, so a single Escape press
        // could close the inventory AND open the Exit menu on the same frame. Having
        // one script own all Escape handling for the whole menu layer avoids that.
    }

    /// <summary>Whether the inventory is currently open — checked by ExitMenuController before opening the Exit menu.</summary>
    public bool IsInventoryOpen => isInventoryOpen;

    /// <summary>Closes the inventory if it's open. Does nothing if already closed. Called by ExitMenuController on Escape.</summary>
    public void CloseInventory()
    {
        if (isInventoryOpen) ToggleInventory();
    }

    private void OnInventoryPerformed(InputAction.CallbackContext context)
    {
        ToggleInventory();
    }

    public void ToggleInventory()
    {
        isInventoryOpen = !isInventoryOpen;
        SetInventoryVisible(isInventoryOpen);
        if(isInventoryOpen)
            if(Tutorial_2.Instance != null)
                Tutorial_2.Instance.OpenedInventory();

        if (isInventoryOpen)
        {
            GameInputModeManager.Instance?.SetMenuUIMode();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ThirdPersonCameraController.CameraLocked = true;
            RefreshUI();
        }
        else
        {
            GameInputModeManager.Instance?.SetGameplayMode();
        }
    }
    void SetInventoryVisible(bool visible)
    {
        if (inventoryRoot != null)
            inventoryRoot.SetActive(visible);

        // Available is the default view every time the inventory opens (or closes) —
        // never leave the Plant detail panel showing from a previous session.
        HidePlantDetail();

        // Re-apply here too (not just Awake) — EventSystem.current can still be null
        // during this script's own Awake depending on scene script execution order,
        // so the "All" button might not have actually been selected yet back then.
        if (visible) RefreshFilterButtonVisuals();
    }

    // ------------------------------------------------------------
    // PLANT DETAIL PANEL — shown when the player clicks a plant in
    // the grid or Available. Called by InventorySlotUI.OnPointerClick.
    // ------------------------------------------------------------
    private string detailInstanceId = null;

    /// <summary>
    /// Populates and shows the Plant detail panel for the clicked item.
    /// Clicking the SAME item again (with no dedicated close button) closes
    /// it back to the default Available/grid view — this is the toggle that
    /// replaces having a separate close button.
    /// </summary>
    public void ShowPlantDetail(InventoryItemInstance instance)
    {
        if (instance == null || plantPanel == null) return;

        if (plantPanel.activeSelf && detailInstanceId == instance.instanceId)
        {
            HidePlantDetail();
            return;
        }

        if (plantPanelImage != null)
        {
            // displayImage is the larger "info card" image — deliberately distinct
            // from instance.icon, which is only the small slot thumbnail. Fall back
            // to the icon so the panel isn't blank if no detail image was assigned.
            Sprite detail = instance.displayImage != null ? instance.displayImage : instance.icon;
            plantPanelImage.sprite = detail;
            plantPanelImage.enabled = detail != null;
        }

        if (plantPanelName != null)
            plantPanelName.text = instance.displayName;

        detailInstanceId = instance.instanceId;
        plantPanel.SetActive(true);

        // Hide the whole Available panel — including its "Available" header text,
        // since that lives on the parent (availablePanelRoot), not on the item
        // container (availablePanel/AvailableGrid) — while the detail view is up.
        // The grid panel is intentionally left alone; it was never meant to hide here.
        if (availablePanelRoot != null) availablePanelRoot.SetActive(false);
    }

    /// <summary>Hides the Plant detail panel, returning the player to the default Available view.</summary>
    public void HidePlantDetail()
    {
        detailInstanceId = null;
        if (plantPanel != null)
            plantPanel.SetActive(false);

        if (availablePanelRoot != null) availablePanelRoot.SetActive(true);
    }

    // ------------------------------------------------------------
    // FILTER BAR — sun/moon/wave/skull icons filter both the grid and
    // Available panels down to one PlantType. There's no "All" button:
    // clicking the active filter's icon again just turns it back off.
    // ------------------------------------------------------------

    private void ToggleFilter(PlantType type)
    {
        SetFilter(activeFilter == type ? (PlantType?)null : type);
    }

    private void SetFilter(PlantType? type)
    {
        activeFilter = type;
        RefreshFilterButtonVisuals();
        RefreshUI();
    }

    private void RefreshFilterButtonVisuals()
    {
        // Each filter button already has its own "active" look configured via
        // Sprite Swap (Selected Sprite = all_active_hover.png etc.), so all we
        // need to do is make the active filter's button the EventSystem's
        // "selected" object — that's what drives the Selected Sprite. null
        // (no type filter) maps to allFilterButton, so "All" reads as selected
        // by default and whenever a filter is cleared — not deselected outright.
        Button activeButton = GetFilterButton(activeFilter);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(activeButton != null ? activeButton.gameObject : null);
    }

    private Button GetFilterButton(PlantType? type)
    {
        if (type == null) return allFilterButton;
        if (type == PlantType.Sunny) return sunnyFilterButton;
        if (type == PlantType.Dark) return darkFilterButton;
        if (type == PlantType.Water) return waterFilterButton;
        if (type == PlantType.Dead) return deadFilterButton;
        return null;
    }

    /// <summary>Applies the active filter (if any) to a list of items for display.</summary>
    private List<InventoryItemInstance> ApplyFilter(List<InventoryItemInstance> source)
    {
        if (activeFilter == null) return source;
        return source.Where(i => i.plantType == activeFilter.Value).ToList();
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

        // Uniform square cell size, read from the template on the canvas (see
        // GetGridCellSize) — NOT derived by dividing gridPanel's width/height
        // independently, which produced rectangular cells whenever the panel's
        // aspect ratio didn't exactly match GridWidth:GridHeight.
        float cellW = GetGridCellSize();
        float cellH = cellW;

        // Keep the cell-highlight overlay in sync with the grid's current
        // size/geometry. Cheap for typical grid sizes, and correctly handles
        // ExpandGrid() changing dimensions mid-game.
        EnsureCellOverlays();
        LayoutCellOverlays();

        foreach (var visual in slotVisuals.Values)
            if (visual != null) Destroy(visual.gameObject);
        slotVisuals.Clear();

        foreach (var instance in ApplyFilter(playerInventory.GetGridItems()))
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
        var availableItems = ApplyFilter(playerInventory.GetAvailableItems());
        int columns = Mathf.Max(1, availableColumns);

        // IMPORTANT: availablePanel's size is NOT touched here — it stays at
        // whatever fixed size you set up on the canvas, exactly like gridPanel.
        // It used to be resized every refresh to just fit the current item
        // count (columns * cellSizePx wide, rows * cellSizePx tall), which
        // shrank the panel's actual RectTransform down to near-zero whenever
        // Available was empty or sparse. HandleDrop/UpdateDragHighlight test
        // drops against this RectTransform's rect, so a shrunken panel meant
        // most of what you could visually see as "the Available area" wasn't
        // actually droppable. Leaving the size alone makes the whole designed
        // rect droppable at all times, regardless of how many items are in it.
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

    /// <summary>
    /// The size (in px) of ONE 1x1 grid cell. Read directly from gridSlotTemplate's
    /// own RectTransform — the size you've already set up by hand on the canvas —
    /// rather than derived by dividing gridPanel's width/height by column/row count.
    /// Dividing the panel only produces square cells if the panel's aspect ratio
    /// happens to exactly match GridWidth:GridHeight; any mismatch stretches cells
    /// into rectangles, which is what caused 2x2 items to render as long slabs
    /// instead of squares.
    /// </summary>
    private float GetGridCellSize()
    {
        if (gridSlotTemplate != null)
        {
            RectTransform templateRect = gridSlotTemplate.GetComponent<RectTransform>();
            if (templateRect != null)
            {
                float w = templateRect.rect.width;
                float h = templateRect.rect.height;
                if (w > 0f && h > 0f)
                    return Mathf.Min(w, h); // guards against the template being slightly non-square
            }
        }

        // Fallback if no template is assigned yet: derive a square cell size that
        // fits within the panel on whichever axis is tighter.
        if (gridPanel != null && playerInventory != null)
        {
            return Mathf.Min(
                gridPanel.rect.width / Mathf.Max(1, playerInventory.GridWidth),
                gridPanel.rect.height / Mathf.Max(1, playerInventory.GridHeight));
        }

        return cellSizePx;
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
    // GRID CELL HIGHLIGHT — shows available/occupied cells while
    // dragging, and the drop footprint under the cursor.
    // Called by InventorySlotUI's drag handlers.
    // ------------------------------------------------------------

    /// <summary>Call from InventorySlotUI.OnBeginDrag.</summary>
    public void BeginDragHighlight(InventoryItemInstance instance)
    {
        draggedInstance = instance;
        EnsureCellOverlays();
        LayoutCellOverlays();
        PaintBaseOverlayState();
    }

    /// <summary>Call from InventorySlotUI.OnDrag (every frame while dragging).</summary>
    public void UpdateDragHighlight(PointerEventData eventData)
    {
        if (draggedInstance == null || cellOverlays == null || playerInventory == null) return;

        // Reset to the base empty/occupied state, then paint the footprint
        // under the cursor on top of it.
        PaintBaseOverlayState();

        if (!RectTransformUtility.RectangleContainsScreenPoint(gridPanel, eventData.position, eventData.pressEventCamera))
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gridPanel, eventData.position, eventData.pressEventCamera, out Vector2 local);

        float cellW = GetGridCellSize();
        float cellH = cellW;

        int originX = Mathf.FloorToInt(local.x / cellW);
        int originY = Mathf.FloorToInt(-local.y / cellH);

        bool valid = playerInventory.Grid.CanPlaceAt(originX, originY, draggedInstance.footprint, draggedInstance.instanceId);
        Color highlight = valid ? validDropColor : invalidDropColor;

        for (int x = originX; x < originX + draggedInstance.footprint.x; x++)
        {
            for (int y = originY; y < originY + draggedInstance.footprint.y; y++)
            {
                if (x < 0 || y < 0 || x >= overlayGridWidth || y >= overlayGridHeight) continue;
                cellOverlays[x, y].color = highlight;
            }
        }
    }

    /// <summary>Call from InventorySlotUI.OnEndDrag, before HandleDrop.</summary>
    public void EndDragHighlight()
    {
        draggedInstance = null;
        if (cellOverlays == null) return;
        foreach (var img in cellOverlays)
            if (img != null) img.color = Color.clear;
    }

    private void EnsureCellOverlays()
    {
        if (playerInventory == null || gridPanel == null) return;

        int w = playerInventory.GridWidth;
        int h = playerInventory.GridHeight;

        if (cellOverlays != null && overlayGridWidth == w && overlayGridHeight == h)
            return;

        if (cellOverlays != null)
        {
            foreach (var img in cellOverlays)
                if (img != null) Destroy(img.gameObject);
        }

        overlayGridWidth = w;
        overlayGridHeight = h;
        cellOverlays = new Image[w, h];

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                GameObject go = new GameObject($"CellOverlay_{x}_{y}", typeof(RectTransform));
                go.transform.SetParent(gridPanel, false);
                go.transform.SetAsFirstSibling(); // stay behind item slots, which are instantiated afterwards

                Image img = go.AddComponent<Image>();
                img.raycastTarget = false; // never intercept the drag's pointer events
                img.color = Color.clear;
                cellOverlays[x, y] = img;
            }
        }
    }

    private void LayoutCellOverlays()
    {
        if (cellOverlays == null || playerInventory == null) return;

        float cellW = GetGridCellSize();
        float cellH = cellW;

        for (int x = 0; x < overlayGridWidth; x++)
        {
            for (int y = 0; y < overlayGridHeight; y++)
            {
                Image img = cellOverlays[x, y];
                if (img == null) continue;

                RectTransform rt = img.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(cellW - cellGapPx, cellH - cellGapPx);
                rt.anchoredPosition = new Vector2(
                    x * cellW + cellGapPx * 0.5f,
                    -(y * cellH + cellGapPx * 0.5f));
            }
        }
    }

    /// <summary>Paints every cell empty/occupied (ignoring the item currently being dragged).</summary>
    private void PaintBaseOverlayState()
    {
        if (cellOverlays == null || playerInventory == null) return;

        for (int x = 0; x < overlayGridWidth; x++)
        {
            for (int y = 0; y < overlayGridHeight; y++)
            {
                InventoryItemInstance occupant = playerInventory.Grid.GetItemAt(x, y);
                bool occupied = occupant != null && occupant.instanceId != draggedInstance?.instanceId;
                cellOverlays[x, y].color = occupied ? occupiedCellColor : emptyCellColor;
            }
        }
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

            float cellW = GetGridCellSize();
            float cellH = cellW;

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
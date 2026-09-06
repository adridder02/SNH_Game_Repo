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
//
// CONSUMABLES/PLACEABLES NOW SHARE THIS SAME GRID:
//   There is no separate "Abilities" section in the main inventory
//   anymore — a Consumable/Placeable stack fills the SAME grid a
//   plant would (auto-placed the first time it's picked up), and
//   overflows to the SAME Available panel once the grid's full,
//   exactly like a plant does. InventorySlotUI displays either kind
//   (see its Occupant property) and the fields below (gridSlotTemplate/
//   availableSlotTemplate) are reused for both — you do NOT need a
//   separate ability slot template or ability panel in your hierarchy
//   any more. Dragging a Consumable onto a hotbar slot still ASSIGNS
//   it there (see TryHandleHotbarDrop) rather than moving it.
// =============================================================
public class InventoryUIController : MonoBehaviour, IHotbarActivator
{
    [Header("Inventory of Player")]
    [SerializeField] private PlayerInventory playerInventory;

    [Header("Ability Inventory")]
    [Tooltip("Auto-found via FindObjectOfType if left empty.")]
    [SerializeField] private PlayerAbilityInventory abilityInventory;
    [Tooltip("Auto-found via FindObjectOfType if left empty. Drives hotbar activation from both number keys and slot clicks.")]
    [SerializeField] private AbilityHotbarSystem hotbarSystem;

    [Header("Mission")]
    [Tooltip("Task 0 is completed the first time the inventory is opened. Assign the same MissionData " +
             "asset used on PlacementSystem's collectionMission field (index 0 = OpenedInventory, " +
             "1/2/3 = Small/Medium/Large pot planted).")]
    [SerializeField] private MissionData collectionMission;

    [Header("Canvas References")]
    [Tooltip("The panel GameObject that gets shown/hidden when toggling the inventory.")]
    [SerializeField] private GameObject inventoryRoot;
    [Tooltip("The main HUD controller — used to hide the HUD (bars/hotbar/icons) while the inventory is " +
             "open and show it again on close. Auto-found in the scene if left empty.")]
    [SerializeField] private MainUIController mainUI;
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
             "Cloned the same way as gridSlotTemplate. Leave empty to reuse gridSlotTemplate for both panels. " +
             "Reused for consumable/placeable stacks too — there's no separate ability slot template any more.")]
    [SerializeField] private InventorySlotUI availableSlotTemplate;
    [Tooltip("Optional close/back button.")]
    [SerializeField] private Button backButton;

    [Header("Plant Detail Panel")]
    [Tooltip("The detail panel GameObject shown when the player clicks a plant or ability item " +
             "(in the grid, Available, or Abilities section). Hidden by default — Available is " +
             "what shows when the inventory first opens.")]
    [SerializeField] private GameObject plantPanel;
    [Tooltip("Image component on the Plant panel that shows the plant's larger detail image " +
             "(CollectablePlant.plantImage) — this is intentionally NOT the small slot icon. Also " +
             "reused to show an ability item's icon when the panel is opened for one of those instead.")]
    [SerializeField] private Image plantPanelImage;
    [Tooltip("Text component on the Plant panel that shows the plant's (or ability item's) name.")]
    [SerializeField] private TMPro.TextMeshProUGUI plantPanelName;
    [Tooltip("Optional close/back button on the Plant panel that returns to the Available view. " +
             "If you don't want a dedicated button, add a Button component to the panel's " +
             "background image instead and assign that here.")]
    [SerializeField] private Button plantPanelCloseButton;
    [Tooltip("Shown ONLY when the panel is open for an ability item that can have an effect right " +
             "now: an untargeted Consumable (e.g. Bubble of Holding, Glowcap Spore). Hidden for " +
             "plants (they're managed via the pot menu's own buttons, not this panel), for " +
             "Placeables (they need the player to actually place + interact with them in the " +
             "world, not an instant press-to-use), and for pot-targeted Consumables like Verdant " +
             "Algae/Pollen Puff/Dewdrop (those only make sense from inside a specific pot's own " +
             "Abilities panel — PotMenuUIController).")]
    [SerializeField] private Button plantPanelUseButton;

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
    [Tooltip("Filters both panels down to Consumable ability item stacks only, hiding every plant. " +
             "Mutually exclusive with the Sunny/Dark/Water/Dead/All buttons above — picking this " +
             "clears whichever PlantType filter was active, and vice versa.")]
    [SerializeField] private Button consumablesFilterButton;

    [Header("Hotbar")]
    [Tooltip("The fixed hand-placed hotbar slots at the bottom of the panel, in the SAME ORDER as " +
             "AbilityHotbarSystem's own slots array (index 0 = key '1', etc.) — mismatched order " +
             "means clicking/pressing a slot won't visually match what actually activates.")]
    [SerializeField] private List<HotbarSlotUI> hotbarSlotUIs = new List<HotbarSlotUI>();

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
    private IGridPlaceable draggedInstance;

    // One highlight overlay per hotbar slot (Inventory panel's own row only — that's the row
    // actually reachable while dragging; the HUD row isn't on-screen behind the Inventory panel).
    // Built lazily the first time a drag starts, parallel to hotbarSlotUIs by index.
    private Image[] hotbarOverlays;

    // Single full-panel overlay for the Available panel — unlike the grid (per-cell, occupancy
    // matters) or the hotbar (per-slot, eligibility matters), a drop onto Available always
    // succeeds unconditionally (see HandleDrop below), so one hover highlight for the whole panel
    // is all it needs. Built lazily the first time a drag starts.
    private Image availableOverlay;

    // null = no filter active, i.e. show everything (this is the default — there's
    // deliberately no "All" button; not selecting any filter button already means "all").
    private PlantType? activeFilter = null;
    private bool showOnlyConsumables = false;

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
        if (abilityInventory == null)
            abilityInventory = FindObjectOfType<PlayerAbilityInventory>();
        if (hotbarSystem == null)
            hotbarSystem = FindObjectOfType<AbilityHotbarSystem>();
        if (mainUI == null)
            mainUI = FindObjectOfType<MainUIController>();

        if (backButton != null)
            backButton.onClick.AddListener(ToggleInventory);

        if (plantPanelCloseButton != null)
            plantPanelCloseButton.onClick.AddListener(HidePlantDetail);

        if (allFilterButton != null) allFilterButton.onClick.AddListener(() => SetFilter(null));
        if (sunnyFilterButton != null) sunnyFilterButton.onClick.AddListener(() => ToggleFilter(PlantType.Sunny));
        if (darkFilterButton != null) darkFilterButton.onClick.AddListener(() => ToggleFilter(PlantType.Dark));
        if (waterFilterButton != null) waterFilterButton.onClick.AddListener(() => ToggleFilter(PlantType.Water));
        if (deadFilterButton != null) deadFilterButton.onClick.AddListener(() => ToggleFilter(PlantType.Dead));
        if (consumablesFilterButton != null) consumablesFilterButton.onClick.AddListener(ToggleConsumablesFilter);
        RefreshFilterButtonVisuals();

        // These are live scene objects being used as clone templates — hide the
        // originals so they don't show up as a phantom extra slot.
        if (gridSlotTemplate != null) gridSlotTemplate.gameObject.SetActive(false);
        if (availableSlotTemplate != null) availableSlotTemplate.gameObject.SetActive(false);

        for (int i = 0; i < hotbarSlotUIs.Count; i++)
            hotbarSlotUIs[i]?.Initialize(this, i);

        SetInventoryVisible(false);

        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
    }

    void OnEnable()
    {
        playerControls?.Enable();
        if (playerInventory != null)
            playerInventory.OnInventoryChanged += RefreshUI;
        if (abilityInventory != null)
            // A consumable/placeable stack's count can change (picked up more, used one) without
            // its grid position changing — PlayerInventory.OnInventoryChanged only fires when a
            // stack's PLACEMENT changes, so this covers redrawing the "xN" count label too.
            abilityInventory.OnChanged += RefreshUI;
        if (hotbarSystem != null)
            hotbarSystem.OnSlotsChanged += RefreshHotbarUI;
    }  

    void OnDisable()
    {
        playerControls?.Disable();
        if (playerInventory != null)
            playerInventory.OnInventoryChanged -= RefreshUI;
        if (abilityInventory != null)
            abilityInventory.OnChanged -= RefreshUI;
        if (hotbarSystem != null)
            hotbarSystem.OnSlotsChanged -= RefreshHotbarUI;
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
        mainUI?.SetHudHidden(isInventoryOpen, this);
        if (isInventoryOpen && collectionMission != null && collectionMission.tasks.Count > 0)
            MissionProgressManager.Instance?.CompleteTask(collectionMission, collectionMission.tasks[0]); // OpenedInventory

        if (isInventoryOpen)
        {
            MenuLayerManager.NotifyOpened(this, CloseInventory);

            GameInputModeManager.Instance?.SetMenuUIMode();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ThirdPersonCameraController.CameraLocked = true;
            RefreshUI();
            RefreshHotbarUI();

            // Lets a tutorial step (e.g. "Press [I] to open your inventory") auto-advance the instant
            // this actually happens, instead of requiring a click on the prompt itself. No-ops if no
            // TutorialSequenceController exists, or if the current step isn't listening for this id.
            TutorialSequenceController.Instance?.NotifyExternalTrigger("inventory_opened");
        }
        else
        {
            MenuLayerManager.NotifyClosed(this);
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
    // DETAIL PANEL — shown when the player clicks a plant OR a
    // consumable/placeable stack, wherever it's sitting in the shared
    // grid/Available. Both are routed here by the same InventorySlotUI.
    // OnPointerClick (it checks which kind of item it's holding) —
    // ShowPlantDetail for a plant, ShowAbilityDetail for a stack. Both
    // share the same panel/image/name fields; only plantPanelUseButton's
    // visibility differs between the two.
    // ------------------------------------------------------------
    private string detailInstanceId = null;
    private AbilityItemData detailAbilityData = null;

    /// <summary>
    /// Populates and shows the detail panel for the clicked plant.
    /// Clicking the SAME item again (with no dedicated close button) closes
    /// it back to the default Available/grid view — this is the toggle that
    /// replaces having a separate close button.
    /// </summary>
    public void ShowPlantDetail(InventoryItemInstance instance)
    {
        if (instance == null || plantPanel == null) return;

        if (plantPanel.activeSelf && detailAbilityData == null && detailInstanceId == instance.instanceId)
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

        // Plants are never "used" from this panel — they're managed via the pot menu's own
        // Choose Plant / Remove / Harvest buttons — so the Use button never shows here.
        if (plantPanelUseButton != null)
            plantPanelUseButton.gameObject.SetActive(false);

        detailInstanceId = instance.instanceId;
        detailAbilityData = null;
        plantPanel.SetActive(true);

        // Hide the whole Available panel — including its "Available" header text,
        // since that lives on the parent (availablePanelRoot), not on the item
        // container (availablePanel/AvailableGrid) — while the detail view is up.
        // The grid panel is intentionally left alone; it was never meant to hide here.
        if (availablePanelRoot != null) availablePanelRoot.SetActive(false);
    }

    /// <summary>
    /// Populates and shows the SAME detail panel for the clicked ability item stack. The Use
    /// button only appears when this item can actually have an effect right now: an untargeted
    /// Consumable. Placeables (need the player to place + interact with them in the world) and
    /// pot-targeted Consumables (Verdant Algae, Pollen Puff, Dewdrop — only usable from inside a
    /// pot's own Abilities panel) never show it. Clicking the same stack again closes the panel,
    /// same toggle behaviour as ShowPlantDetail.
    /// </summary>
    public void ShowAbilityDetail(AbilityItemInstance stack)
    {
        if (stack?.data == null || plantPanel == null) return;

        if (plantPanel.activeSelf && detailAbilityData == stack.data)
        {
            HidePlantDetail();
            return;
        }

        if (plantPanelImage != null)
        {
            plantPanelImage.sprite = stack.data.icon;
            plantPanelImage.enabled = stack.data.icon != null;
        }

        if (plantPanelName != null)
            plantPanelName.text = stack.data.displayName;

        bool usableNow = stack.data.kind == AbilityKind.Consumable &&
                          !AbilityConsumableEffects.RequiresPotTarget(stack.data.effectId);

        if (plantPanelUseButton != null)
        {
            plantPanelUseButton.gameObject.SetActive(usableNow);
            plantPanelUseButton.onClick.RemoveAllListeners();
            if (usableNow)
                plantPanelUseButton.onClick.AddListener(() => UseAbility(stack.data));
        }

        detailInstanceId = null;
        detailAbilityData = stack.data;
        plantPanel.SetActive(true);

        if (availablePanelRoot != null) availablePanelRoot.SetActive(false);
    }

    /// <summary>Hides the detail panel, returning the player to the default Available view.</summary>
    public void HidePlantDetail()
    {
        detailInstanceId = null;
        detailAbilityData = null;
        if (plantPanelUseButton != null)
            plantPanelUseButton.gameObject.SetActive(false);
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
        showOnlyConsumables = false; // mutually exclusive with the Consumables filter
        RefreshFilterButtonVisuals();
        RefreshUI();
    }

    private void ToggleConsumablesFilter()
    {
        showOnlyConsumables = !showOnlyConsumables;
        if (showOnlyConsumables)
            activeFilter = null; // mutually exclusive with the PlantType filters

        RefreshFilterButtonVisuals();
        RefreshUI();
    }

    private void RefreshFilterButtonVisuals()
    {
        // Each filter button already has its own "active" look configured via
        // Sprite Swap (Selected Sprite = all_active_hover.png etc.), so all we
        // need to do is make the active filter's button the EventSystem's
        // "selected" object — that's what drives the Selected Sprite. null
        // (no type filter, and Consumables off) maps to allFilterButton, so
        // "All" reads as selected by default and whenever a filter is cleared.
        Button activeButton = showOnlyConsumables ? consumablesFilterButton : GetFilterButton(activeFilter);
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

    /// <summary>Applies whichever filter (if any) is active to a list of items for display.
    /// Sunny/Dark/Water/Dead are plant-only filters — they now hide ability item stacks entirely,
    /// not just leave them unaffected, so picking e.g. Water only shows Water plants and nothing
    /// else. The Consumables filter is the reverse: hides every plant and shows every ability item
    /// stack regardless of kind (Consumable AND Placeable — e.g. Waterbell shows here too, not
    /// just true Consumables).</summary>
    private List<IGridPlaceable> ApplyFilter(List<IGridPlaceable> source)
    {
        if (showOnlyConsumables)
            return source.Where(i => i is AbilityItemInstance).ToList();

        if (activeFilter == null) return source;
        return source.Where(i => i is InventoryItemInstance plant && plant.plantType == activeFilter.Value).ToList();
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

        foreach (var occupant in ApplyFilter(playerInventory.GetGridItems()))
        {
            InventorySlotUI slot = CreateSlot(occupant, gridPanel, gridSlotTemplate);
            RectTransform rt = slot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(
                occupant.Footprint.x * cellW - cellGapPx,
                occupant.Footprint.y * cellH - cellGapPx);
            rt.anchoredPosition = new Vector2(
                occupant.GridX * cellW + cellGapPx * 0.5f,
                -(occupant.GridY * cellH + cellGapPx * 0.5f));
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
            InventorySlotUI slot = CreateSlot(availableItems[i], availablePanel, availableTemplate, isAvailableSlot: true);
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

    InventorySlotUI CreateSlot(IGridPlaceable occupant, RectTransform parent, InventorySlotUI template, bool isAvailableSlot = false)
    {
        InventorySlotUI slot = Instantiate(template, parent);
        slot.gameObject.SetActive(true); // template itself is hidden — the clone needs to be shown
        slot.Initialize(occupant, this, rootCanvas, isAvailableSlot);
        slotVisuals[occupant.InstanceId] = slot;
        return slot;
    }

    /// <summary>Called by the detail panel's Use button (ShowAbilityDetail) — reached only for
    /// untargeted Consumables now (Placeables/pot-targeted Consumables never show that button —
    /// see ShowAbilityDetail), but the guard clauses below are left in for safety in case this
    /// is ever called from somewhere else too.</summary>
    public void UseAbility(AbilityItemData data)
    {
        if (data == null || abilityInventory == null) return;

        if (data.kind == AbilityKind.Consumable && AbilityConsumableEffects.RequiresPotTarget(data.effectId))
        {
            Debug.Log($"[InventoryUIController] '{data.displayName}' needs a pot target — open a pot's menu to use it.");
            return;
        }

        if (data.kind == AbilityKind.Placeable)
        {
            AbilityPlacementSystem placementSystem = FindObjectOfType<AbilityPlacementSystem>();
            if (placementSystem == null)
            {
                Debug.LogWarning("[InventoryUIController] No AbilityPlacementSystem in scene.");
                return;
            }
            placementSystem.BeginPlacing(data);
            ToggleInventory(); // close the panel so the player can see the world grid to place on
            return;
        }

        // Untargeted Consumable (ExpandInventory, DragonGlow) — applies straight to the player.
        // Deliberately does NOT close the inventory (unlike Placeable above, which needs the world
        // grid visible) — using a Consumable like Bubble Blossom should let the player keep browsing/
        // using more items without the panel getting dismissed out from under them each time.
        if (AbilityConsumableEffects.TryApply(data, abilityInventory.gameObject, null))
        {
            abilityInventory.TryConsume(data, 1);
            RefreshUI(); // reflect the new state (e.g. the newly expanded row, or this stack's count)
        }
    }

    // ------------------------------------------------------------
    // HOTBAR — mirrors AbilityHotbarSystem's slot data onto the hand-placed
    // hotbar row. Assignment is drag-and-drop straight from the main grid/
    // Available (see TryHandleHotbarDrop, called by InventorySlotUI); activation
    // is either a number key (AbilityHotbarSystem.Update) or clicking the slot
    // itself (ActivateHotbarSlot, called by HotbarSlotUI.OnPointerClick).
    // ------------------------------------------------------------
    void RefreshHotbarUI()
    {
        if (hotbarSystem == null) return;
        foreach (var slot in hotbarSlotUIs)
            slot?.Refresh(hotbarSystem);
    }

    public void ActivateHotbarSlot(int slotIndex)
    {
        if (hotbarSystem == null) return;

        AbilityItemData data = hotbarSystem.GetAssigned(slotIndex);
        bool wasPlaceable = data != null && data.kind == AbilityKind.Placeable;

        hotbarSystem.ActivateSlot(slotIndex);

        // Placement mode needs the world grid visible — close the panel exactly like UseAbility does.
        if (wasPlaceable) ToggleInventory();
    }

    /// <summary>Called by HotbarSlotUI.OnPointerClick on right-click. Just forgets the slot's
    /// assignment — doesn't touch the player's actual item count, so nothing is lost.</summary>
    public void ClearHotbarSlot(int slotIndex) => hotbarSystem?.Clear(slotIndex);

    /// <summary>Called by InventorySlotUI.OnEndDrag BEFORE the normal grid/Available move check.
    /// Only relevant for a Consumable/Placeable stack (plants never match) — if the drop point is
    /// over one of the hand-placed hotbar slots, this ASSIGNS the item there (see
    /// AbilityHotbarSystem.CanAssign for which items are eligible — only untargeted Consumables)
    /// and returns true either way, meaning "handled here, don't also try to move it in the grid".
    /// Returns false if the drop wasn't over any hotbar slot at all, so the caller falls through
    /// to the normal grid/Available move logic instead.</summary>
    public bool TryHandleHotbarDrop(InventorySlotUI slot, PointerEventData eventData)
    {
        if (!(slot?.Occupant is AbilityItemInstance stack) || stack.data == null || hotbarSystem == null)
            return false;

        for (int i = 0; i < hotbarSlotUIs.Count; i++)
        {
            HotbarSlotUI hotbarSlot = hotbarSlotUIs[i];
            if (hotbarSlot == null) continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(
                    hotbarSlot.RectTransform, eventData.position, eventData.pressEventCamera))
            {
                bool assigned = hotbarSystem.TryAssign(i, stack.data);
                if (!assigned)
                    Debug.Log($"[InventoryUIController] '{stack.data.displayName}' can't go on the hotbar " +
                              "— only Consumables that don't need a pot target are hotbar-eligible.");
                else
                    RefreshHotbarUI();
                return true; // landed on a hotbar slot either way — never move the item in the grid for this drop
            }
        }

        return false;
    }

    // ------------------------------------------------------------
    // GRID CELL HIGHLIGHT — shows available/occupied cells while
    // dragging, and the drop footprint under the cursor.
    // Called by InventorySlotUI's drag handlers.
    // ------------------------------------------------------------

    /// <summary>Call from InventorySlotUI.OnBeginDrag.</summary>
    public void BeginDragHighlight(IGridPlaceable instance)
    {
        draggedInstance = instance;
        EnsureCellOverlays();
        LayoutCellOverlays();
        PaintBaseOverlayState();

        EnsureHotbarOverlays();
        PaintHotbarBaseOverlayState();

        EnsureAvailableOverlay();
        if (availableOverlay != null) availableOverlay.color = emptyCellColor;
    }

    /// <summary>Call from InventorySlotUI.OnDrag (every frame while dragging).</summary>
    public void UpdateDragHighlight(PointerEventData eventData)
    {
        UpdateHotbarDragHighlight(eventData);
        UpdateAvailableDragHighlight(eventData);

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

        bool valid = playerInventory.Grid.CanPlaceAt(originX, originY, draggedInstance.Footprint, draggedInstance.InstanceId);
        Color highlight = valid ? validDropColor : invalidDropColor;

        for (int x = originX; x < originX + draggedInstance.Footprint.x; x++)
        {
            for (int y = originY; y < originY + draggedInstance.Footprint.y; y++)
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

        ClearHotbarOverlays();

        if (availableOverlay != null) availableOverlay.color = Color.clear;
    }

    // ------------------------------------------------------------
    // HOTBAR DRAG HIGHLIGHT — same green/red valid-drop feedback as the
    // grid cells above, applied to the Inventory panel's own hotbar row
    // (the row actually reachable while dragging). A slot lights up
    // green under the cursor if the item being dragged could actually be
    // assigned there (see AbilityHotbarSystem.CanAssign — only untargeted
    // Consumables), or red if it's hovering a slot but can't go there
    // (a plant, a Placeable, or a pot-targeted Consumable like Algae).
    // ------------------------------------------------------------

    /// <summary>Creates one transparent overlay Image as a child of each hotbar slot's RectTransform,
    /// the first time a drag happens — no Editor setup needed, same lazy-build approach as the grid's
    /// cell overlays. Safe to call every drag start; does nothing once already built.</summary>
    private void EnsureHotbarOverlays()
    {
        if (hotbarOverlays != null && hotbarOverlays.Length == hotbarSlotUIs.Count) return;

        hotbarOverlays = new Image[hotbarSlotUIs.Count];
        for (int i = 0; i < hotbarSlotUIs.Count; i++)
        {
            HotbarSlotUI slot = hotbarSlotUIs[i];
            if (slot == null) continue;

            GameObject go = new GameObject("DragHighlightOverlay", typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(slot.RectTransform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = go.AddComponent<Image>();
            img.raycastTarget = false; // never intercept the drag's pointer events
            img.color = Color.clear;

            hotbarOverlays[i] = img;
        }
    }

    private void ClearHotbarOverlays()
    {
        if (hotbarOverlays == null) return;
        foreach (var img in hotbarOverlays)
            if (img != null) img.color = Color.clear;
    }

    /// <summary>Paints every hotbar slot with the same persistent "you could drop here" gray used by
    /// the grid's empty cells — shown for the whole duration of a drag, not just while the cursor is
    /// directly over a slot, so the player can see where dragging is possible before they get there.
    /// UpdateHotbarDragHighlight overrides this per-slot with green/red while actually hovering.</summary>
    private void PaintHotbarBaseOverlayState()
    {
        if (hotbarOverlays == null) return;
        foreach (var img in hotbarOverlays)
            if (img != null) img.color = emptyCellColor;
    }

    /// <summary>Called every frame while dragging (from UpdateDragHighlight above). Lights up
    /// whichever hotbar slot is currently under the cursor — green if the dragged item could be
    /// assigned there, red if not — and leaves every other slot transparent.</summary>
    private void UpdateHotbarDragHighlight(PointerEventData eventData)
    {
        if (hotbarOverlays == null || draggedInstance == null) return;

        bool eligible = draggedInstance is AbilityItemInstance stack && AbilityHotbarSystem.CanAssign(stack.data);

        for (int i = 0; i < hotbarSlotUIs.Count; i++)
        {
            HotbarSlotUI slot = hotbarSlotUIs[i];
            Image overlay = i < hotbarOverlays.Length ? hotbarOverlays[i] : null;
            if (slot == null || overlay == null) continue;

            bool hovering = RectTransformUtility.RectangleContainsScreenPoint(
                slot.RectTransform, eventData.position, eventData.pressEventCamera);

            overlay.color = hovering ? (eligible ? validDropColor : invalidDropColor) : emptyCellColor;
        }
    }

    // ------------------------------------------------------------
    // AVAILABLE PANEL DRAG HIGHLIGHT — a drop onto Available always succeeds unconditionally
    // (see HandleDrop below), so unlike the grid/hotbar there's no valid/invalid distinction to
    // show — just whether you're hovering it at all, same visual weight as validDropColor so it
    // reads as "yes, you can drop here" at a glance.
    // ------------------------------------------------------------

    /// <summary>Creates one transparent overlay Image covering the whole Available panel, the first
    /// time a drag happens — same lazy-build approach as the grid/hotbar overlays.</summary>
    private void EnsureAvailableOverlay()
    {
        if (availableOverlay != null || availablePanel == null) return;

        GameObject go = new GameObject("DragHighlightOverlay", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(availablePanel, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetAsFirstSibling(); // stay behind item slots, same as the grid's cell overlays

        availableOverlay = go.AddComponent<Image>();
        availableOverlay.raycastTarget = false; // never intercept the drag's pointer events
        availableOverlay.color = Color.clear;
    }

    /// <summary>Called every frame while dragging (from UpdateDragHighlight above). Lights up the
    /// whole Available panel while the cursor is over it, and clears otherwise.</summary>
    private void UpdateAvailableDragHighlight(PointerEventData eventData)
    {
        if (availableOverlay == null || draggedInstance == null || availablePanel == null) return;

        bool hovering = RectTransformUtility.RectangleContainsScreenPoint(
            availablePanel, eventData.position, eventData.pressEventCamera);

        availableOverlay.color = hovering ? validDropColor : emptyCellColor;
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
                IGridPlaceable occupant = playerInventory.Grid.GetItemAt(x, y);
                bool occupied = occupant != null && occupant.InstanceId != draggedInstance?.InstanceId;
                cellOverlays[x, y].color = occupied ? occupiedCellColor : emptyCellColor;
            }
        }
    }

    // ------------------------------------------------------------
    // DROP RESOLUTION — called by InventorySlotUI.OnEndDrag
    // ------------------------------------------------------------

    /// <summary>Returns true if the drop was handled (item moved), false if the slot should snap back.
    /// Works the same whether the slot holds a plant or a consumable/placeable stack — both are
    /// IGridPlaceable, and the shared grid doesn't care which.</summary>
    public bool HandleDrop(InventorySlotUI slot, PointerEventData eventData)
    {
        IGridPlaceable occupant = slot.Occupant;
        if (occupant == null) return false;

        if (RectTransformUtility.RectangleContainsScreenPoint(gridPanel, eventData.position, eventData.pressEventCamera))
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                gridPanel, eventData.position, eventData.pressEventCamera, out Vector2 local);

            float cellW = GetGridCellSize();
            float cellH = cellW;

            int cellX = Mathf.FloorToInt(local.x / cellW);
            int cellY = Mathf.FloorToInt(-local.y / cellH); // gridPanel pivot is top-left, so local.y <= 0 going down

            return playerInventory.TryPlaceInGrid(occupant, cellX, cellY);
        }

        if (RectTransformUtility.RectangleContainsScreenPoint(availablePanel, eventData.position, eventData.pressEventCamera))
        {
            if (occupant.IsInGrid)
            {
                playerInventory.MoveToAvailable(occupant);
                return true;
            }
            return true; // already in Available, dropped back onto Available — treat as a no-op success
        }

        return false;
    }
}
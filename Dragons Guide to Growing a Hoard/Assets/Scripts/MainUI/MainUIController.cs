// =============================================================
// MainUIController.cs
// -------------------------------------------------------------
// Attach this to the main HUD Canvas.
//
// WHAT IT DRIVES:
//   - Water bar          — same ImageFillBar setup as the one in
//                          PotMenuUIController, hooked to the same
//                          PlayerInventory water pool.
//   - Journal icon       — calls JournalUIController.ToggleJournal()
//   - Inventory icon     — calls InventoryUIController.ToggleInventory()
//   - Miasma bar          — ImageFillBar, useFillGradient OFF (fixed
//                          colour), showing MiasmaController.CurrentSize
//                          normalized against MiasmaController.MaxSize.
//   - Zone happiness bar  — ImageFillBar, useFillGradient ON (e.g.
//                          red -> yellow -> green). Automatically
//                          follows whichever zone the player is
//                          currently standing inside, reported by
//                          PlayerZoneTracker.cs on the player.
//   - Tool selector slots — 4 Button slots. Slots 0-2 toggle the
//                          Place/Remove/Move tools on PlacementSystem
//                          (mirrors the F/X/G keybinds) and light up
//                          to show whichever tool is currently active.
//                          Slot 3 is still reserved/unused.
//   - Interact prompt (HUD) — a fixed screen-space element that's just
//                          shown/hidden via SetInteractPromptVisible().
//                          This is the alternative PotInteraction can
//                          switch to (its useWorldSpacePrompt toggle)
//                          instead of its own floating, player-following,
//                          billboarded world-space prompt.
//   - Secondary HUD visibility — while placing/moving/removing a pot
//                          (PlacementSystem) or an ability placeable
//                          (AbilityPlacementSystem), secondaryHudRoot
//                          hides automatically so only the hotbar and
//                          tool selector remain visible. See
//                          RefreshSecondaryHudVisibility().
//
// SETUP:
//   1. Attach to the HUD Canvas GameObject.
//   2. Build the water bar the same way as the one in the pot menu
//      (Background/Track/Fill/Indicator/Outline images, Fill Image
//      Type = Filled) and assign it to waterBar.
//   3. Assign journalButton/journalUI and inventoryButton/inventoryUI
//      to the two icon buttons and their respective controllers.
//   4. Build the miasma bar (fixed colour, useFillGradient = false)
//      and assign it + the MiasmaController to miasmaBar/miasma.
//   5. Build the zone happiness bar (useFillGradient = true, with a
//      red->yellow->green fillColorGradient) and assign it. Also
//      attach PlayerZoneTracker.cs to the player/dragon GameObject
//      (see that file's header) and assign it to playerZoneTracker.
//   6. Drag the 4 tool-selector slot buttons into toolSlots (slot 0 =
//      Place, 1 = Remove, 2 = Move, 3 = Water) and assign the
//      scene's PlacementSystem to placementSystem. Nothing else to
//      wire up — clicking a slot calls the matching Toggle*Mode() on
//      PlacementSystem, and the slot's tint follows PlacementSystem's
//      OnModeChanged event, so it stays correct whether the tool was
//      turned on/off by the button or by its keybind.
//
// HOW ZONE-FOLLOWING WORKS:
//   PlayerZoneTracker.cs (attached to the player, not here) uses
//   OnTriggerEnter/OnTriggerExit against each zone's own trigger
//   BoxCollider — the same volumes ZoneHealth already uses for plant
//   detection — so Unity's physics reports overlaps directly instead
//   of this script polling/raycasting for the player's position. This
//   also sidesteps needing a reliable player world-position reference
//   here: PlayerInventory (used for the water bar) intentionally lives
//   on the Inventory UI, not the player, so its transform is UI-space
//   and was never usable for this.
//
// REMOVING THE OLD MIASMA UI (UI_Script.cs):
//   1. Delete the GameObject that has UI_Script + UIDocument attached
//      (the one with the "MiasmaProg" ProgressBar / IntensityLabel /
//      PlantsAffected / NextIncrease UXML).
//   2. Delete UI_Script.cs and its UXML/USS assets — nothing else
//      references them, so nothing else breaks.
//   3. Hook up the new miasmaBar here instead.
// =============================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainUIController : MonoBehaviour, IHotbarActivator
{
    [Header("Player")]
    [Tooltip("Auto-found in the scene if left empty (same as PotInteraction's dragonInventory). " +
             "Only used for the water bar — NOT the player's world position (see " +
             "playerZoneTracker below for that).")]
    [SerializeField] private PlayerInventory playerInventory;

    [Header("Water Bar")]
    [Tooltip("Same ImageFillBar setup/prefab as PotMenuUIController's water bar. Fixed colour, " +
             "useFillGradient left off.")]
    [SerializeField] private ImageFillBar waterBar;

    [Header("Journal Icon")]
    [SerializeField] private Button journalButton;
    [SerializeField] private JournalUIController journalUI;
    [Tooltip("Small dot shown on the journal icon while any species has been discovered but its page " +
             "hasn't been opened yet (NewItemTracker.HasAnyUnseenJournal). Optional.")]
    [SerializeField] private GameObject journalNewDot;

    [Header("Inventory Icon")]
    [SerializeField] private Button inventoryButton;
    [SerializeField] private InventoryUIController inventoryUI;
    [Tooltip("Small dot shown on the inventory icon while any item type has been acquired but its " +
             "slot hasn't been clicked yet, in either the grid or Available " +
             "(NewItemTracker.HasAnyUnseenInventory). Optional.")]
    [SerializeField] private GameObject inventoryNewDot;

    [Header("Miasma Bar")]
    [Tooltip("Fixed-colour bar (useFillGradient = false) showing the current miasma size.")]
    [SerializeField] private ImageFillBar miasmaBar;
    [SerializeField] private MiasmaController miasma;

    [Header("Zone Happiness Bar")]
    [Tooltip("Gradient bar (useFillGradient = true, e.g. red->yellow->green) showing the " +
             "average happiness of plants in whichever zone the player is currently standing in.")]
    [SerializeField] private ImageFillBar zoneHappinessBar;
    [Tooltip("PlayerZoneTracker.cs on the player/dragon — reports which zone's trigger volume " +
             "the player is currently overlapping. NOT PlayerInventory (that's UI-space, not a " +
             "world position).")]
    [SerializeField] private PlayerZoneTracker playerZoneTracker;

    [Header("Tool Selector Slots")]
    [Tooltip("Slot 0 = Place tool, slot 1 = Remove tool, slot 2 = Move tool. Slot 3 is still " +
             "reserved/unused. Each slot toggles its tool on PlacementSystem — pressing it while " +
             "that tool is active turns the tool back off, same as pressing its keybind (F/X/G) " +
             "would.")]
    [SerializeField] private Button[] toolSlots = new Button[4];
    [Tooltip("The scene's PlacementSystem. Required for the tool slots above to do anything.")]
    [SerializeField] private PlacementSystem placementSystem;
    [Tooltip("Tint applied to a tool slot's button graphic while that tool is the active one.")]
    [SerializeField] private Color toolActiveColor = new Color(1f, 0.85f, 0.4f);
    [Tooltip("Tint applied to a tool slot's button graphic while that tool is NOT active.")]
    [SerializeField] private Color toolInactiveColor = Color.white;

    [Header("Hotbar")]
    [Tooltip("The persistent hotbar row shown on the main gameplay HUD (as opposed to the preview " +
             "row tucked into the Inventory panel, which InventoryUIController owns separately) — " +
             "same AbilityHotbarSystem data, just a second always-visible place to see/click it. " +
             "SAME ORDER as AbilityHotbarSystem's own slots array (index 0 = key '1', etc.).")]
    [SerializeField] private List<HotbarSlotUI> hudHotbarSlotUIs = new List<HotbarSlotUI>();
    [Tooltip("Auto-found in the scene if left empty.")]
    [SerializeField] private AbilityHotbarSystem hotbarSystem;

    [Header("HUD Visibility")]
    [Tooltip("Everything on the HUD that should hide while a menu is open — water bar, miasma bar, " +
             "zone bar, tool slots, hotbar, journal/inventory icons, etc. Parent all of that under one " +
             "GameObject in the Editor and assign it here. The interact prompt below is handled " +
             "separately (it's already off whenever a menu is open, via GameInputModeManager), so it " +
             "doesn't need to live under this root.")]
    [SerializeField] private GameObject hudRoot;

    [Tooltip("A SECOND, more selective group living INSIDE hudRoot — everything EXCEPT the hotbar and " +
             "tool selector slots (water bar, miasma bar, zone bar, journal/inventory icons, etc). " +
             "Parent those specific elements under their own child GameObject and assign it here; leave " +
             "hotbar/tool slots as siblings outside it. While placing/moving/removing a pot, or placing/" +
             "removing an ability placeable (Sparkmint leaf, Waterbell, ...), this hides while hotbar " +
             "and the tool selector stay visible — see RefreshSecondaryHudVisibility(). Being a child of " +
             "hudRoot means it's automatically irrelevant whenever hudRoot itself is off (Journal/" +
             "Inventory open) — no conflict between the two systems.")]
    [SerializeField] private GameObject secondaryHudRoot;

    [Tooltip("Ability placeables (Sparkmint leaf, Waterbell, ...) — the OTHER placement mode, alongside " +
             "PlacementSystem's pots. Auto-found in the scene if left empty. Used only to know whether " +
             "secondaryHudRoot should be hidden right now (see IsActive below).")]
    [SerializeField] private AbilityPlacementSystem abilityPlacementSystem;

    [Tooltip("Wall placeables (Clovenwick, etc). Auto-found in the scene if left empty. Used for the " +
             "Floor/Wall Placement Mode banner below.")]
    [SerializeField] private WallPlacementSystem wallPlacementSystem;

    [Header("Placement Mode Banner")]
    [Tooltip("Root of the banner shown while PlacementSystem (Floor) or WallPlacementSystem (Wall) is " +
             "in Placing mode — just an image with text on it. Hidden the rest of the time.")]
    [SerializeField] private GameObject placementBannerRoot;
    [SerializeField] private TextMeshProUGUI placementBannerText;
    [SerializeField] private string floorPlacementBannerText = "Floor Placement Mode";
    [SerializeField] private string wallPlacementBannerText = "Wall Placement Mode";

    [Header("Interact Prompt (HUD)")]
    [Tooltip("Fixed screen-space element (e.g. a 'Press E' panel docked on the HUD) — just enabled/" +
             "disabled, no positioning or billboarding. PotInteraction calls SetInteractPromptVisible() " +
             "on this when its own useWorldSpacePrompt toggle is OFF, instead of using its floating " +
             "world-space prompt.")]
    [SerializeField] private GameObject interactPromptHUD;

    [Tooltip("Second fixed screen-space element, positioned directly below interactPromptHUD in the " +
             "Editor (the 'Quick Water' / 'Water Plant' option) — shown only while the nearby pot " +
             "actually has a plant. Unlike interactPromptHUD, only PotInteraction ever drives this one " +
             "(HarvestNodeContainer etc. have no use for it), so it doesn't need the same multi-" +
             "requester tracking interactPromptHUD has — a plain show/hide is enough.")]
    [SerializeField] private GameObject waterPromptHUD;

    [Tooltip("The 'E' key icon Image on interactPromptHUD — hidden entirely (not just dimmed) while " +
             "this prompt isn't the active selection, so only the currently-active option shows a key " +
             "to press.")]
    [SerializeField] private Image interactPromptEIcon;

    [Tooltip("Same as interactPromptEIcon, but for waterPromptHUD.")]
    [SerializeField] private Image waterPromptEIcon;

    [Tooltip("CanvasGroup on interactPromptHUD, used to dim it when the Water prompt below is the " +
             "active selection instead — see SetActiveHUDPrompt(). Auto-added if missing.")]
    [SerializeField] private CanvasGroup interactPromptHUDGroup;

    [Tooltip("CanvasGroup on waterPromptHUD, same purpose as interactPromptHUDGroup but for this prompt.")]
    [SerializeField] private CanvasGroup waterPromptHUDGroup;

    [Tooltip("Opacity applied to whichever of interactPromptHUD/waterPromptHUD is NOT the active " +
             "selection (scrolled-to) one.")]
    [Range(0f, 1f)]
    [SerializeField] private float inactiveHUDPromptOpacity = 0.45f;

    [Tooltip("Small 'scroll to switch' hint icon — shown alongside the two HUD prompts whenever " +
             "waterPromptHUD is visible (i.e. whenever scrolling would actually do something), hidden " +
             "the rest of the time. Optional.")]
    [SerializeField] private GameObject promptScrollHint;

    [Header("Pot Selector (HUD)")]
    [Tooltip("Root of the pot-type selector — pops up while in PlacementSystem's Placing mode, " +
             "hidden otherwise. Shows one icon per entry in PlacementSystem.AvailablePots, in the " +
             "same order as potSelectorIcons below.")]
    [SerializeField] private GameObject potSelectorRoot;

    [Tooltip("One Image per pot type, in the SAME order as PlacementSystem's availablePots list. The " +
             "currently-selected pot's icon shows at full opacity; the others dim to " +
             "inactivePotIconOpacity. Assign as many as there are pot types — extra slots beyond " +
             "AvailablePots.Count are just left blank/hidden.")]
    [SerializeField] private List<Image> potSelectorIcons;

    [Tooltip("Opacity applied to every pot icon EXCEPT the currently-selected one.")]
    [Range(0f, 1f)]
    [SerializeField] private float inactivePotIconOpacity = 0.35f;

    [Header("Wall Mushroom Selector (HUD)")]
    [Tooltip("Root of the wall-mushroom selector — pops up while in WallPlacementSystem's Placing " +
             "mode, hidden otherwise. Shows one icon per entry in WallPlacementSystem.AvailableMushrooms, " +
             "in the same order as wallMushroomSelectorIcons below. There's currently only ever one " +
             "wall-mushroom type, so in practice this just shows a single always-active icon while " +
             "placing — built the same way as the (multi-option) pot selector above for consistency, " +
             "and so it needs no rework if more wall types are ever added.")]
    [SerializeField] private GameObject wallMushroomSelectorRoot;

    [Tooltip("One Image per wall-mushroom type, in the SAME order as WallPlacementSystem's " +
             "availableMushrooms list.")]
    [SerializeField] private List<Image> wallMushroomSelectorIcons;

    [Tooltip("Opacity applied to every wall-mushroom icon EXCEPT the currently-selected one.")]
    [Range(0f, 1f)]
    [SerializeField] private float inactiveWallMushroomIconOpacity = 0.35f;

    [Header("Harvest Feedback (HUD)")]
    [Tooltip("Root of the fixed HUD feedback popup (e.g. 'Harvested Sparkmint x1') — replaces the old " +
             "runtime-built floating canvas HarvestNodeContainer used to create itself. Should start " +
             "inactive in the scene; ShowHarvestFeedback below shows it, fills in the text, and hides " +
             "it again after a few seconds.")]
    [SerializeField] private GameObject harvestFeedbackRoot;
    [SerializeField] private TextMeshProUGUI harvestFeedbackText;
    [Tooltip("How long the feedback popup stays visible before auto-hiding.")]
    [SerializeField] private float harvestFeedbackDuration = 2.5f;
    private Coroutine harvestFeedbackRoutine;

    [Header("Miasma Growth")]
    [Tooltip("The deleted UI_Script.cs used to call miasma.flipSize() once on Start(), which is the " +
             "ONLY thing in the project that ever set incSize = true and made the sphere grow. Now " +
             "that UI_Script is gone, nothing does that anymore — this replaces it. Leave on to restore " +
             "the old always-growing-on-scene-start behaviour, or turn off if growth should instead be " +
             "triggered by something else (a game-start event, a button, etc).")]
    [SerializeField] private bool autoStartMiasmaGrowth = true;

    [Header("Debug")]
    [Tooltip("Logs each bar's raw value once a second so you can tell whether the script is actually " +
             "computing new numbers (a data problem) vs. the numbers are right but nothing draws (an " +
             "Image/ImageFillBar setup problem).")]
    [SerializeField] private bool debugLogging = false;
    private float _debugLogTimer = 0f;

    /// <summary>The zone the player is currently standing in, or null if they're in none of them.</summary>
    public ZoneHealth CurrentZone { get; private set; }

    // ---------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------
    private void Awake()
    {
        if (harvestFeedbackRoot != null) harvestFeedbackRoot.SetActive(false);

        if (playerInventory == null)
        {
            // FindObjectOfType just grabs whichever instance it happens to find first — if there's
            // more than one PlayerInventory in the scene (e.g. one accidentally left on a UI prefab
            // alongside the real one on the player), which one "wins" is down to luck, not correctness.
            PlayerInventory[] allInventories = FindObjectsOfType<PlayerInventory>();

            if (allInventories.Length > 1)
                Debug.LogWarning($"[MainUIController] Found {allInventories.Length} PlayerInventory " +
                    $"instances in the scene — auto-picked '{allInventories[0].name}' at " +
                    $"{allInventories[0].transform.position}. If that's not your actual player (e.g. its " +
                    "position looks like screen-space UI coordinates rather than a world position), " +
                    "assign the playerInventory field manually in the Inspector instead of leaving it " +
                    "to auto-find.", this);

            playerInventory = allInventories.Length > 0 ? allInventories[0] : null;
        }

        if (journalButton != null)
            journalButton.onClick.AddListener(() => journalUI?.ToggleJournal());

        if (inventoryButton != null)
            inventoryButton.onClick.AddListener(() => inventoryUI?.ToggleInventory());

        WireToolSlot(0, () => placementSystem.TogglePlaceMode());
        WireToolSlot(1, () => placementSystem.ToggleRemoveMode());
        WireToolSlot(2, () => placementSystem.ToggleMoveMode());
        WireToolSlot(3, () => placementSystem.ToggleWaterMode());

        if (placementSystem != null)
        {
            // OnModeChanged fires no matter whether the mode changed via keybind or via one of the
            // buttons above, so this one subscription keeps the buttons correctly highlighted
            // (or un-highlighted) regardless of which input triggered the change.
            placementSystem.OnModeChanged += RefreshToolButtonHighlights;
            placementSystem.OnModeChanged += OnPlacementSystemModeChanged;
            RefreshToolButtonHighlights(placementSystem.CurrentMode); // sync initial state
        }

        if (abilityPlacementSystem == null)
            abilityPlacementSystem = FindObjectOfType<AbilityPlacementSystem>();

        if (abilityPlacementSystem != null)
            abilityPlacementSystem.OnPlacingChanged += RefreshSecondaryHudVisibility;

        RefreshSecondaryHudVisibility(); // sync initial state

        if (wallPlacementSystem == null)
            wallPlacementSystem = FindObjectOfType<WallPlacementSystem>();

        if (wallPlacementSystem != null)
            wallPlacementSystem.OnModeChanged += RefreshWallPlacementUI;

        RefreshWallPlacementUI(); // sync initial state

        if (hotbarSystem == null)
            hotbarSystem = FindObjectOfType<AbilityHotbarSystem>();

        for (int i = 0; i < hudHotbarSlotUIs.Count; i++)
            hudHotbarSlotUIs[i]?.Initialize(this, i);
    }

    private void OnEnable()
    {
        if (hotbarSystem != null)
        {
            hotbarSystem.OnSlotsChanged += RefreshHotbarUI;
            RefreshHotbarUI(); // sync initial state — assignments made before this enabled shouldn't show empty
        }

        if (NewItemTracker.Instance != null)
            NewItemTracker.Instance.OnChanged += RefreshNewItemDots;
        RefreshNewItemDots(); // sync initial state
    }

    private void OnDisable()
    {
        if (hotbarSystem != null)
            hotbarSystem.OnSlotsChanged -= RefreshHotbarUI;

        if (NewItemTracker.Instance != null)
            NewItemTracker.Instance.OnChanged -= RefreshNewItemDots;
    }

    private void OnDestroy()
    {
        if (placementSystem != null)
        {
            placementSystem.OnModeChanged -= RefreshToolButtonHighlights;
            placementSystem.OnModeChanged -= OnPlacementSystemModeChanged;
        }

        if (abilityPlacementSystem != null)
            abilityPlacementSystem.OnPlacingChanged -= RefreshSecondaryHudVisibility;

        if (wallPlacementSystem != null)
            wallPlacementSystem.OnModeChanged -= RefreshWallPlacementUI;
    }

    // ---------------------------------------------------------------
    // Hotbar (HUD row) — mirrors AbilityHotbarSystem's slot data, same treatment
    // InventoryUIController gives its own preview row. Activation (click or number
    // key) is handled entirely by AbilityHotbarSystem itself; this just displays state
    // and forwards clicks.
    // ---------------------------------------------------------------
    private void RefreshHotbarUI()
    {
        if (hotbarSystem == null) return;
        foreach (var slot in hudHotbarSlotUIs)
            slot?.Refresh(hotbarSystem);
    }

    /// <summary>Called by HotbarSlotUI.OnPointerClick on the HUD row. Unlike InventoryUIController's
    /// version, there's no inventory panel to close afterwards — the HUD is already what's on-screen
    /// during normal play.</summary>
    public void ActivateHotbarSlot(int slotIndex) => hotbarSystem?.ActivateSlot(slotIndex);

    /// <summary>Called by HotbarSlotUI.OnPointerClick on right-click. Just forgets the slot's
    /// assignment — doesn't touch the player's actual item count, so nothing is lost.</summary>
    public void ClearHotbarSlot(int slotIndex) => hotbarSystem?.Clear(slotIndex);

    /// <summary>Adds a click listener to toolSlots[index] if both the slot and placementSystem exist.</summary>
    private void WireToolSlot(int index, UnityEngine.Events.UnityAction onClick)
    {
        if (placementSystem == null) return;
        if (index < 0 || index >= toolSlots.Length || toolSlots[index] == null) return;

        toolSlots[index].onClick.AddListener(onClick);
    }

    /// <summary>Tints each tool slot to show which tool (if any) is currently active.</summary>
    private void RefreshToolButtonHighlights(PlacementSystem.Mode mode)
    {
        SetToolSlotHighlight(0, mode == PlacementSystem.Mode.Placing);
        SetToolSlotHighlight(1, mode == PlacementSystem.Mode.Removing);
        SetToolSlotHighlight(2, mode == PlacementSystem.Mode.Moving);
        SetToolSlotHighlight(3, mode == PlacementSystem.Mode.Watering);

        RefreshPotSelector(mode);
        RefreshPlacementBanner();
    }

    /// <summary>Shows "Floor Placement Mode" while PlacementSystem is Placing, "Wall Placement
    /// Mode" while WallPlacementSystem is Placing, and hides the banner otherwise. Called both from
    /// RefreshToolButtonHighlights (fires on every PlacementSystem.OnModeChanged) and from
    /// RefreshWallPlacementUI below, since the two systems' events aren't shaped the same (one
    /// passes a Mode, the other doesn't) — this method takes no parameters and just re-reads both
    /// systems' current state fresh each time, so either caller can trigger the same result.</summary>
    private void RefreshPlacementBanner()
    {
        if (placementBannerRoot == null) return;

        bool floorPlacing = placementSystem != null && placementSystem.CurrentMode == PlacementSystem.Mode.Placing;
        bool wallPlacing = wallPlacementSystem != null && wallPlacementSystem.CurrentMode == WallPlacementSystem.Mode.Placing;

        placementBannerRoot.SetActive(floorPlacing || wallPlacing);

        if (placementBannerText != null)
            placementBannerText.text = wallPlacing ? wallPlacementBannerText : floorPlacementBannerText;
    }

    /// <summary>Everything that needs to react to WallPlacementSystem.OnModeChanged specifically —
    /// the shared banner (also driven from the floor side, see above) plus the wall-mushroom
    /// selector, which only wall mode changes affect at all.</summary>
    private void RefreshWallPlacementUI()
    {
        RefreshPlacementBanner();
        RefreshWallMushroomSelector();
    }

    /// <summary>Shows/hides and refreshes the wall-mushroom selector — same shape as
    /// RefreshPotSelector below, just for WallPlacementSystem instead of PlacementSystem.</summary>
    private void RefreshWallMushroomSelector()
    {
        bool visible = wallPlacementSystem != null && wallPlacementSystem.CurrentMode == WallPlacementSystem.Mode.Placing;

        if (wallMushroomSelectorRoot != null)
            wallMushroomSelectorRoot.SetActive(visible);

        if (!visible || wallMushroomSelectorIcons == null || wallPlacementSystem == null) return;

        IReadOnlyList<WallMushroomData> mushrooms = wallPlacementSystem.AvailableMushrooms;
        int activeIndex = wallPlacementSystem.SelectedIndex;

        for (int i = 0; i < wallMushroomSelectorIcons.Count; i++)
        {
            Image img = wallMushroomSelectorIcons[i];
            if (img == null) continue;

            WallMushroomData data = mushrooms != null && i < mushrooms.Count ? mushrooms[i] : null;
            img.sprite = data != null ? data.icon : null;
            img.enabled = data != null && data.icon != null;

            Color c = img.color;
            c.a = i == activeIndex ? 1f : inactiveWallMushroomIconOpacity;
            img.color = c;
        }
    }

    /// <summary>Shows/hides and refreshes the pot-type selector. Called from
    /// RefreshToolButtonHighlights, which already fires on every PlacementSystem.OnModeChanged —
    /// including every single scroll-cycle step while in Placing mode (EnterPlaceMode fires the
    /// event on each call, not just the first), so this stays in sync with scrolling for free.</summary>
    private void RefreshPotSelector(PlacementSystem.Mode mode)
    {
        bool visible = mode == PlacementSystem.Mode.Placing;

        if (potSelectorRoot != null)
            potSelectorRoot.SetActive(visible);

        if (!visible || potSelectorIcons == null || placementSystem == null) return;

        IReadOnlyList<PotData> pots = placementSystem.AvailablePots;
        int activeIndex = placementSystem.SelectedPotIndex;

        for (int i = 0; i < potSelectorIcons.Count; i++)
        {
            Image img = potSelectorIcons[i];
            if (img == null) continue;

            PotData data = pots != null && i < pots.Count ? pots[i] : null;
            img.sprite = data != null ? data.icon : null;
            img.enabled = data != null && data.icon != null;

            Color c = img.color;
            c.a = i == activeIndex ? 1f : inactivePotIconOpacity;
            img.color = c;
        }
    }

    private void SetToolSlotHighlight(int index, bool active)
    {
        if (index < 0 || index >= toolSlots.Length || toolSlots[index] == null) return;
        if (toolSlots[index].targetGraphic == null) return;

        toolSlots[index].targetGraphic.color = active ? toolActiveColor : toolInactiveColor;
    }

    private void Start()
    {
        // Restores the growth-start call that used to live in UI_Script.Start(). See the
        // autoStartMiasmaGrowth tooltip above for why this is here at all.
        //
        // NOTE: uses SetGrowing(true) rather than flipSize(). flipSize() is a toggle, so if
        // MiasmaController.growOnStart is ALSO true, the two Start() calls fire in some
        // (unpredictable) order and cancel each other out, leaving growth OFF and the miasma
        // bar frozen. SetGrowing(true) is idempotent — it's safe no matter which Start() runs
        // first or whether growOnStart is left on.
        if (autoStartMiasmaGrowth && miasma != null)
            miasma.SetGrowing(true);
    }

    private void Update()
    {
        RefreshWaterBar();
        RefreshMiasmaBar();
        RefreshZoneHappinessBar();

        if (debugLogging) RunDebugLog();
    }

    private void RunDebugLog()
    {
        _debugLogTimer += Time.deltaTime;
        if (_debugLogTimer < 1f) return;
        _debugLogTimer = 0f;

        Debug.Log($"[MainUIController] water={(playerInventory != null ? playerInventory.getWaterPool().ToString("F1") : "no playerInventory")} " +
                  $"| miasma={(miasma != null ? $"{miasma.CurrentSize:F1}/{miasma.MaxSize:F1}" : "no miasma ref")} " +
                  $"| zone={(CurrentZone != null ? $"{CurrentZone.zoneName}={CurrentZone.ZoneHappiness:F1}" : "player in no zone")} " +
                  $"| waterBar={(waterBar != null)} miasmaBar={(miasmaBar != null)} zoneHappinessBar={(zoneHappinessBar != null)} playerZoneTracker={(playerZoneTracker != null)}");
    }

    // ---------------------------------------------------------------
    // HUD visibility — called by JournalUIController / InventoryUIController / ExitMenuController
    // whenever they open or close
    // ---------------------------------------------------------------
    // Same reference-counting pattern as the interact prompt below: tracks which open menus
    // currently want the HUD hidden, rather than a single last-caller-wins bool. In practice only
    // one of Journal/Inventory/ExitMenu is ever open at a time (see ExitMenuController's Escape
    // handling), but this stays correct even if that ever changes — the HUD only comes back once
    // NONE of them want it hidden anymore.
    private readonly HashSet<object> _hudHideRequesters = new HashSet<object>();

    /// <summary>Hides/shows hudRoot. Pass the calling component as <paramref name="requester"/> so
    /// multiple menus sharing this one HUD don't show it out from under each other.</summary>
    public void SetHudHidden(bool hidden, object requester)
    {
        if (requester == null) return;

        if (hidden)
            _hudHideRequesters.Add(requester);
        else
            _hudHideRequesters.Remove(requester);

        if (hudRoot != null)
            hudRoot.SetActive(_hudHideRequesters.Count == 0);
    }

    // ---------------------------------------------------------------
    // Secondary HUD visibility — hides everything except hotbar/tool selector while placing,
    // moving, or removing a pot (PlacementSystem) or an ability placeable (AbilityPlacementSystem).
    // ---------------------------------------------------------------
    // PlacementSystem.OnModeChanged passes its Mode — ignored here, this only cares THAT it
    // changed, not to what — kept as a separate named method (rather than a lambda) so OnDestroy
    // can unsubscribe it the same way RefreshToolButtonHighlights already does above.
    private void OnPlacementSystemModeChanged(PlacementSystem.Mode mode) => RefreshSecondaryHudVisibility();

    private void RefreshSecondaryHudVisibility()
    {
        if (secondaryHudRoot == null) return;

        bool placingActive =
            (placementSystem != null && placementSystem.IsPlacementModeActive) ||
            (abilityPlacementSystem != null && abilityPlacementSystem.IsActive);

        secondaryHudRoot.SetActive(!placingActive);
    }

    // ---------------------------------------------------------------
    // New-item HUD dots — see NewItemTracker.cs. Journal/inventory icon click already opens
    // their respective panels via journalUI/inventoryUI above; the dots here are purely visual
    // and don't need their own click handling.
    // ---------------------------------------------------------------
    private void RefreshNewItemDots()
    {
        if (journalNewDot != null)
            journalNewDot.SetActive(NewItemTracker.Instance != null && NewItemTracker.Instance.HasAnyUnseenJournal);

        if (inventoryNewDot != null)
            inventoryNewDot.SetActive(NewItemTracker.Instance != null && NewItemTracker.Instance.HasAnyUnseenInventory);
    }

    // ---------------------------------------------------------------
    // Harvest feedback (HUD) — replaces HarvestNodeContainer's old runtime-built floating canvas.
    // ---------------------------------------------------------------
    /// <summary>Shows a short-lived message on the HUD (e.g. "Harvested Sparkmint x1"), auto-hiding
    /// after harvestFeedbackDuration. Safe to call again while already showing — restarts the timer
    /// with the new message rather than stacking/queuing multiple popups.</summary>
    public void ShowHarvestFeedback(string message)
    {
        if (harvestFeedbackRoot == null) return;

        if (harvestFeedbackText != null)
            harvestFeedbackText.text = message;

        harvestFeedbackRoot.SetActive(true);

        if (harvestFeedbackRoutine != null) StopCoroutine(harvestFeedbackRoutine);
        harvestFeedbackRoutine = StartCoroutine(HideHarvestFeedbackAfterDelay());
    }

    private System.Collections.IEnumerator HideHarvestFeedbackAfterDelay()
    {
        yield return new WaitForSeconds(harvestFeedbackDuration);
        if (harvestFeedbackRoot != null) harvestFeedbackRoot.SetActive(false);
        harvestFeedbackRoutine = null;
    }

    // ---------------------------------------------------------------
    // Interact prompt (HUD) — called by PotInteraction / HarvestNodeContainer (or anything else)
    // when their useWorldSpacePrompt toggle is OFF
    // ---------------------------------------------------------------
    // Tracks which callers currently want the prompt visible. Needed because multiple scripts
    // (PotInteraction, HarvestNodeContainer, etc.) share this one HUD element and each calls
    // SetInteractPromptVisible(...) every frame regardless of what the others are doing. A plain
    // SetActive(visible) meant whichever script's Update() happened to run last in a frame always
    // won, so e.g. HarvestNodeContainer calling "hide" every frame (no node nearby) could stomp
    // PotInteraction calling "show" (pot nearby) right after, even though a prompt SHOULD be
    // showing. Now the HUD stays visible as long as at least one caller wants it visible.
    private readonly HashSet<object> _interactPromptRequesters = new HashSet<object>();

    /// <summary>Shows/hides the fixed HUD interact-prompt element. Pass the calling component as
    /// <paramref name="requester"/> (e.g. "this") so multiple sources sharing this one HUD element
    /// don't hide it out from under each other - the element stays visible as long as ANY
    /// requester currently wants it visible.</summary>
    public void SetInteractPromptVisible(bool visible, object requester)
    {
        if (requester == null) return;

        if (visible)
            _interactPromptRequesters.Add(requester);
        else
            _interactPromptRequesters.Remove(requester);

        if (interactPromptHUD != null)
            interactPromptHUD.SetActive(_interactPromptRequesters.Count > 0);
    }

    /// <summary>Shows/hides the Water Plant HUD prompt, and the "scroll to switch" hint alongside
    /// it — they always share the same visibility condition (there's nothing to scroll BETWEEN
    /// unless this second prompt exists), so one method covers both. Only PotInteraction calls
    /// this — see the waterPromptHUD tooltip for why it doesn't need interactPromptHUD's
    /// requester-tracking.</summary>
    public void SetWaterPromptVisible(bool visible)
    {
        if (waterPromptHUD != null)
            waterPromptHUD.SetActive(visible);

        if (promptScrollHint != null)
            promptScrollHint.SetActive(visible);
    }

    /// <summary>Sets which HUD prompt is the active/highlighted one (full opacity, E icon shown),
    /// dimming the other to inactiveHUDPromptOpacity AND hiding its E icon entirely. Call whenever
    /// the selection OR either prompt's visibility changes — safe even if one or both prompts are
    /// currently hidden.</summary>
    public void SetActiveHUDPrompt(bool waterIsActive)
    {
        EnsureHUDPromptGroups();

        if (interactPromptHUDGroup != null)
            interactPromptHUDGroup.alpha = waterIsActive ? inactiveHUDPromptOpacity : 1f;

        if (waterPromptHUDGroup != null)
            waterPromptHUDGroup.alpha = waterIsActive ? 1f : inactiveHUDPromptOpacity;

        if (interactPromptEIcon != null)
            interactPromptEIcon.enabled = !waterIsActive;

        if (waterPromptEIcon != null)
            waterPromptEIcon.enabled = waterIsActive;
    }

    private void EnsureHUDPromptGroups()
    {
        if (interactPromptHUD != null && interactPromptHUDGroup == null)
        {
            interactPromptHUDGroup = interactPromptHUD.GetComponent<CanvasGroup>();
            if (interactPromptHUDGroup == null)
                interactPromptHUDGroup = interactPromptHUD.AddComponent<CanvasGroup>();
        }

        if (waterPromptHUD != null && waterPromptHUDGroup == null)
        {
            waterPromptHUDGroup = waterPromptHUD.GetComponent<CanvasGroup>();
            if (waterPromptHUDGroup == null)
                waterPromptHUDGroup = waterPromptHUD.AddComponent<CanvasGroup>();
        }
    }

    // ---------------------------------------------------------------
    // Water bar — mirrors PotMenuUIController.RefreshMainStatusBars()
    // ---------------------------------------------------------------
    private void RefreshWaterBar()
    {
        if (waterBar == null || playerInventory == null) return;
        waterBar.SetValue(playerInventory.getWaterPool(), playerInventory.getMaxWaterPool());
    }

    // ---------------------------------------------------------------
    // Miasma bar — normalized current size against MiasmaController's max
    // ---------------------------------------------------------------
    private void RefreshMiasmaBar()
    {
        if (miasmaBar == null || miasma == null) return;

        float normalized = miasma.MaxSize > 0f ? miasma.CurrentSize / miasma.MaxSize : 0f;
        miasmaBar.SetNormalized(normalized);
    }

    // ---------------------------------------------------------------
    // Zone happiness bar — follows whichever zone the player is in
    // ---------------------------------------------------------------
    private void RefreshZoneHappinessBar()
    {
        if (zoneHappinessBar == null || playerZoneTracker == null) return;

        // Only update CurrentZone when the tracker actually reports one — if the player is
        // briefly outside every zone (e.g. walking/flying through a gap between them), keep
        // showing the last zone's score instead of the bar snapping to 0 every time.
        if (playerZoneTracker.CurrentZone != null)
            CurrentZone = playerZoneTracker.CurrentZone;

        if (CurrentZone != null)
            zoneHappinessBar.SetNormalized(CurrentZone.ZoneHappiness / 100f);
    }
}
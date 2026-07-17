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
//      Place, 1 = Remove, 2 = Move, 3 = still reserved) and assign the
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

using UnityEngine;
using UnityEngine.UI;

public class MainUIController : MonoBehaviour
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

    [Header("Inventory Icon")]
    [SerializeField] private Button inventoryButton;
    [SerializeField] private InventoryUIController inventoryUI;

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
        // toolSlots[3] intentionally left unwired — still reserved.

        if (placementSystem != null)
        {
            // OnModeChanged fires no matter whether the mode changed via keybind or via one of the
            // buttons above, so this one subscription keeps the buttons correctly highlighted
            // (or un-highlighted) regardless of which input triggered the change.
            placementSystem.OnModeChanged += RefreshToolButtonHighlights;
            RefreshToolButtonHighlights(placementSystem.CurrentMode); // sync initial state
        }
    }

    private void OnDestroy()
    {
        if (placementSystem != null)
            placementSystem.OnModeChanged -= RefreshToolButtonHighlights;
    }

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
        if (autoStartMiasmaGrowth && miasma != null)
            miasma.flipSize();
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
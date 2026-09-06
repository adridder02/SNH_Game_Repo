using UnityEngine;

// =============================================================
// PlantSpeciesData.cs
// -------------------------------------------------------------
// One asset per plant SPECIES (not per owned instance) — this is
// the reference data shown on the journal's right-hand page.
// Distinct from InventoryItemInstance, which represents one owned
// plant sitting in the grid/Available; a species can exist here
// whether or not the player currently owns one.
//
// SETUP:
//   Right-click in the Project window ->
//   Create > Plants > Plant Species Data
//   Then link it to the matching plant prefab via
//   PlantState.journalSpecies (see PlantState.cs), and add it to
//   your PlantJournalDatabase asset's list.
// =============================================================
[CreateAssetMenu(fileName = "PlantSpecies_", menuName = "Plants/Plant Species Data")]
public class PlantSpeciesData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique key used to save discovery progress. Leave blank to just use the " +
             "asset's file name. IMPORTANT: once players have save data, don't rename this " +
             "(or the asset, if left blank) — it'll look undiscovered again.")]
    [SerializeField] private string speciesId;

    public string displayName = "Plant";

    [Tooltip("Which journal section this appears under (Sunny/Dark/Water row). Reuses the same " +
             "PlantType enum as the inventory's filter bar, so a species' category here matches " +
             "the filter it shows up under in the inventory too.")]
    public PlantType category = PlantType.Sunny;

    [Tooltip("Which progression-page room this species' icon appears in (Main Hall/East/West). " +
             "Unrelated to category above — a room can mix species from any category.")]
    public RoomType room = RoomType.Main;

    public int tier = 1;

    [Tooltip("Filled dots shown on the detail page, out of 4.")]
    [Range(0, 4)] public int difficulty = 1;

    [Header("Journal Page")]
    [TextArea(3, 8)]
    [Tooltip("First description block on the detail page.")]
    public string description;

    [TextArea(3, 8)]
    [Tooltip("Second description block on the detail page — e.g. a care/behavior note shown below the " +
             "main description. Optional; leave blank if this species doesn't need a second section.")]
    public string descriptionSecondary;

    [Tooltip("Small thumbnail shown in the journal grid once discovered.")]
    public Sprite journalIcon;

    [Tooltip("Larger illustration shown on the detail page. Falls back to journalIcon if left blank.")]
    public Sprite journalImage;

    [Header("Progression Icons")]
    [Tooltip("Shown on the Progress page while this species is undiscovered.")]
    public Sprite disabledIcon;

    [Tooltip("Shown on the Progress page once this species has been discovered.")]
    public Sprite activeIcon;

    [Tooltip("Reserved for the progression system (not wired up yet) — shown once this species " +
             "hits its 'gold' milestone. Just needs an asset assigned for now; the logic that " +
             "switches to it comes later.")]
    public Sprite goldIcon;

    [Header("Care Reference (display only)")]
    [Tooltip("These describe the species for the reference page — they're not read by the live " +
             "growing simulation (that's PlantState's thresholds). Keep them in sync by hand if " +
             "you want the journal text to match actual gameplay difficulty.")]
    public RequirementLevel waterRequirement = RequirementLevel.Medium;
    public RequirementLevel lightRequirement = RequirementLevel.Medium;
    public SoilKind preferredSoilDisplay = SoilKind.Loam;

    [Header("Special Unlock (optional)")]
    [Tooltip("If true, this species can't be planted at all until its room (above) is fully " +
             "discovered/completed in the Journal AND that room's zone happiness (see " +
             "RoomZoneRegistry) clears happinessThreshold below. Used for rare late-game plants — " +
             "e.g. the tree's three crystal-heart species. Leave off for every normal plant.")]
    public bool requiresRoomUnlock = false;

    [Tooltip("Minimum ZoneHappiness (0-100) this species' room's zone must have, on top of full " +
             "room completion, before it's plantable. Only used when requiresRoomUnlock is true.")]
    [Range(0f, 100f)] public float happinessThreshold = 70f;

    /// <summary>The key actually used for save data — falls back to the asset name if speciesId is blank.</summary>
    public string ResolvedId => string.IsNullOrEmpty(speciesId) ? name : speciesId;
}

public enum RequirementLevel
{
    Low,
    Medium,
    High
}

// Order here doubles as the Progress page's flip order: Main -> East -> West.
// ProgressPageUIController.RoomOrder is what actually drives the paging, but
// keep this declaration order matching it for sanity's sake.
public enum RoomType
{
    Main,
    East,
    West
}
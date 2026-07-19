using UnityEngine;

// =============================================================
// CollectablePlant.cs
// -------------------------------------------------------------
// DATABASE-FIRST ICONS/IMAGES/NAME:
// plantPrefab's PlantState.journalSpecies (the same PlantSpeciesData
// asset that drives the Journal — see PlantJournalDatabase.cs) is now
// the preferred source for the icon, detail image, and display name
// that get copied onto InventoryItemInstance when this is picked up.
// This keeps the Journal and the inventory/pot UIs showing the exact
// same art/name for a given species, with nothing to duplicate or
// get out of sync by hand.
//
// plantName / plantIcon / plantImage below are now only a FALLBACK,
// used when the prefab has no PlantState.journalSpecies assigned
// (e.g. a plant that intentionally isn't in the journal). Leave them
// set for safety, but once a species asset is linked on the prefab,
// these fields are ignored.
// =============================================================
public class CollectablePlant : MonoBehaviour
{
    [Header("Plant Settings")]
    [Tooltip("REQUIRED: the actual plant prefab asset to add to the player's inventory. " +
             "This must be a prefab from your Project window (the same kind used when " +
             "planting into pots), NOT this scene object itself.")]
    [SerializeField] private GameObject plantPrefab;

    [Header("Fallback Display (used only if the prefab has no PlantState.journalSpecies)")]
    [Tooltip("Fallback name. Ignored once the prefab's PlantState.journalSpecies is assigned — " +
             "that asset's displayName wins instead.")]
    [SerializeField] private string plantName = "Plant";
    [Tooltip("Fallback small icon shown in grid/available slots. Ignored once the prefab's " +
             "PlantState.journalSpecies is assigned — that asset's journalIcon wins instead.")]
    [SerializeField] private Sprite plantIcon;
    [Tooltip("Fallback larger reference image shown in the Plant detail panel. Ignored once the " +
             "prefab's PlantState.journalSpecies is assigned — that asset's journalImage wins instead. " +
             "This is intentionally separate from Plant Icon — the icon is the small slot thumbnail, " +
             "this is the bigger 'info card' image.")]
    [SerializeField] private Sprite plantImage;

    public GameObject GetPlantPrefab()
    {
        if (plantPrefab == null)
        {
            Debug.LogError($"[CollectablePlant] '{gameObject.name}' has no Plant Prefab assigned! " +
                            "Drag the plant's prefab asset into the Plant Prefab field in the Inspector.", this);
            return null;
        }
        Tutorial.removedPlant();
        return plantPrefab;
    }

    /// <summary>The species asset linked on the prefab (PlantState.journalSpecies), if any — the same
    /// asset used by PlantJournalDatabase/JournalUIController. Null for plants not wired into the journal.</summary>
    private PlantSpeciesData ResolveSpecies()
    {
        if (plantPrefab == null) return null;
        PlantState state = plantPrefab.GetComponentInChildren<PlantState>();
        return state != null ? state.journalSpecies : null;
    }

    public string GetPlantName()
    {
        PlantSpeciesData species = ResolveSpecies();
        if (species != null && !string.IsNullOrEmpty(species.displayName))
            return species.displayName;
        return plantName;
    }

    public Sprite GetPlantIcon()
    {
        PlantSpeciesData species = ResolveSpecies();
        if (species != null && species.journalIcon != null)
            return species.journalIcon;
        return plantIcon;
    }

    /// <summary>The larger detail-panel image. Prefers the species' journalImage (falling back to its
    /// journalIcon), then the local fallback fields, so the panel is never blank.</summary>
    public Sprite GetPlantImage()
    {
        PlantSpeciesData species = ResolveSpecies();
        if (species != null)
        {
            if (species.journalImage != null) return species.journalImage;
            if (species.journalIcon != null) return species.journalIcon;
        }
        return plantImage != null ? plantImage : plantIcon;
    }
}
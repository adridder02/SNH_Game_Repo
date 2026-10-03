using UnityEngine;

// =============================================================
// PlantHarvestYield.cs
// -------------------------------------------------------------
// Attach to the SAME GameObject as PlantState on any plant prefab
// that should hand over an ability item when harvested (see
// PotContents.HarvestPlant()). Deliberately a separate component
// rather than new fields bolted onto PlantState — most plants
// (Sundial Cactus, Windmill Aster, Melodybloom, Sproionshroom,
// Drifter) have a LIVING ability instead and never get harvested
// for an item at all, so they simply don't get this component.
//
// If left off a prefab entirely, HarvestPlant() falls back to the
// old dummy behaviour (plant returns to the plant inventory as-is).
// =============================================================
public class PlantHarvestYield : MonoBehaviour
{
    [Tooltip("The ability item granted when this plant is harvested. Leave null to fall back to the old " +
             "'return plant to inventory' behaviour.")]
    public AbilityItemData yieldItem;

    [Tooltip("Overrides yieldItem.harvestGrantAmount if > 0. Leave at 0 to just use the asset's own amount " +
             "(e.g. Sparkmint's asset already says 10 leaves — no override needed).")]
    public int amountOverride = 0;

    public int ResolveAmount() =>
        amountOverride > 0 ? amountOverride : (yieldItem != null ? Mathf.Max(1, yieldItem.harvestGrantAmount) : 0);
}

using UnityEngine;

// =============================================================
// PotData.cs
// -------------------------------------------------------------
// ScriptableObject that defines a pot type available in the
// placement system.
//
// Create via:
// Assets -> Create -> Greenhouse -> Pot Data
//
// Each PotData describes:
//   - Pot display name.
//   - The prefab to place in the world.
//   - Optional semi-transparent preview prefab.
//   - How many grid cells the pot occupies (derived - see below).
//   - Default soil kind when placed.
//
// SIZE - SINGLE SOURCE OF TRUTH:
//   `size` is no longer set by hand. It's computed from
//   `correspondingPlantSize` via PlantSizeRegistry. Want this pot
//   to be a 4x4? Don't touch this asset - go to the PlantSizeRegistry
//   asset and set the Large entry's footprint to (4,4). That change
//   applies to every Large pot AND every Large plant (inventory
//   footprint included) at once.
// =============================================================

[CreateAssetMenu(fileName = "NewPotData", menuName = "Greenhouse/Pot Data")]
public class PotData : ScriptableObject
{
    [Header("Pot Info")]
    [Tooltip("Name shown in the placement HUD / UI.")]
    public string potName = "Pot";

    [Header("Grid")]
    [Tooltip("The PlantSize category this pot belongs to. This is the ONLY size knob you set - the actual " +
             "footprint (below) is looked up from the PlantSizeRegistry asset, so every pot and plant sharing " +
             "this size stays in lockstep. To change what 'Large' (etc.) actually measures, edit the " +
             "PlantSizeRegistry asset, not this field.")]
    public PlantSize correspondingPlantSize = PlantSize.Medium;

    /// <summary>
    /// How many grid cells this pot occupies - derived from correspondingPlantSize via
    /// PlantSizeRegistry. Read-only by design: change the registry's entry for this size
    /// if you want a different footprint, don't override it per-pot.
    /// </summary>
    public Vector2Int size => PlantSizeRegistry.GetFootprint(correspondingPlantSize);

    [Header("Prefabs")]
    [Tooltip("The actual pot GameObject placed in the scene.")]
    public GameObject potPrefab;

    [Tooltip("Optional ghost/preview prefab shown while hovering before placement. Uses potPrefab if empty.")]
    public GameObject previewPrefab;

    [Header("Soil")]
    [Tooltip("The soil type pre-loaded into the pot when first placed.")]
    public SoilKind defaultSoil = SoilKind.Loam;
}
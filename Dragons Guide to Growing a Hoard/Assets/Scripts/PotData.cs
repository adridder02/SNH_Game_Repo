using UnityEngine;

// =============================================================
// PotData.cs
// -------------------------------------------------------------
// ScriptableObject that defines a pot type available in the
// placement system.
//
// Create via:
// Assets → Create → Greenhouse → Pot Data
//
// Each PotData describes:
//   • Pot display name.
//   • The prefab to place in the world.
//   • Optional semi-transparent preview prefab.
//   • How many grid cells the pot occupies.
//   • Default soil kind when placed.
// =============================================================

[CreateAssetMenu(fileName = "NewPotData", menuName = "Greenhouse/Pot Data")]
public class PotData : ScriptableObject
{
    [Header("Pot Info")]
    [Tooltip("Name shown in the placement HUD / UI.")]
    public string potName = "Pot";

    [Header("Grid")]
    [Tooltip("How many grid cells this pot occupies. Examples: (1,1), (2,1), (2,2).")]
    public Vector2Int size = Vector2Int.one;

    [Header("Prefabs")]
    [Tooltip("The actual pot GameObject placed in the scene.")]
    public GameObject potPrefab;

    [Tooltip("Optional ghost/preview prefab shown while hovering before placement. Uses potPrefab if empty.")]
    public GameObject previewPrefab;

    [Header("Soil")]
    [Tooltip("The soil type pre-loaded into the pot when first placed.")]
    public SoilKind defaultSoil = SoilKind.Loam;
}
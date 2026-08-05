using UnityEngine;

// =============================================================
// WallMushroomData.cs
// -------------------------------------------------------------
// One asset per Clovenwick variant (small wall-crawlers vs the big
// weight-bearing ones). Mirrors PotData's role for WallPlacementSystem
// the same way PotData does for PlacementSystem — deliberately its
// own asset type rather than folded into AbilityItemData, since these
// aren't harvested-ability items (per the brief: "doesn't have
// abilities per say") — they're a straightforward placement catalogue,
// same category as pots.
//
// SETUP: Assets → Create → Greenhouse → Wall Mushroom
// =============================================================
[CreateAssetMenu(fileName = "WallMushroom", menuName = "Greenhouse/Wall Mushroom")]
public class WallMushroomData : ScriptableObject
{
    public string displayName;
    public Vector2Int size = Vector2Int.one;
    public GameObject prefab;
    public GameObject previewPrefab;

    [Tooltip("Roughly how much weight this can support standing on it — 'the big ones can hold an " +
             "average basset hound or a puppy St. Bernard'. Not simulated yet; exposed so a future " +
             "creature-weight system has something to check against.")]
    public float maxSupportedWeightKg = 15f;
}

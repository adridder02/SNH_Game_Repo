using UnityEngine;

// =============================================================
// PlantSizeUtility.cs
// -------------------------------------------------------------
// PlantState.plantSize (Small/Medium/Large) is used elsewhere in
// the project purely to check plant <-> pot compatibility. The
// inventory grid needs an actual footprint (width x height in
// cells) for each of those sizes, so that mapping lives here —
// one place to tweak if the footprints ever need rebalancing.
//
//   Small  -> 1x1
//   Medium -> 1x2
//   Large  -> 2x2
// =============================================================
public static class PlantSizeUtility
{
    public static Vector2Int GetFootprint(PlantSize size)
    {
        switch (size)
        {
            case PlantSize.Small:  return new Vector2Int(1, 1);
            case PlantSize.Medium: return new Vector2Int(1, 2);
            case PlantSize.Large:  return new Vector2Int(2, 2);
            default:               return new Vector2Int(1, 1);
        }
    }
}

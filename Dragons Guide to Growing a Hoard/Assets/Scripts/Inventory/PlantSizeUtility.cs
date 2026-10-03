using UnityEngine;

// =============================================================
// PlantSizeUtility.cs
// -------------------------------------------------------------
// Thin convenience wrapper around PlantSizeRegistry, kept around
// so existing callers (InventoryItemInstance, etc.) don't need to
// change. The actual footprint values now live entirely in the
// PlantSizeRegistry asset — see that file to change/add sizes.
// =============================================================
public static class PlantSizeUtility
{
    public static Vector2Int GetFootprint(PlantSize size) => PlantSizeRegistry.GetFootprint(size);
}
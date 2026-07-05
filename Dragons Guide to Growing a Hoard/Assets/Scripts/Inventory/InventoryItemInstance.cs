using System;
using UnityEngine;

// =============================================================
// InventoryItemInstance.cs
// -------------------------------------------------------------
// Represents ONE owned plant in the player's inventory — whether
// it's currently sitting in a grid cell or floating in the
// "Available" overflow panel.
//
// We need per-instance identity (not just the prefab reference)
// because the player can own several copies of the same plant
// prefab at once, each independently placed/removed.
//
// gridX / gridY == -1 means "not placed in the grid" i.e. it's
// in Available.
// =============================================================
[Serializable]
public class InventoryItemInstance
{
    public readonly string instanceId;
    public readonly GameObject plantPrefab;
    public readonly PlantSize size;
    public readonly Vector2Int footprint;

    public int gridX = -1;
    public int gridY = -1;

    public bool IsInGrid => gridX >= 0 && gridY >= 0;

    public InventoryItemInstance(GameObject prefab)
    {
        instanceId = Guid.NewGuid().ToString();
        plantPrefab = prefab;

        PlantState ps = prefab != null ? prefab.GetComponent<PlantState>() : null;
        size = ps != null ? ps.plantSize : PlantSize.Small;
        footprint = PlantSizeUtility.GetFootprint(size);
    }
}

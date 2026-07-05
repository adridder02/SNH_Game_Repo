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

    // The UI icon to show for this item. Plant prefabs are 3D objects meant
    // for planting into pots — they don't carry a SpriteRenderer — so this
    // comes from CollectablePlant.plantIcon (passed in via
    // PlayerInventory.AddPlantToInventory) rather than being derived from
    // the prefab itself. Null if the harvest source never supplied one
    // (e.g. a plant returned from a pot), in which case the UI falls back
    // to whatever InventorySlotUI can find.
    public readonly Sprite icon;

    public int gridX = -1;
    public int gridY = -1;

    public bool IsInGrid => gridX >= 0 && gridY >= 0;

    public InventoryItemInstance(GameObject prefab, Sprite icon = null)
    {
        instanceId = Guid.NewGuid().ToString();
        plantPrefab = prefab;
        this.icon = icon;

        // GetComponentInChildren, not GetComponent: PlantState commonly lives on a child
        // mesh object rather than the prefab root. GetComponent-only would silently miss
        // it and default every such plant to Small, which is why "size unknown" bugs happen.
        PlantState ps = prefab != null ? prefab.GetComponentInChildren<PlantState>() : null;
        if (prefab != null && ps == null)
            Debug.LogWarning($"[InventoryItemInstance] '{prefab.name}' has no PlantState component " +
                              "(checked root + children) — defaulting size to Small. This plant won't " +
                              "match any pot correctly until PlantState is added.");
        size = ps != null ? ps.plantSize : PlantSize.Small;
        footprint = PlantSizeUtility.GetFootprint(size);
    }
}
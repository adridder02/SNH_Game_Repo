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
public class InventoryItemInstance : IGridPlaceable
{
    public readonly string instanceId;
    public readonly GameObject plantPrefab;
    public readonly PlantSize size;
    public readonly Vector2Int footprint;

    // Filter-bar tag (Sunny/Dark/Water/Dead) — read from PlantState, same
    // component/lookup as size. Defaults to Sunny if there's no PlantState,
    // matching the same "missing component" fallback pattern as size below.
    public readonly PlantType plantType;

    // The UI icon to show for this item. Plant prefabs are 3D objects meant
    // for planting into pots — they don't carry a SpriteRenderer — so this
    // comes from CollectablePlant.plantIcon (passed in via
    // PlayerInventory.AddPlantToInventory) rather than being derived from
    // the prefab itself. Null if the harvest source never supplied one
    // (e.g. a plant returned from a pot), in which case the UI falls back
    // to whatever InventorySlotUI can find.
    public readonly Sprite icon;

    // The larger "info card" image shown in the Plant detail panel — deliberately
    // separate from icon (the small slot thumbnail). Comes from
    // CollectablePlant.GetPlantImage() via PlayerInventory.AddPlantToInventory.
    // Null if the harvest source never supplied one.
    public readonly Sprite displayImage;

    // Human-readable name for the detail panel. Prefers the name passed in from
    // CollectablePlant.GetPlantName(); falls back to a cleaned-up prefab name
    // (stripping the "(Clone)" suffix Unity appends) if none was supplied.
    public readonly string displayName;

    public int gridX = -1;
    public int gridY = -1;

    public bool IsInGrid => gridX >= 0 && gridY >= 0;

    // IGridPlaceable — lets InventoryGrid treat a plant exactly like a consumable stack.
    // Deliberately capitalised versions of the existing fields above (case-sensitive, so both
    // can coexist) — every other script keeps using instanceId/footprint/gridX/gridY directly,
    // only InventoryGrid and the drag/drop code talk to items through this interface.
    public string InstanceId => instanceId;
    public Vector2Int Footprint => footprint;
    int IGridPlaceable.GridX { get => gridX; set => gridX = value; }
    int IGridPlaceable.GridY { get => gridY; set => gridY = value; }

    public InventoryItemInstance(GameObject prefab, Sprite icon = null, Sprite displayImage = null, string displayName = null)
    {
        instanceId = Guid.NewGuid().ToString();
        plantPrefab = prefab;
        this.icon = icon;
        this.displayImage = displayImage;
        this.displayName = !string.IsNullOrEmpty(displayName)
            ? displayName
            : (prefab != null ? prefab.name.Replace("(Clone)", "").Trim() : "Unknown");

        // GetComponentInChildren, not GetComponent: PlantState commonly lives on a child
        // mesh object rather than the prefab root. GetComponent-only would silently miss
        // it and default every such plant to Small, which is why "size unknown" bugs happen.
        PlantState ps = prefab != null ? prefab.GetComponentInChildren<PlantState>() : null;
        if (prefab != null && ps == null)
            Debug.LogWarning($"[InventoryItemInstance] '{prefab.name}' has no PlantState component " +
                              "(checked root + children) — defaulting size to Small. This plant won't " +
                              "match any pot correctly until PlantState is added.");
        size = ps != null ? ps.plantSize : PlantSize.Small;
        plantType = ps != null ? ps.plantType : PlantType.Sunny;
        footprint = PlantSizeUtility.GetFootprint(size);
    }
}
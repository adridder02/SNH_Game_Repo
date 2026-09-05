using UnityEngine;

// =============================================================
// IGridPlaceable.cs
// -------------------------------------------------------------
// Anything that can occupy cells in the shared main-inventory grid.
// Right now that's two very different C# classes:
//   - InventoryItemInstance  (a single owned plant)
//   - AbilityItemInstance    (a stack of a Consumable or Placeable
//                             ability item, e.g. Verdant Algae x3)
//
// Implementing this interface is what lets InventoryGrid store BOTH
// kinds of item in the exact same grid, laid out and dragged around
// identically, without InventoryGrid needing to know or care which
// one it's actually holding at any given cell.
// =============================================================
public interface IGridPlaceable
{
    /// <summary>Stable unique id for this specific owned item/stack — used as the grid's occupancy key.</summary>
    string InstanceId { get; }

    /// <summary>How many cells (width x height) this occupies in the grid.</summary>
    Vector2Int Footprint { get; }

    /// <summary>Current grid column, or -1 if not currently placed (i.e. sitting in Available).</summary>
    int GridX { get; set; }

    /// <summary>Current grid row, or -1 if not currently placed (i.e. sitting in Available).</summary>
    int GridY { get; set; }

    /// <summary>True while this item currently occupies a grid cell (GridX/GridY both >= 0).</summary>
    bool IsInGrid { get; }
}

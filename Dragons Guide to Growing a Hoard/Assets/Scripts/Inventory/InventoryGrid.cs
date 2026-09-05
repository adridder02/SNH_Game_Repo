using System.Collections.Generic;
using UnityEngine;

// =============================================================
// InventoryGrid.cs
// -------------------------------------------------------------
// Plain C# grid model (no MonoBehaviour) for the main inventory grid.
// Handles occupancy, placement/removal of variable-footprint items
// (1x1, 1x2, 2x2, ...), and auto-placement for newly picked up items.
//
// Works against the IGridPlaceable interface rather than a concrete
// type, so ONE grid can hold a mix of InventoryItemInstance (plants)
// and AbilityItemInstance (consumable/placeable stacks) side by side —
// see IGridPlaceable.cs.
//
// Cell (0,0) is the top-left cell. An item's footprint occupies
// the rectangle from (gridX, gridY) to (gridX + w - 1, gridY + h - 1).
//
// This class is deliberately UI-agnostic — PlayerInventory owns
// one of these, and the UI controller only ever reads from it /
// asks PlayerInventory to mutate it.
// =============================================================
public class InventoryGrid
{
    public int Width { get; private set; }
    public int Height { get; private set; }

    // occupancy[x, y] holds the instanceId occupying that cell, or null if empty.
    private string[,] occupancy;
    private readonly Dictionary<string, IGridPlaceable> placedItems = new Dictionary<string, IGridPlaceable>();

    public IReadOnlyDictionary<string, IGridPlaceable> PlacedItems => placedItems;

    public InventoryGrid(int width, int height)
    {
        Resize(width, height);
    }

    /// <summary>
    /// Grows (or shrinks) the grid, preserving existing items that still fit.
    /// Used for the future grid-expansion consumable.
    /// </summary>
    public void Resize(int newWidth, int newHeight)
    {
        string[,] newOccupancy = new string[newWidth, newHeight];

        if (occupancy != null)
        {
            int copyW = Mathf.Min(Width, newWidth);
            int copyH = Mathf.Min(Height, newHeight);
            for (int x = 0; x < copyW; x++)
                for (int y = 0; y < copyH; y++)
                    newOccupancy[x, y] = occupancy[x, y];
        }

        occupancy = newOccupancy;
        Width = newWidth;
        Height = newHeight;
    }

    /// <summary>
    /// Can this footprint be placed with its origin at (originX, originY)?
    /// ignoreInstanceId lets an item check against its own currently-occupied
    /// cells when being moved (so it doesn't collide with itself).
    /// </summary>
    public bool CanPlaceAt(int originX, int originY, Vector2Int footprint, string ignoreInstanceId = null)
    {
        if (originX < 0 || originY < 0) return false;
        if (originX + footprint.x > Width || originY + footprint.y > Height) return false;

        for (int x = originX; x < originX + footprint.x; x++)
        {
            for (int y = originY; y < originY + footprint.y; y++)
            {
                string occupant = occupancy[x, y];
                if (occupant != null && occupant != ignoreInstanceId)
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Places (or moves) an item so its origin is at (originX, originY).
    /// Returns false and does nothing if the spot is invalid.
    /// </summary>
    public bool PlaceAt(IGridPlaceable item, int originX, int originY)
    {
        if (item == null) return false;
        if (!CanPlaceAt(originX, originY, item.Footprint, item.InstanceId))
            return false;

        // If this item was already placed somewhere else in the grid, free those cells first.
        ClearCellsById(item.InstanceId);

        for (int x = originX; x < originX + item.Footprint.x; x++)
            for (int y = originY; y < originY + item.Footprint.y; y++)
                occupancy[x, y] = item.InstanceId;

        item.GridX = originX;
        item.GridY = originY;
        placedItems[item.InstanceId] = item;
        return true;
    }

    /// <summary>
    /// Scans left-to-right, top-to-bottom for the first spot this item's
    /// footprint fits, and places it there. Used the first time a plant is
    /// harvested or a consumable/placeable stack is picked up. Returns false
    /// if no space is left (caller should then route the item to "Available").
    /// </summary>
    public bool TryAutoPlace(IGridPlaceable item)
    {
        if (item == null) return false;

        for (int y = 0; y <= Height - item.Footprint.y; y++)
        {
            for (int x = 0; x <= Width - item.Footprint.x; x++)
            {
                if (CanPlaceAt(x, y, item.Footprint))
                {
                    PlaceAt(item, x, y);
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>Frees this item's cells and removes it from the grid (it still exists as an item — caller decides where it goes next).</summary>
    public void RemoveItem(IGridPlaceable item)
    {
        if (item == null) return;
        ClearCellsById(item.InstanceId);
        placedItems.Remove(item.InstanceId);
        item.GridX = -1;
        item.GridY = -1;
    }

    /// <summary>
    /// Clears a placed item's cells purely by id, without needing the live object reference.
    /// Used when reconciling a consumable/placeable stack that PlayerAbilityInventory has
    /// already fully removed (its count hit 0) — there's no object left to call RemoveItem()
    /// with at that point, just the id it used to be tracked under. Safe to call with an id
    /// that isn't currently placed (no-op).
    /// </summary>
    public void RemoveById(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return;
        ClearCellsById(instanceId);
        placedItems.Remove(instanceId);
    }

    public IGridPlaceable GetItemAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return null;
        string id = occupancy[x, y];
        if (id == null) return null;
        placedItems.TryGetValue(id, out var item);
        return item;
    }

    private void ClearCellsById(string instanceId)
    {
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (occupancy[x, y] == instanceId)
                    occupancy[x, y] = null;
    }
}

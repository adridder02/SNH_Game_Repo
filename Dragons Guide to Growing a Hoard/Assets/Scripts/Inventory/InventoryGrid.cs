using System.Collections.Generic;
using UnityEngine;

// =============================================================
// InventoryGrid.cs
// -------------------------------------------------------------
// Plain C# grid model (no MonoBehaviour) for the inventory grid.
// Handles occupancy, placement/removal of variable-footprint
// items (1x1, 1x2, 2x2, ...), and auto-placement for harvested
// or returned plants.
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
    private readonly Dictionary<string, InventoryItemInstance> placedItems = new Dictionary<string, InventoryItemInstance>();

    public IReadOnlyDictionary<string, InventoryItemInstance> PlacedItems => placedItems;

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
    public bool PlaceAt(InventoryItemInstance item, int originX, int originY)
    {
        if (!CanPlaceAt(originX, originY, item.footprint, item.instanceId))
            return false;

        // If this item was already placed somewhere else in the grid, free those cells first.
        ClearCells(item);

        for (int x = originX; x < originX + item.footprint.x; x++)
            for (int y = originY; y < originY + item.footprint.y; y++)
                occupancy[x, y] = item.instanceId;

        item.gridX = originX;
        item.gridY = originY;
        placedItems[item.instanceId] = item;
        return true;
    }

    /// <summary>
    /// Scans left-to-right, top-to-bottom for the first spot this item's
    /// footprint fits, and places it there. Used by harvesting and by
    /// returning a plant from a pot. Returns false if no space is left
    /// (caller should then route the item to "Available").
    /// </summary>
    public bool TryAutoPlace(InventoryItemInstance item)
    {
        for (int y = 0; y <= Height - item.footprint.y; y++)
        {
            for (int x = 0; x <= Width - item.footprint.x; x++)
            {
                if (CanPlaceAt(x, y, item.footprint))
                {
                    PlaceAt(item, x, y);
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>Frees this item's cells and removes it from the grid (it still exists as an item — caller decides where it goes next).</summary>
    public void RemoveItem(InventoryItemInstance item)
    {
        ClearCells(item);
        placedItems.Remove(item.instanceId);
        item.gridX = -1;
        item.gridY = -1;
    }

    public InventoryItemInstance GetItemAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return null;
        string id = occupancy[x, y];
        if (id == null) return null;
        placedItems.TryGetValue(id, out var item);
        return item;
    }

    private void ClearCells(InventoryItemInstance item)
    {
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (occupancy[x, y] == item.instanceId)
                    occupancy[x, y] = null;
    }
}

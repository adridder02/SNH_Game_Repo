using System;
using System.Collections.Generic;
using UnityEngine;

public class GridData
{
    // maps every occupied cell to the placement it belongs to
    private Dictionary<Vector3Int, PlacementData> occupiedCells = new();

    // returns true if all cells required by size starting at origin are free
    //public String GridPlacement = new string[10];
    public bool CanPlace(Vector3Int origin, Vector2Int size)
    {
        foreach (Vector3Int cell in GetCells(origin, size))
        {
            if (occupiedCells.ContainsKey(cell))
                return false;
        }
        return true;
    }

    /// marks all cells for the placement as occupied
    public void AddPlacement(Vector3Int origin, Vector2Int size, GameObject placedObject)
    {
        PlacementData data = new PlacementData(origin, size, placedObject);
        // going to use ocupide here to check the corordinets of aproximate barries.
        
        foreach (Vector3Int cell in GetCells(origin, size))
            occupiedCells[cell] = data;
    }

    // removes a placement given any cell it occupies
    // returns the GameObject to destroy
    public GameObject RemovePlacement(Vector3Int cell)
    {
        if (!occupiedCells.TryGetValue(cell, out PlacementData data))
            return null;

        foreach (Vector3Int c in GetCells(data.Origin, data.Size))
            occupiedCells.Remove(c);

        return data.PlacedObject;
    }

    // returns the PlacementData for any cell, or null if empty.
    public PlacementData GetPlacement(Vector3Int cell)
    {
        occupiedCells.TryGetValue(cell, out PlacementData data);
        return data;
    }

    public bool IsCellOccupied(Vector3Int cell) => occupiedCells.ContainsKey(cell);

    // enumerate every cell covered by this placement
    private IEnumerable<Vector3Int> GetCells(Vector3Int origin, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                yield return origin + new Vector3Int(x, 0, y);
    }
}

public class PlacementData
{
    public Vector3Int Origin;
    public Vector2Int Size;
    public GameObject PlacedObject;

    public PlacementData(Vector3Int origin, Vector2Int size, GameObject obj)
    {
        Origin = origin;
        Size = size;
        PlacedObject = obj;
    }
}
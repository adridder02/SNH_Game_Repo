using System.Collections.Generic;
using UnityEngine;

// =============================================================
// SparkmintCircuitManager.cs
// -------------------------------------------------------------
// Pure logic, no MonoBehaviour — SparkmintLeafFence instances call
// Recalculate() whenever a leaf is placed, removed, or expires.
//
// ALGORITHM (per-surface, run on the whole grid — grids here are small
// so this is cheap and only runs on actual circuit changes, not per frame):
//   1. Collect every grid cell currently occupied by an ACTIVE
//      (non-expired) Sparkmint leaf.
//   2. Flood-fill from every border cell that ISN'T a leaf, walking
//      only through non-leaf cells. Everything the flood fill reaches
//      is "outside" the circuit.
//   3. Any non-leaf cell the flood fill never reached is fully enclosed
//      -> "inside". A ring of leaves with no gaps produces some inside
//      cells; a ring with even one gap lets the flood fill leak in and
//      inside ends up empty — exactly "closed circuit or it doesn't count".
//   4. Any pot sitting in an inside cell has its plant warded via
//      PlantState.AddMiasmaImmunitySource(surface) — the surface itself
//      is used as the stable source key, one per surface's circuit.
// =============================================================
public static class SparkmintCircuitManager
{
    private static readonly Dictionary<GreenhouseSurface, HashSet<PlantState>> wardedPlants = new();

    private static readonly Vector3Int[] Dirs4 =
    {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
    };

    public static void Recalculate(GreenhouseSurface surface, GridData gridData)
    {
        if (surface == null || gridData == null) return;

        HashSet<Vector3Int> leafCells = CollectActiveLeafCells(surface, gridData);
        HashSet<Vector3Int> interior = FindInteriorCells(surface, leafCells);

        HashSet<PlantState> newlyWarded = new HashSet<PlantState>();
        foreach (Vector3Int cell in interior)
        {
            PlacementData data = gridData.GetPlacement(cell);
            PotContents pot = data?.PlacedObject != null ? data.PlacedObject.GetComponent<PotContents>() : null;
            if (pot != null && pot.HasPlant && pot.Plant != null)
                newlyWarded.Add(pot.Plant);
        }

        wardedPlants.TryGetValue(surface, out HashSet<PlantState> previouslyWarded);
        previouslyWarded ??= new HashSet<PlantState>();

        foreach (PlantState plant in previouslyWarded)
            if (plant != null && !newlyWarded.Contains(plant))
                plant.RemoveMiasmaImmunitySource(surface);

        foreach (PlantState plant in newlyWarded)
            plant.AddMiasmaImmunitySource(surface);

        wardedPlants[surface] = newlyWarded;
    }

    private static HashSet<Vector3Int> CollectActiveLeafCells(GreenhouseSurface surface, GridData gridData)
    {
        var cells = new HashSet<Vector3Int>();
        Vector2Int dims = surface.GridDimensions;

        for (int x = 0; x < dims.x; x++)
        {
            for (int z = 0; z < dims.y; z++)
            {
                Vector3Int c = new Vector3Int(x, 0, z);
                PlacementData data = gridData.GetPlacement(c);
                SparkmintLeafFence leaf = data?.PlacedObject != null ? data.PlacedObject.GetComponent<SparkmintLeafFence>() : null;
                if (leaf != null && leaf.IsActive)
                    cells.Add(c);
            }
        }
        return cells;
    }

    private static HashSet<Vector3Int> FindInteriorCells(GreenhouseSurface surface, HashSet<Vector3Int> leafCells)
    {
        Vector2Int dims = surface.GridDimensions;
        var outside = new HashSet<Vector3Int>();
        var queue = new Queue<Vector3Int>();

        void TrySeed(Vector3Int c)
        {
            if (!leafCells.Contains(c) && outside.Add(c))
                queue.Enqueue(c);
        }

        for (int x = 0; x < dims.x; x++)
        {
            TrySeed(new Vector3Int(x, 0, 0));
            TrySeed(new Vector3Int(x, 0, dims.y - 1));
        }
        for (int z = 0; z < dims.y; z++)
        {
            TrySeed(new Vector3Int(0, 0, z));
            TrySeed(new Vector3Int(dims.x - 1, 0, z));
        }

        while (queue.Count > 0)
        {
            Vector3Int cur = queue.Dequeue();
            foreach (Vector3Int d in Dirs4)
            {
                Vector3Int next = cur + d;
                if (next.x < 0 || next.x >= dims.x || next.z < 0 || next.z >= dims.y) continue;
                if (leafCells.Contains(next)) continue;
                if (outside.Add(next)) queue.Enqueue(next);
            }
        }

        var interior = new HashSet<Vector3Int>();
        for (int x = 0; x < dims.x; x++)
        {
            for (int z = 0; z < dims.y; z++)
            {
                Vector3Int c = new Vector3Int(x, 0, z);
                if (!leafCells.Contains(c) && !outside.Contains(c))
                    interior.Add(c);
            }
        }
        return interior;
    }

    /// <summary>Call when a surface is being torn down (scene unload) to avoid holding stale plant refs.</summary>
    public static void ClearSurface(GreenhouseSurface surface) => wardedPlants.Remove(surface);
}

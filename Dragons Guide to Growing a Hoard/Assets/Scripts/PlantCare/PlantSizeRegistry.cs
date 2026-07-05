using System;
using System.Collections.Generic;
using UnityEngine;

// =============================================================
// PlantSizeRegistry.cs
// -------------------------------------------------------------
// SINGLE SOURCE OF TRUTH for what each named PlantSize actually
// measures, in grid cells. Everything that used to hardcode a
// footprint (PlantSizeUtility's old switch statement, PotData's
// old standalone `size` field) now reads from here instead.
//
// Want "Large" to mean 4x4 instead of 2x2? Change the Large entry
// on this ONE asset. Every pot and every plant tagged Large — in
// the world-placement grid and the inventory grid alike — picks
// it up automatically. No code changes, no touching PotData or
// PlantState assets one-by-one.
//
// SETUP (one-time):
//   1. Assets → Create → Greenhouse → Plant Size Registry
//   2. Move/create the asset at: Assets/Resources/PlantSizeRegistry.asset
//      (it MUST live in a folder named "Resources" — that's how
//      Instance below finds it at runtime).
//   3. Fill in the footprint for each PlantSize value.
//
// If you ever add a new value to the PlantSize enum (PlantState.cs),
// add a matching entry here too, or GetFootprint() will warn and
// fall back to a default.
// =============================================================
[CreateAssetMenu(fileName = "PlantSizeRegistry", menuName = "Greenhouse/Plant Size Registry")]
public class PlantSizeRegistry : ScriptableObject
{
    [Serializable]
    public class SizeEntry
    {
        public PlantSize size;

        [Tooltip("Grid footprint (width x height) for this size — used for BOTH the world pot-placement " +
                 "grid and the player's inventory grid. One number to rule them both.")]
        public Vector2Int footprint = Vector2Int.one;
    }

    [Tooltip("One entry per PlantSize value. This IS the size system — everything else just looks values up here.")]
    public List<SizeEntry> entries = new List<SizeEntry>
    {
        new SizeEntry { size = PlantSize.Small,  footprint = new Vector2Int(1, 1) },
        new SizeEntry { size = PlantSize.Medium, footprint = new Vector2Int(1, 2) },
        new SizeEntry { size = PlantSize.Large,  footprint = new Vector2Int(2, 2) },
    };

    private static PlantSizeRegistry _instance;

    /// <summary>Lazily loaded from Resources/PlantSizeRegistry.asset. Cached after first access.</summary>
    public static PlantSizeRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<PlantSizeRegistry>("PlantSizeRegistry");
                if (_instance == null)
                {
                    Debug.LogError("[PlantSizeRegistry] No asset found at Resources/PlantSizeRegistry. " +
                                   "Create one via Assets → Create → Greenhouse → Plant Size Registry and " +
                                   "place it inside a folder named 'Resources'. Falling back to built-in defaults for now.");
                }
            }
            return _instance;
        }
    }

    /// <summary>Looks up the footprint for a given size. Safe to call even if the asset is missing (logs + falls back).</summary>
    public static Vector2Int GetFootprint(PlantSize size)
    {
        var instance = Instance;
        if (instance != null)
        {
            foreach (var entry in instance.entries)
            {
                if (entry.size == size)
                    return entry.footprint;
            }
            Debug.LogWarning($"[PlantSizeRegistry] No entry for '{size}' on the registry asset — add one. Falling back to default.");
        }
        return FallbackFootprint(size);
    }

    // Last-resort defaults, only used if the registry asset is missing entirely
    // or doesn't have an entry for this size yet. Keeps things from breaking
    // outright, but you should fix the actual asset if you see this log.
    private static Vector2Int FallbackFootprint(PlantSize size)
    {
        switch (size)
        {
            case PlantSize.Small:  return new Vector2Int(1, 1);
            case PlantSize.Medium: return new Vector2Int(1, 2);
            case PlantSize.Large:  return new Vector2Int(2, 2);
            default:               return Vector2Int.one;
        }
    }
}

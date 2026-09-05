using UnityEngine;

// =============================================================
// AbilityItemInstance.cs
// -------------------------------------------------------------
// A stack of one AbilityItemData in the player's ability inventory.
//
// Now implements IGridPlaceable so a stack can sit in the SAME shared
// main-inventory grid as plants (fills the grid first, overflows to
// Available once it's full — same as plants), instead of living in a
// separate unbounded Abilities list. PlayerAbilityInventory is still
// the single source of truth for COUNTS (Add/TryConsume/GetCount) —
// this just adds a grid position on top of that, kept in sync by
// PlayerInventory (see PlayerInventory.ReconcileAbilityGridPlacement).
//
// instanceId is generated once per stack and never changes for the
// life of that stack object — PlayerAbilityInventory.Add() reuses the
// same AbilityItemInstance object (just bumping .count) for repeat
// pickups of the same AbilityItemData, so the grid position sticks
// across restocks. A stack is only ever auto-placed ONCE, the first
// time it's picked up — see PlayerInventory for that logic.
// =============================================================
[System.Serializable]
public class AbilityItemInstance : IGridPlaceable
{
    public readonly string instanceId;
    public AbilityItemData data;
    public int count;

    public int gridX = -1;
    public int gridY = -1;

    public AbilityItemInstance(AbilityItemData data, int count)
    {
        instanceId = System.Guid.NewGuid().ToString();
        this.data = data;
        this.count = count;
    }

    // IGridPlaceable
    public string InstanceId => instanceId;

    // Footprint comes from AbilityItemData.footprint (already used for world placement via
    // AbilityPlacementSystem) — falls back to 1x1 if it's unset (Vector2Int.zero on a fresh
    // asset), since a 0-sized footprint would never fit anywhere.
    public Vector2Int Footprint => (data != null && data.footprint.x > 0 && data.footprint.y > 0)
        ? data.footprint
        : Vector2Int.one;

    public int GridX { get => gridX; set => gridX = value; }
    public int GridY { get => gridY; set => gridY = value; }
    public bool IsInGrid => gridX >= 0 && gridY >= 0;
}

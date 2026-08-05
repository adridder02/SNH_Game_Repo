using UnityEngine;

// =============================================================
// AbilityPlaceable.cs
// -------------------------------------------------------------
// Base class for anything AbilityPlacementSystem puts on the grid
// (Sparkmint leaves, Waterbells, ...). Mirrors what PotContents is
// to pots: this is the "what does the grid cell actually DO" half,
// while AbilityPlacementSystem is the "hover/place/remove" half —
// same split PlacementSystem/PotContents already use.
//
// Every concrete placeable (SparkmintLeafFence.cs, WaterbellSprinkler.cs)
// derives from this and overrides OnPlaced/OnRemoved for setup/teardown,
// plus draws its own OnDrawGizmos placeholder per the "gizmo for now"
// brief — there's no real visual yet since placedPrefab is usually just
// an empty GameObject with this component on it.
// =============================================================
public abstract class AbilityPlaceable : MonoBehaviour
{
    public AbilityItemData Data { get; private set; }
    public Vector3Int GridOrigin { get; private set; }
    public Vector2Int GridSize { get; private set; }
    public GridData GridData { get; private set; }
    public GreenhouseSurface Surface { get; private set; }

    /// <summary>Called once, right after Instantiate + AddPlacement, by AbilityPlacementSystem.</summary>
    public void Initialise(AbilityItemData data, Vector3Int origin, Vector2Int size, GridData gridData, GreenhouseSurface surface)
    {
        Data = data;
        GridOrigin = origin;
        GridSize = size;
        GridData = gridData;
        Surface = surface;
        OnPlaced();
    }

    /// <summary>Called by AbilityPlacementSystem right before this GameObject is destroyed (removed).</summary>
    public void NotifyRemoved() => OnRemoved();

    protected virtual void OnPlaced() { }
    protected virtual void OnRemoved() { }

    // ---------------------------------------------------------------
    // Shared gizmo helper — every concrete placeable can call this in its
    // own OnDrawGizmos for a consistent "footprint outline" placeholder.
    // ---------------------------------------------------------------
    protected void DrawFootprintGizmo(Color color)
    {
        if (Surface == null) return;

        Gizmos.color = color;
        float cs = Surface.CellSize;
        Vector3 centre = Surface.GridOriginWorld + new Vector3(
            (GridOrigin.x + GridSize.x * 0.5f) * cs,
            0.05f,
            (GridOrigin.z + GridSize.y * 0.5f) * cs);

        Gizmos.DrawWireCube(centre, new Vector3(GridSize.x * cs, 0.1f, GridSize.y * cs));
    }
}

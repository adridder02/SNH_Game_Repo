using UnityEngine;

// ! Attach this to any plane/quad in the greenhouse
// it reads the object's world-space size, works out how many fixed-size
// cells fit and spawns a GridVisual centred on the surface.

[RequireComponent(typeof(Renderer))]
public class GreenhouseSurface : MonoBehaviour
{
    [Header("Grid settings")]
    [Tooltip("World-space size of one grid cell (e.g. 1 = 1 Unity unit per cell)")]
    [SerializeField] private float cellSize = 1f;

    [Header("Visual")]
    [SerializeField] private GridVisual gridVisualPrefab;
    [Tooltip("URP only: assign a URP/Unlit Transparent material here. Leave blank for Built-in pipeline.")]
    [SerializeField] private Material gridMaterial;

    // accessible by PlacementSystem
    public GridVisual GridVisual { get; private set; }
    public float CellSize => cellSize;
    public Vector2Int GridDimensions { get; private set; }

    [Header("Pre-filled surface")]
    [Tooltip("ON = the player can't place anything on this surface (PlacementSystem ignores it). Normally " +
             "left OFF; a PrefilledSurface component turns this on at Start and off again once every " +
             "pre-placed pot on the surface has been picked up.")]
    [SerializeField] private bool placementLocked = false;

    // GridClutter objects currently sitting on this grid. Any uncleared one locks the grid.
    private readonly System.Collections.Generic.HashSet<GridClutter> clutter = new System.Collections.Generic.HashSet<GridClutter>();

    /// <summary>True while the player is not allowed to place on this surface (manually locked, or any
    /// GridClutter is still on it).</summary>
    public bool PlacementLocked => placementLocked || clutter.Count > 0;

    public void SetPlacementLocked(bool locked)
    {
        placementLocked = locked;
        RefreshLockVisual();
    }

    public void RegisterClutter(GridClutter c)
    {
        if (c == null) return;
        clutter.Add(c);
        RefreshLockVisual();
    }

    public void UnregisterClutter(GridClutter c)
    {
        if (clutter.Remove(c))
        {
            RefreshLockVisual();
            if (clutter.Count == 0) Debug.Log($"[GreenhouseSurface] '{name}' is clear - open for placement.", this);
        }
    }

    // Locked grids draw faint, unlocked normally.
    private void RefreshLockVisual()
    {
        if (GridVisual != null) GridVisual.SetDimmed(PlacementLocked);
    }

    [Header("Water surface")]
    [Tooltip("OFF (default) = normal surface: pots are placed as usual. ON = placing a pot here only places its " +
             "SOIL, set to Water and locked (no Change Soil button) - like a pool you plant into. Pot size/" +
             "footprint, planting, picking up and moving all work as normal.")]
    [SerializeField] private bool waterSurface = false;

    /// <summary>True if pots placed on this surface become water-soil-only (the pot body is hidden).</summary>
    public bool WaterSurface => waterSurface;

    [Tooltip("Water Surface only: how far the SOIL VISUAL sits above (+) or below (-) the surface, since the soil is " +
             "IN the water rather than on top of it. Only the soil moves - the plant, placement preview and " +
             "everything else stay at the normal surface height. Ignored on normal surfaces.")]
    [SerializeField] private float waterSoilHeightOffset = 0f;

    /// <summary>Vertical offset of the soil visual on a Water Surface, else 0.</summary>
    public float SoilHeightOffset => waterSurface ? waterSoilHeightOffset : 0f;

    /// <summary>Grid X / Z axes in world space (flat). Equal to world X / Z unless the surface is rotated.</summary>
    public Vector3 GridRight { get; private set; } = Vector3.right;
    public Vector3 GridForward { get; private set; } = Vector3.forward;

    /// <summary>The surface's yaw in degrees (rotation about Y) that the grid follows.</summary>
    public float Yaw { get; private set; }

    /// <summary>World position (at grid height) of a point given in CELL units from the grid origin, e.g. (0.5, 0.5) = centre of cell 0,0.</summary>
    public Vector3 CellToWorld(float cellX, float cellZ)
    {
        return GridOriginWorld + GridRight * (cellX * cellSize) + GridForward * (cellZ * cellSize);
    }

    /// <summary>A world position expressed in CELL units from the grid origin (x along GridRight, y along GridForward).</summary>
    public Vector2 WorldToGridCells(Vector3 world)
    {
        Vector3 d = world - GridOriginWorld;
        return new Vector2(Vector3.Dot(d, GridRight) / cellSize, Vector3.Dot(d, GridForward) / cellSize);
    }

    // bottom left world position of the grid (y is the surface's y)
    public Vector3 GridOriginWorld { get; private set; }

    private void Awake()
    {
        BuildGrid();
    }

    private void BuildGrid()
    {
        Renderer rend = GetComponent<Renderer>();

        // Grid axes follow the surface's yaw so rotated shelves/tables get a rotated grid. GridRight is the
        // grid's X axis, GridForward its Z axis (both flat on the XZ plane). Unrotated = world X / world Z.
        Vector3 r = transform.right; r.y = 0f;
        if (r.sqrMagnitude < 1e-4f) { r = transform.forward; r.y = 0f; }
        if (r.sqrMagnitude < 1e-4f) r = Vector3.right;
        r.Normalize();
        GridRight = r;
        GridForward = new Vector3(-r.z, 0f, r.x);
        Yaw = Mathf.Atan2(-r.z, r.x) * Mathf.Rad2Deg;

        // Footprint: the renderer's 8 box corners projected onto the grid axes. Unrotated surfaces use the
        // world-space bounds exactly as before; rotated ones use the local bounds so the footprint is the
        // true rotated rectangle, not the (bigger) axis-aligned box around it.
        Vector3[] corners = new Vector3[8];
        bool axisAligned = Mathf.Abs(r.z) < 1e-3f && r.x > 0f;
        if (axisAligned)
        {
            Bounds b = rend.bounds;
            for (int i = 0; i < 8; i++)
                corners[i] = b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
        }
        else
        {
            Bounds lb = rend.localBounds;
            for (int i = 0; i < 8; i++)
                corners[i] = rend.transform.TransformPoint(lb.center + Vector3.Scale(lb.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
        }

        float minR = float.MaxValue, maxR = float.MinValue, minF = float.MaxValue, maxF = float.MinValue, maxY = float.MinValue;
        foreach (Vector3 c in corners)
        {
            float pr = Vector3.Dot(c, GridRight);
            float pf = Vector3.Dot(c, GridForward);
            if (pr < minR) minR = pr; if (pr > maxR) maxR = pr;
            if (pf < minF) minF = pf; if (pf > maxF) maxF = pf;
            if (c.y > maxY) maxY = c.y;
        }

        int cols = Mathf.Max(1, Mathf.FloorToInt((maxR - minR) / cellSize));
        int rows = Mathf.Max(1, Mathf.FloorToInt((maxF - minF) / cellSize));
        GridDimensions = new Vector2Int(cols, rows);

        // centre the grid on the surface
        float gridWidth = cols * cellSize;
        float gridDepth = rows * cellSize;
        float cr = (minR + maxR) * 0.5f - gridWidth * 0.5f;
        float cf = (minF + maxF) * 0.5f - gridDepth * 0.5f;

        Vector3 originXZ = GridRight * cr + GridForward * cf;
        GridOriginWorld = new Vector3(originXZ.x, maxY + 0.001f, originXZ.z); // sit just above the surface

        // spawn the visual as a child so it moves with the plane
        if (gridVisualPrefab != null)
        {
            GridVisual = Instantiate(gridVisualPrefab, transform);
            DetachGridVisual();
            GridVisual.Initialise(GridDimensions, cellSize, GridOriginWorld, GridRight, GridForward);
            GridVisual.SetMaterial(gridMaterial);
        }
        else
        {
            GameObject go = new GameObject("GridVisual");
            go.transform.SetParent(transform);
            GridVisual = go.AddComponent<GridVisual>();
            DetachGridVisual();
            GridVisual.Initialise(GridDimensions, cellSize, GridOriginWorld, GridRight, GridForward);
            GridVisual.SetMaterial(gridMaterial);
        }

        GridVisual.SetDimmed(PlacementLocked);
        GridVisual.SetVisible(false); // hidden until placement mode starts
    }

    // The grid visual is built in WORLD space, so it must not inherit this surface's scale/rotation - a scaled
    // (or rotated + non-uniformly scaled) parent used to stretch/skew the cell quads.
    private void DetachGridVisual()
    {
        GridVisual.transform.SetParent(null, true);
        GridVisual.transform.position = Vector3.zero;
        GridVisual.transform.rotation = Quaternion.identity;
        GridVisual.transform.localScale = Vector3.one;
        GridVisual.gameObject.name = $"GridVisual_{name}";
    }

    private void OnDestroy()
    {
        if (GridVisual != null) Destroy(GridVisual.gameObject);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Color.cyan;
        for (int x = 0; x < GridDimensions.x; x++)
        {
            for (int z = 0; z < GridDimensions.y; z++)
            {
                Vector3 cellCentre = CellToWorld(x + 0.5f, z + 0.5f);
                Gizmos.matrix = Matrix4x4.TRS(cellCentre, Quaternion.Euler(0f, Yaw, 0f), Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(cellSize, 0.01f, cellSize));
            }
        }
    }
#endif
}
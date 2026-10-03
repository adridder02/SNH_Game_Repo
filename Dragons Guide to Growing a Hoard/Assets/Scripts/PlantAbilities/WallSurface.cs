using UnityEngine;

// =============================================================
// WallSurface.cs
// -------------------------------------------------------------
// Attach to any wall plane/quad that should accept Clovenwick-style
// mounted placements. Counterpart to GreenhouseSurface, but for a
// VERTICAL plane instead of the horizontal pot grid — GreenhouseSurface/
// GridVisual hard-code world X/Z (see GridVisual.BuildCellQuads and
// OnRenderObject), so this is a parallel implementation rather than a
// reuse of that class, working in the wall's own horizontal/vertical
// axes instead.
//
// SAME SIMPLIFYING ASSUMPTION the existing floor grid already makes
// (GreenhouseSurface also just reads world-space Renderer bounds,
// implicitly assuming the surface is axis-aligned): a wall is assumed
// to be axis-aligned to one of the two vertical world planes, picked
// via 'facing' below, rather than arbitrarily rotated. Covers a
// rectangular greenhouse's four walls; a fully general arbitrary-
// rotation version would need local-space basis math throughout
// WallGridVisual instead of the world X/Y/Z shortcuts it currently uses.
//
// SETUP: same shape as GreenhouseSurface — attach to a wall plane,
// pick which world axis it faces, assign a WallGridVisual prefab
// (or leave empty to auto-build one), done.
// =============================================================
[RequireComponent(typeof(Renderer))]
public class WallSurface : MonoBehaviour
{
    public enum FacingAxis
    {
        FacesZ, // wall lies in the XY plane (horizontal axis = world X, vertical = world Y)
        FacesX  // wall lies in the ZY plane (horizontal axis = world Z, vertical = world Y)
    }

    [Header("Grid settings")]
    [SerializeField] private float cellSize = 2f;
    [SerializeField] private FacingAxis facing = FacingAxis.FacesZ;

    [Header("Visual")]
    [SerializeField] private WallGridVisual gridVisualPrefab;
    [SerializeField] private Material gridMaterial;

    public WallGridVisual GridVisual { get; private set; }
    public float CellSize => cellSize;
    public Vector2Int GridDimensions { get; private set; }
    public FacingAxis Facing => facing;

    /// <summary>World position of the grid's bottom-left cell corner.</summary>
    public Vector3 GridOriginWorld { get; private set; }

    private void Awake()
    {
        BuildGrid();
    }

    private void BuildGrid()
    {
        Bounds bounds = GetComponent<Renderer>().bounds;

        float worldWidth = facing == FacingAxis.FacesZ ? bounds.size.x : bounds.size.z;
        float worldHeight = bounds.size.y;

        int cols = Mathf.Max(1, Mathf.FloorToInt(worldWidth / cellSize));
        int rows = Mathf.Max(1, Mathf.FloorToInt(worldHeight / cellSize));
        GridDimensions = new Vector2Int(cols, rows);

        float gridWidth = cols * cellSize;
        float gridHeight = rows * cellSize;

        Vector3 centre = bounds.center;
        // sit just off the wall's face so quads don't z-fight with the wall mesh
        centre += (facing == FacingAxis.FacesZ ? Vector3.back : Vector3.left) * -0.001f;

        float originH = (facing == FacingAxis.FacesZ ? centre.x : centre.z) - gridWidth * 0.5f;
        float originV = centre.y - gridHeight * 0.5f;

        GridOriginWorld = facing == FacingAxis.FacesZ
            ? new Vector3(originH, originV, centre.z)
            : new Vector3(centre.x, originV, originH);

        GridVisual = gridVisualPrefab != null
            ? Instantiate(gridVisualPrefab, transform)
            : new GameObject("WallGridVisual").AddComponent<WallGridVisual>();

        GridVisual.transform.SetParent(transform, worldPositionStays: true);
        GridVisual.Initialise(GridDimensions, cellSize, GridOriginWorld, facing);
        GridVisual.SetMaterial(gridMaterial);
        GridVisual.SetVisible(false);
    }

    /// <summary>World-space centre of a given cell — origin + half-cell offsets along whichever
    /// world axes this wall's 'facing' maps horizontal/vertical to.</summary>
    public Vector3 CellToWorldCentre(Vector2Int cell) => CellToWorldCentre(cell, Vector2Int.one);

    /// <summary>World-space centre of a footprint (origin cell + size), for multi-cell placements.</summary>
    public Vector3 CellToWorldCentre(Vector2Int origin, Vector2Int size)
    {
        float h = origin.x * cellSize + size.x * cellSize * 0.5f;
        float v = origin.y * cellSize + size.y * cellSize * 0.5f;

        return facing == FacingAxis.FacesZ
            ? new Vector3(GridOriginWorld.x + h, GridOriginWorld.y + v, GridOriginWorld.z)
            : new Vector3(GridOriginWorld.x, GridOriginWorld.y + v, GridOriginWorld.z + h);
    }

    /// <summary>The rotation a mounted prefab should use to sit flush against this wall, facing outward.</summary>
   public Quaternion MountRotation =>
    facing == FacingAxis.FacesZ
        ? Quaternion.LookRotation(Vector3.back) * Quaternion.Euler(-90f, 90f, 0f)
        : Quaternion.LookRotation(Vector3.left) * Quaternion.Euler(-90f, 90f, 0f); //!MAY NEED TO CHANGE THIS FOR THE X AXIS

}

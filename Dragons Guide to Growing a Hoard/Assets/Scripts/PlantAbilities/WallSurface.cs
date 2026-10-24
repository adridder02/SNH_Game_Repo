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
        FacesZ, // wall lies in the XY plane, room is on its +Z side (horizontal axis = world X, vertical = world Y)
        FacesX, // wall lies in the ZY plane, room is on its +X side (horizontal axis = world Z, vertical = world Y)

        // Appended AFTER the originals so existing scenes keep their serialized value (0 / 1).
        [InspectorName("Faces -Z")] FacesNegZ, // XY plane, room is on its -Z side (things mount facing -Z)
        [InspectorName("Faces -X")] FacesNegX  // ZY plane, room is on its -X side (things mount facing -X)
    }

    /// <summary>True if this facing's wall runs along world X (FacesZ / FacesNegZ); false if it runs
    /// along world Z (FacesX / FacesNegX).</summary>
    public static bool FacingRunsAlongX(FacingAxis f) => f == FacingAxis.FacesZ || f == FacingAxis.FacesNegZ;

    /// <summary>The direction pointing OUT of the wall into the room for this facing.</summary>
    public static Vector3 FacingNormal(FacingAxis f)
    {
        switch (f)
        {
            case FacingAxis.FacesZ:    return Vector3.forward;
            case FacingAxis.FacesNegZ: return Vector3.back;
            case FacingAxis.FacesX:    return Vector3.right;
            default:                   return Vector3.left; // FacesNegX
        }
    }

    [Header("Grid settings")]
    [SerializeField] private float cellSize = 2f;
    [SerializeField] private FacingAxis facing = FacingAxis.FacesZ;

    [Header("Placement offset")]
    [Tooltip("Pushes everything mounted on THIS wall this far OUT from the wall, along its facing " +
             "direction (world units). Use it when the grid origin sits inside the wall mesh, or the " +
             "mounted object's pivot makes it clip into the wall. Negative pulls it into the wall. " +
             "Stacks with the Wall Offset on each WallMushroomData.")]
    [SerializeField] private float placementOffset = 0f;

    [Header("Visual")]
    [SerializeField] private WallGridVisual gridVisualPrefab;
    [SerializeField] private Material gridMaterial;

    public WallGridVisual GridVisual { get; private set; }
    public float CellSize => cellSize;
    public Vector2Int GridDimensions { get; private set; }
    public FacingAxis Facing => facing;

    /// <summary>True if this wall runs along world X (see FacingRunsAlongX).</summary>
    public bool RunsAlongX => FacingRunsAlongX(facing);

    /// <summary>Direction pointing out of this wall into the room.</summary>
    public Vector3 OutwardNormal => FacingNormal(facing);

    /// <summary>Per-wall push-out distance (see Placement Offset in the Inspector).</summary>
    public float PlacementOffset => placementOffset;

    /// <summary>World position of the grid's bottom-left cell corner.</summary>
    public Vector3 GridOriginWorld { get; private set; }

    private void Awake()
    {
        BuildGrid();
    }

    private void BuildGrid()
    {
        Bounds bounds = GetComponent<Renderer>().bounds;

        float worldWidth = RunsAlongX ? bounds.size.x : bounds.size.z;
        float worldHeight = bounds.size.y;

        int cols = Mathf.Max(1, Mathf.FloorToInt(worldWidth / cellSize));
        int rows = Mathf.Max(1, Mathf.FloorToInt(worldHeight / cellSize));
        GridDimensions = new Vector2Int(cols, rows);

        float gridWidth = cols * cellSize;
        float gridHeight = rows * cellSize;

        Vector3 centre = bounds.center;
        // sit just off the wall's face so quads don't z-fight with the wall mesh
        centre += OutwardNormal * 0.001f;

        float originH = (RunsAlongX ? centre.x : centre.z) - gridWidth * 0.5f;
        float originV = centre.y - gridHeight * 0.5f;

        GridOriginWorld = RunsAlongX
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

        return RunsAlongX
            ? new Vector3(GridOriginWorld.x + h, GridOriginWorld.y + v, GridOriginWorld.z)
            : new Vector3(GridOriginWorld.x, GridOriginWorld.y + v, GridOriginWorld.z + h);
    }

    /// <summary>Where a mounted object should actually sit: the footprint's centre on the grid,
    /// pushed out from the wall by this wall's Placement Offset plus 'extraOutOffset' (the mushroom
    /// type's own Wall Offset), then nudged sideways/up within the wall plane by 'planeNudge'
    /// (x = along the wall, y = up). Used for both the hover preview and the final placement so they
    /// always agree.</summary>
    public Vector3 MountPosition(Vector2Int origin, Vector2Int size, float extraOutOffset = 0f, Vector2 planeNudge = default)
    {
        Vector3 pos = CellToWorldCentre(origin, size);
        pos += OutwardNormal * (placementOffset + extraOutOffset);
        pos += (RunsAlongX ? Vector3.right : Vector3.forward) * planeNudge.x;
        pos += Vector3.up * planeNudge.y;
        return pos;
    }

    /// <summary>The rotation a mounted prefab should use to sit flush against this wall, facing outward.</summary>
    public Quaternion MountRotation
    {
        get
        {
            // FacesZ / FacesX keep the exact rotations that were already working. The negative
            // facings are those same rotations turned 180 degrees about the vertical axis, so a
            // prefab sits on a -Z / -X wall exactly the way it sits on a +Z / +X one, just mirrored
            // into the opposite direction.
            Quaternion positive = RunsAlongX
                ? Quaternion.LookRotation(Vector3.back) * Quaternion.Euler(-90f, 90f, 0f)
                : Quaternion.LookRotation(Vector3.left) * Quaternion.Euler(-90f, 90f, 0f);

            bool negative = facing == FacingAxis.FacesNegZ || facing == FacingAxis.FacesNegX;
            return negative ? Quaternion.Euler(0f, 180f, 0f) * positive : positive;
        }
    }

}

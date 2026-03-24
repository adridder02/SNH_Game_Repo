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

    // bottom left world position of the grid (y is the surface's y)
    public Vector3 GridOriginWorld { get; private set; }

    private void Awake()
    {
        BuildGrid();
    }

    private void BuildGrid()
    {
        // use the renderer bounds to get the actual world space footprint
        Bounds bounds = GetComponent<Renderer>().bounds;

        float worldWidth = bounds.size.x;
        float worldDepth = bounds.size.z;

        // how many whole cells fit?
        int cols = Mathf.FloorToInt(worldWidth / cellSize);
        int rows = Mathf.FloorToInt(worldDepth / cellSize);

        cols = Mathf.Max(1, cols);
        rows = Mathf.Max(1, rows);

        GridDimensions = new Vector2Int(cols, rows);

        // centre the grid on the plane surface
        float gridWidth = cols * cellSize;
        float gridDepth = rows * cellSize;

        Vector3 centre = bounds.center;
        centre.y = bounds.max.y + 0.001f; // sit just above surface to avoid z-fight

        GridOriginWorld = new Vector3(
            centre.x - gridWidth * 0.5f,
            centre.y,
            centre.z - gridDepth * 0.5f
        );

        // spawn the visual as a child so it moves with the plane
        if (gridVisualPrefab != null)
        {
            GridVisual = Instantiate(gridVisualPrefab, transform);
            GridVisual.Initialise(GridDimensions, cellSize, GridOriginWorld);
            GridVisual.SetMaterial(gridMaterial);
        }
        else
        {
            GameObject go = new GameObject("GridVisual");
            go.transform.SetParent(transform);
            GridVisual = go.AddComponent<GridVisual>();
            GridVisual.Initialise(GridDimensions, cellSize, GridOriginWorld);
            GridVisual.SetMaterial(gridMaterial);
        }

        GridVisual.SetVisible(false); // hidden until placement mode starts
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
                Vector3 cellCentre = GridOriginWorld + new Vector3(
                    x * cellSize + cellSize * 0.5f,
                    0,
                    z * cellSize + cellSize * 0.5f);
                Gizmos.DrawWireCube(cellCentre, new Vector3(cellSize, 0.01f, cellSize));
            }
        }
    }
#endif
}
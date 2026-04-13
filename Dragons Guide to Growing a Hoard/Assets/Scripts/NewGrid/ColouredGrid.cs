using UnityEngine;
using UnityEngine.InputSystem;
/// <summary>
/// Generates a subdivided plane mesh at runtime, colours each cell with a
/// checkerboard pattern, draws grid-line outlines using Unity's GL API, and
/// lets the player click any cell to highlight it.
///
/// SETUP
/// -----
/// 1. Create an empty GameObject in your scene.
/// 2. Attach this script to it.
/// 3. Also attach a MeshFilter and MeshRenderer (the [RequireComponent]
///    attributes below will remind Unity to add them automatically).
/// 4. Assign a material that reads vertex colours.  In URP use:
///      Shader Graph → Unlit → plug VertexColor node into Base Color, OR
///      use the built-in "Particles/Standard Unlit" shader as a quick test.
/// 5. Assign the PlantPlacer script's reference to this grid (see PlantPlacer).
/// 6. Press Play — the grid is generated entirely in code; no pre-built mesh
///    asset is required.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ColoredGrid : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector-exposed configuration
    // -------------------------------------------------------------------------

    [Header("Grid dimensions")]
    [Tooltip("Number of columns (cells along the X axis).")]
    public int columns = 10;

    [Tooltip("Number of rows (cells along the Z axis).")]
    public int rows = 10;

    [Tooltip("World-space size of each individual cell (in Unity units).")]
    public float cellSize = 1f;

    [Header("Cell colours")]
    [Tooltip("First colour of the checkerboard pattern.")]
    public Color colorA = new Color(0.85f, 0.85f, 0.85f);

    [Tooltip("Second colour of the checkerboard pattern.")]
    public Color colorB = new Color(0.55f, 0.55f, 0.55f);

    [Tooltip("Colour applied to a cell when the player clicks it.")]
    public Color highlightColor = new Color(1f, 0.85f, 0.2f);

    [Tooltip("Colour applied to a cell that already has a plant on it " +
             "(set externally by PlantPlacer).")]
    public Color occupiedColor = new Color(0.3f, 0.7f, 0.35f);

    [Header("Grid outline")]
    [Tooltip("Colour of the lines drawn between cells.")]
    public Color outlineColor = Color.black;

    [Tooltip("Vertical offset above the mesh so the outline lines are not " +
             "clipped by the mesh surface (z-fighting).")]
    public float outlineYOffset = 0.002f;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    // The mesh we build at runtime and re-use throughout the session.
    private Mesh _mesh;

    // Vertex-colour array.  Unity meshes store one colour per vertex, so we
    // keep this array alive and modify it whenever a cell changes colour,
    // then push the whole array back to the mesh.
    private Color[] _vertexColors;

    // Tracks which cell (if any) is currently highlighted.
    // (-1, -1) means nothing is selected.
    private Vector2Int _selectedCell = new Vector2Int(-1, -1);

    // Tracks which cells are occupied by a plant.  We store this separately
    // from the vertex colours so we can restore the correct colour when a
    // cell is deselected.
    private bool[,] _occupied;

    // The "base" colour of every cell — either colorA or colorB depending on
    // the checkerboard position.  Stored so we can restore it on deselect.
    private Color[,] _baseColors;

    // Material used to draw the GL outline lines.
    // GL.Begin / GL.End require a material; we create one from a hidden
    // Unity built-in shader so no external asset is needed.
    private Material _lineMaterial;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        BuildGrid();        // create the mesh geometry and vertex colours
        CreateLineMaterial(); // prepare the GL line-drawing material
    }



    /// <summary>
    /// OnRenderObject is called by Unity after the scene camera has finished
    /// rendering the scene.  It is the correct place to issue raw GL draw
    /// calls so they appear on top of the scene geometry without requiring a
    /// separate camera or render texture.
    /// </summary>
    private void OnRenderObject()
    {
        DrawGridOutlines();
    }

    // -------------------------------------------------------------------------
    // Mesh construction
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds the subdivided plane mesh from scratch.
    ///
    /// A grid of (columns × rows) cells requires:
    ///   • (columns + 1) × (rows + 1) vertices  — one vertex per grid corner
    ///   • columns × rows × 2 triangles          — two triangles per cell
    ///   • columns × rows × 6 triangle indices
    ///
    /// Each cell's four corner vertices are assigned the same colour so the
    /// entire cell appears as a flat, solid colour.  Because adjacent cells
    /// SHARE vertices (the edge between two cells is the same vertex list
    /// entry), we have to be careful: when two neighbouring cells have
    /// different colours, the shared vertex cannot satisfy both colours at once.
    ///
    /// Solution: we do NOT share vertices between cells.  Each cell gets its
    /// own four independent vertices.  This uses more memory but makes
    /// per-cell colouring trivial and avoids colour blending artefacts.
    /// </summary>
    private void BuildGrid()
    {
        // Total number of cells
        int cellCount = columns * rows;

        // 4 unique vertices per cell (we do not share vertices between cells
        // so we can colour each cell independently without blending).
        int vertexCount = cellCount * 4;

        // Allocate arrays
        Vector3[] vertices  = new Vector3[vertexCount];
        Vector2[] uvs       = new Vector2[vertexCount];  // for any texture work
        int[]     triangles = new int[cellCount * 6];    // 2 triangles × 3 indices
        _vertexColors       = new Color[vertexCount];
        _baseColors         = new Color[columns, rows];
        _occupied           = new bool[columns, rows];

        int vIdx = 0; // current vertex write position
        int tIdx = 0; // current triangle-index write position

        for (int z = 0; z < rows; z++)
        {
            for (int x = 0; x < columns; x++)
            {
                // --- Vertex positions (local space) --------------------------
                //
                //   tl ------- tr
                //    |  \      |
                //    |    \    |
                //    |      \  |
                //   bl ------- br
                //
                //  bl = bottom-left corner of this cell
                //  The cell sits on the XZ plane (Y = 0).

                Vector3 bl = new Vector3(x       * cellSize, 0, z       * cellSize);
                Vector3 br = new Vector3((x + 1) * cellSize, 0, z       * cellSize);
                Vector3 tl = new Vector3(x       * cellSize, 0, (z + 1) * cellSize);
                Vector3 tr = new Vector3((x + 1) * cellSize, 0, (z + 1) * cellSize);

                // Write the four vertices for this cell
                vertices[vIdx]     = bl;
                vertices[vIdx + 1] = br;
                vertices[vIdx + 2] = tl;
                vertices[vIdx + 3] = tr;

                // --- UV coordinates ------------------------------------------
                // Map [0,0] → [1,1] across each cell.
                // These are per-cell UVs; if you need UVs across the whole
                // grid replace with (worldPos / totalSize).
                uvs[vIdx]     = new Vector2(0, 0);
                uvs[vIdx + 1] = new Vector2(1, 0);
                uvs[vIdx + 2] = new Vector2(0, 1);
                uvs[vIdx + 3] = new Vector2(1, 1);

                // --- Checkerboard colour -------------------------------------
                // (x + z) even → colorA, odd → colorB
                Color cellColor = (x + z) % 2 == 0 ? colorA : colorB;
                _baseColors[x, z]    = cellColor;   // remember for deselect
                _vertexColors[vIdx]     = cellColor;
                _vertexColors[vIdx + 1] = cellColor;
                _vertexColors[vIdx + 2] = cellColor;
                _vertexColors[vIdx + 3] = cellColor;

                // --- Triangles -----------------------------------------------
                // Triangle 1: bl → tl → tr  (counter-clockwise = front face)
                triangles[tIdx]     = vIdx;
                triangles[tIdx + 1] = vIdx + 2;
                triangles[tIdx + 2] = vIdx + 3;

                // Triangle 2: bl → tr → br
                triangles[tIdx + 3] = vIdx;
                triangles[tIdx + 4] = vIdx + 3;
                triangles[tIdx + 5] = vIdx + 1;

                vIdx += 4; // advance by 4 vertices (one cell)
                tIdx += 6; // advance by 6 indices  (two triangles)
            }
        }

        // --- Assemble the mesh -----------------------------------------------
        _mesh = new Mesh();
        _mesh.name = "ColoredGrid";

        // For grids larger than 65 535 vertices we need 32-bit index buffers.
        // Unity defaults to 16-bit; switch early so we don't hit the limit.
        if (vertexCount > 65535)
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        _mesh.vertices  = vertices;
        _mesh.triangles = triangles;
        _mesh.uv        = uvs;
        _mesh.colors    = _vertexColors;

        // Recalculate normals so lighting works correctly (all point up: 0,1,0).
        _mesh.RecalculateNormals();

        // Assign the mesh to the MeshFilter so it is rendered.
        GetComponent<MeshFilter>().mesh = _mesh;
    }

    // -------------------------------------------------------------------------
    // GL outline drawing
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a minimal material used exclusively for the GL line calls.
    /// We use Unity's hidden "Internal-Colored" shader which simply applies
    /// a vertex colour — exactly what GL.Color() sets.
    /// </summary>
    private void CreateLineMaterial()
    {
        // "Hidden/Internal-Colored" is always available in Unity regardless of
        // the render pipeline.
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        _lineMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        // Disable depth-testing so lines always appear on top of the mesh.
        _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _lineMaterial.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
        _lineMaterial.SetInt("_ZWrite",   0); // do not write to depth buffer
    }

    /// <summary>
    /// Draws horizontal and vertical lines that form the grid outline using
    /// Unity's immediate-mode GL API.
    ///
    /// GL coordinates are in WORLD space when called from OnRenderObject.
    /// We therefore add this GameObject's world position as an offset so the
    /// grid lines follow the GameObject if it is moved.
    /// </summary>
    private void DrawGridOutlines()
    {
        if (_lineMaterial == null) return;

        // Apply the line material (sets up shader passes)
        _lineMaterial.SetPass(0);

        // World-space origin of this grid object
        Vector3 origin = transform.position;

        // Y level for the lines: slightly above the mesh to avoid z-fighting
        float y = origin.y + outlineYOffset;

        GL.Begin(GL.LINES); // tell GL we are about to submit line segments
        GL.Color(outlineColor);

        // --- Vertical lines (run along the Z axis, spaced along X) ----------
        for (int x = 0; x <= columns; x++)
        {
            float worldX = origin.x + x * cellSize;

            // Line from z=0 edge to z=rows edge
            GL.Vertex3(worldX, y, origin.z);
            GL.Vertex3(worldX, y, origin.z + rows * cellSize);
        }

        // --- Horizontal lines (run along the X axis, spaced along Z) --------
        for (int z = 0; z <= rows; z++)
        {
            float worldZ = origin.z + z * cellSize;

            GL.Vertex3(origin.x,                    y, worldZ);
            GL.Vertex3(origin.x + columns * cellSize, y, worldZ);
        }

        GL.End(); // finalise the GL draw call
    }

    /* // -------------------------------------------------------------------------
    // Click / selection handling
    // -------------------------------------------------------------------------

    // -------------------------------------------------------------------------
// Input Action — declare at the top of the class alongside other fields
// -------------------------------------------------------------------------

// Listens for a left mouse button press using the new Input System.
// Created in code so no Input Action Asset is required.
private InputAction _clickAction;

// -------------------------------------------------------------------------
// In Awake() — set up and enable the action
// -------------------------------------------------------------------------
private void Awake()
{
    // Create a Button action bound to the left mouse button.
    // ActionType.Button means the callback fires once per press,
    // equivalent to the old Input.GetMouseButtonDown(0).
    _clickAction = new InputAction(
        name:    "GridClick",
        type:    InputActionType.Button,
        binding: "<Mouse>/leftButton"
    );

    // Subscribe the handler — called automatically by Unity on each press.
    // This replaces polling Input.GetMouseButtonDown inside Update().
    _clickAction.performed += OnGridClicked;

    // Actions must be explicitly enabled before they fire.
    _clickAction.Enable();
}

// -------------------------------------------------------------------------
// In OnDestroy() — clean up to avoid memory leaks
// -------------------------------------------------------------------------
private void OnDestroy()
{
    // Always unsubscribe and dispose when the object is destroyed.
    // Skipping this causes the callback to fire on a dead object.
    _clickAction.performed -= OnGridClicked;
    _clickAction.Disable();
    _clickAction.Dispose();
}

// -------------------------------------------------------------------------
// Remove the old Update() call to HandleClickInput() — delete this line:
//   private void Update() { HandleClickInput(); }
// Then replace HandleClickInput() entirely with the callback below.
// -------------------------------------------------------------------------

/// <summary>
/// Called by the Input System once each time the left mouse button is
/// pressed. Replaces the old HandleClickInput() polling method.
///
/// Mouse.current.position.ReadValue() is the new Input System equivalent
/// of Input.mousePosition — both return a screen-space Vector2.
/// </summary>
private void OnGridClicked(InputAction.CallbackContext ctx)
{
    // Read the current cursor position in screen space.
    // We read from Mouse.current rather than ctx because the action is
    // Button type and carries no positional value itself.
    Vector2 screenPos = Mouse.current.position.ReadValue();

    // Build a ray from the camera through the screen-space cursor position.
    // Identical to the old ScreenPointToRay(Input.mousePosition) call.
    Ray ray = Camera.main.ScreenPointToRay(screenPos);

    // Raycast against all colliders in the scene
    if (!Physics.Raycast(ray, out RaycastHit hit)) return;

    // Ignore hits on anything other than this grid's collider
    if (hit.collider.gameObject != gameObject &&
        hit.collider.transform.parent?.gameObject != gameObject)
        return;

    // Convert world-space hit point to a local grid cell coordinate.
    // Subtracting transform.position means this works even if the grid
    // GameObject is not sitting at the world origin.
    Vector3 localHit = hit.point - transform.position;

    int cellX = Mathf.FloorToInt(localHit.x / cellSize);
    int cellZ = Mathf.FloorToInt(localHit.z / cellSize);

    // Clamp to valid range — floating-point precision can place the hit
    // exactly on a grid edge, which would give an out-of-bounds index.
    cellX = Mathf.Clamp(cellX, 0, columns - 1);
    cellZ = Mathf.Clamp(cellZ, 0, rows    - 1);

    SelectCell(cellX, cellZ);
} */

    /// <summary>
    /// Highlights the given cell and deselects the previously selected one.
    /// Clicking the same cell a second time deselects it.
    /// </summary>
    public void SelectCell(int x, int z)
    {
        // Clicking the already-selected cell toggles it off
        bool clickedSameCell = (_selectedCell.x == x && _selectedCell.y == z);

        // Deselect the old cell — restore its original colour
        if (_selectedCell.x >= 0)
        {
            Color restore = _occupied[_selectedCell.x, _selectedCell.y]
                ? occupiedColor
                : _baseColors[_selectedCell.x, _selectedCell.y];

            SetCellVertexColor(_selectedCell.x, _selectedCell.y, restore);
        }

        if (clickedSameCell)
        {
            // Second click → deselect completely
            _selectedCell = new Vector2Int(-1, -1);
            return;
        }

        // Highlight the new cell
        _selectedCell = new Vector2Int(x, z);
        SetCellVertexColor(x, z, highlightColor);
    }

    // -------------------------------------------------------------------------
    // Public helpers used by PlantPlacer
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true if the cell is within the grid bounds and not already
    /// occupied by a plant.
    /// </summary>
    public bool IsCellAvailable(int x, int z)
    {
        if (x < 0 || x >= columns || z < 0 || z >= rows) return false;
        return !_occupied[x, z];
    }

    /// <summary>
    /// Marks a cell as occupied and colours it with occupiedColor.
    /// Called by PlantPlacer after successfully placing a plant.
    /// </summary>
    public void OccupyCell(int x, int z)
    {
        _occupied[x, z] = true;
        SetCellVertexColor(x, z, occupiedColor);

        // If this was the selected cell, deselect it — the plant is now there
        if (_selectedCell.x == x && _selectedCell.y == z)
            _selectedCell = new Vector2Int(-1, -1);
    }

    /// <summary>
    /// Marks a cell as free again and restores its checkerboard colour.
    /// Called by PlantPlacer when a plant is removed.
    /// </summary>
    public void FreeCell(int x, int z)
    {
        _occupied[x, z] = false;
        SetCellVertexColor(x, z, _baseColors[x, z]);
    }

    /// <summary>
    /// Returns the world-space centre position of a given cell.
    /// Used by PlantPlacer to know where to spawn the plant model.
    /// </summary>
    public Vector3 GetCellWorldCenter(int x, int z)
    {
        return transform.position + new Vector3(
            (x + 0.5f) * cellSize,   // centre X
            0f,                        // on the grid surface
            (z + 0.5f) * cellSize    // centre Z
        );
    }

    /// <summary>
    /// Exposes which cell is currently selected so PlantPlacer can read it.
    /// Returns (-1, -1) when nothing is selected.
    /// </summary>
    public Vector2Int SelectedCell => _selectedCell;

    // -------------------------------------------------------------------------
    // Internal colour helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sets the vertex colour for all four corners of a single cell and
    /// immediately uploads the colour array back to the GPU mesh buffer.
    ///
    /// The vertex index for a cell at grid position (x, z) is:
    ///   baseIndex = (z * columns + x) * 4
    /// because we use 4 non-shared vertices per cell (see BuildGrid).
    /// </summary>
    private void SetCellVertexColor(int x, int z, Color color)
    {
        int baseIndex = (z * columns + x) * 4;

        _vertexColors[baseIndex]     = color;
        _vertexColors[baseIndex + 1] = color;
        _vertexColors[baseIndex + 2] = color;
        _vertexColors[baseIndex + 3] = color;

        // Push the updated colour array back to the mesh on the GPU.
        // This is a full-array upload every time; for very large grids consider
        // using Mesh.SetColors with a NativeArray for better performance.
        _mesh.colors = _vertexColors;
    }

    // -------------------------------------------------------------------------
    // Collider auto-setup (called by PlantPlacer on Awake)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds (or resizes) a BoxCollider that exactly covers the grid surface.
    /// Without a collider, Physics.Raycast will never hit this GameObject
    /// and click detection will not work.
    /// </summary>
    public void SetupCollider()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();

        // The mesh sits on Y = 0; give the collider a small height so the ray
        // can intersect it even when cast at a slight downward angle.
        col.center = new Vector3(
            columns * cellSize * 0.5f,
            0f,
            rows    * cellSize * 0.5f
        );
        col.size = new Vector3(
            columns * cellSize,
            0.05f,              // thin slab
            rows    * cellSize
        );
    }
}

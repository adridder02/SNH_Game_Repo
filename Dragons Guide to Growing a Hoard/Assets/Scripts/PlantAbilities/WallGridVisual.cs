using UnityEngine;

// =============================================================
// WallGridVisual.cs
// -------------------------------------------------------------
// Direct counterpart to GridVisual.cs, same colour scheme and same
// GL-line + per-cell-quad approach, just oriented to a WallSurface's
// vertical plane (facing world Z or world X) instead of the floor
// grid's horizontal XZ plane. See WallSurface.cs for the axis-aligned
// assumption this shares with the floor grid system.
// =============================================================
public class WallGridVisual : MonoBehaviour
{
    private static readonly Color ColorIdle = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color ColorGrid = new Color(1f, 1f, 1f, 0.6f);
    private static readonly Color ColorValid = new Color(0.2f, 0.9f, 0.3f, 0.55f);
    private static readonly Color ColorInvalid = new Color(0.95f, 0.2f, 0.2f, 0.55f);
    private static readonly Color ColorOccupied = new Color(0.5f, 0.5f, 0.55f, 0.4f);

    public enum CellState { Idle, Valid, Invalid, Occupied }

    [SerializeField] private Material gridMaterial;

    private Vector2Int dimensions;
    private float cellSize;
    private Vector3 originWorld;
    private WallSurface.FacingAxis facing;
    private CellState[] cellStates;
    private GameObject[] cellQuads;

    private Material matIdle, matValid, matInvalid, matOccupied, matGL;

    public void Initialise(Vector2Int dims, float size, Vector3 origin, WallSurface.FacingAxis facingAxis)
    {
        dimensions = dims;
        cellSize = size;
        originWorld = origin;
        facing = facingAxis;
        cellStates = new CellState[dims.x * dims.y];

        BuildMaterials();
        BuildCellQuads();
    }

    private void BuildMaterials()
    {
        Shader s = Shader.Find("GUI/Text Shader") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");

        matIdle = MakeMat(s, ColorIdle);
        matValid = MakeMat(s, ColorValid);
        matInvalid = MakeMat(s, ColorInvalid);
        matOccupied = MakeMat(s, ColorOccupied);

        matGL = gridMaterial != null
            ? gridMaterial
            : new Material(Shader.Find("Hidden/Internal-Colored") ?? s) { hideFlags = HideFlags.HideAndDontSave };
        matGL.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        matGL.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        matGL.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        matGL.SetInt("_ZWrite", 0);
    }

    private static Material MakeMat(Shader s, Color c)
    {
        var m = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
        m.color = c;
        m.SetFloat("_Mode", 3);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.renderQueue = 3000;
        return m;
    }

    private void BuildCellQuads()
    {
        int count = dimensions.x * dimensions.y;
        cellQuads = new GameObject[count];

        float inset = cellSize * 0.04f;
        float quadSize = cellSize - inset * 2f;
        Quaternion faceRot = facing == WallSurface.FacingAxis.FacesZ
            ? Quaternion.identity                    // quad's own normal (+Z) already faces along world Z
            : Quaternion.Euler(0f, 90f, 0f);          // rotate to face along world X instead

        for (int v = 0; v < dimensions.y; v++)
        {
            for (int h = 0; h < dimensions.x; h++)
            {
                int idx = v * dimensions.x + h;

                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = $"WallCell_{h}_{v}";
                go.transform.SetParent(transform, worldPositionStays: false);
                Destroy(go.GetComponent<Collider>());

                go.transform.position = CellCentre(h, v);
                go.transform.rotation = faceRot;
                go.transform.localScale = new Vector3(quadSize, quadSize, 1f);

                go.GetComponent<MeshRenderer>().sharedMaterial = matIdle;
                cellQuads[idx] = go;
            }
        }
    }

    private Vector3 CellCentre(int h, int v)
    {
        float horiz = h * cellSize + cellSize * 0.5f;
        float vert = v * cellSize + cellSize * 0.5f;

        return facing == WallSurface.FacingAxis.FacesZ
            ? new Vector3(originWorld.x + horiz, originWorld.y + vert, originWorld.z)
            : new Vector3(originWorld.x, originWorld.y + vert, originWorld.z + horiz);
    }

    private void OnRenderObject()
    {
        if (!gameObject.activeSelf) return;

        matGL.SetPass(0);
        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);
        GL.Begin(GL.LINES);
        GL.Color(ColorGrid);

        float totalH = dimensions.x * cellSize;
        float totalV = dimensions.y * cellSize;

        for (int h = 0; h <= dimensions.x; h++)
        {
            Vector3 a = LinePoint(h * cellSize, 0f);
            Vector3 b = LinePoint(h * cellSize, totalV);
            GL.Vertex3(a.x, a.y, a.z);
            GL.Vertex3(b.x, b.y, b.z);
        }
        for (int v = 0; v <= dimensions.y; v++)
        {
            Vector3 a = LinePoint(0f, v * cellSize);
            Vector3 b = LinePoint(totalH, v * cellSize);
            GL.Vertex3(a.x, a.y, a.z);
            GL.Vertex3(b.x, b.y, b.z);
        }

        GL.End();
        GL.PopMatrix();
    }

    private Vector3 LinePoint(float horiz, float vert)
    {
        return facing == WallSurface.FacingAxis.FacesZ
            ? new Vector3(originWorld.x + horiz, originWorld.y + vert, originWorld.z + 0.002f)
            : new Vector3(originWorld.x + 0.002f, originWorld.y + vert, originWorld.z + horiz);
    }

    // ---------------------------------------------------------------
    public void SetVisible(bool visible) => gameObject.SetActive(visible);
    public void SetMaterial(Material mat) { if (mat != null) gridMaterial = mat; }

    public void SetCellState(int h, int v, CellState state)
    {
        if (!InBounds(h, v)) return;
        int idx = v * dimensions.x + h;
        cellStates[idx] = state;
        cellQuads[idx].GetComponent<MeshRenderer>().sharedMaterial = StateToMat(state);
    }

    public void SetFootprint(Vector2Int origin, Vector2Int size, CellState state)
    {
        for (int dx = 0; dx < size.x; dx++)
            for (int dv = 0; dv < size.y; dv++)
                SetCellState(origin.x + dx, origin.y + dv, state);
    }

    public void ClearHover()
    {
        for (int i = 0; i < cellStates.Length; i++)
        {
            if (cellStates[i] == CellState.Valid || cellStates[i] == CellState.Invalid)
            {
                cellStates[i] = CellState.Idle;
                cellQuads[i].GetComponent<MeshRenderer>().sharedMaterial = matIdle;
            }
        }
    }

    public void ClearFootprint(Vector2Int origin, Vector2Int size) => SetFootprint(origin, size, CellState.Idle);
    public void MarkOccupied(Vector2Int origin, Vector2Int size) => SetFootprint(origin, size, CellState.Occupied);

    public bool WorldToCell(Vector3 worldPos, out Vector2Int cell)
    {
        float horiz = facing == WallSurface.FacingAxis.FacesZ ? worldPos.x - originWorld.x : worldPos.z - originWorld.z;
        float vert = worldPos.y - originWorld.y;

        int h = Mathf.FloorToInt(horiz / cellSize);
        int v = Mathf.FloorToInt(vert / cellSize);
        cell = new Vector2Int(h, v);
        return InBounds(h, v);
    }

    public bool InBounds(int h, int v) => h >= 0 && h < dimensions.x && v >= 0 && v < dimensions.y;

    public bool FootprintInBounds(Vector2Int origin, Vector2Int size) =>
        origin.x >= 0 && origin.y >= 0 &&
        origin.x + size.x <= dimensions.x &&
        origin.y + size.y <= dimensions.y;

    public Vector3 GetCellCenter(Vector2Int cell) => InBounds(cell.x, cell.y) ? CellCentre(cell.x, cell.y) : Vector3.zero;

    private Material StateToMat(CellState s) => s switch
    {
        CellState.Valid => matValid,
        CellState.Invalid => matInvalid,
        CellState.Occupied => matOccupied,
        _ => matIdle,
    };
}

using System.Collections.Generic;
using UnityEngine;

// Draws the grid using GL lines (always visible, no material needed) and
// per-cell highlight quads using simple solid-colour materials that work
// in both Built-in and URP with no manual material setup cuase idk what 
// we're doing about those things yet on the actual assets

public class GridVisual : MonoBehaviour
{
    // * Colours
    private static readonly Color ColorIdle = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color ColorGrid = new Color(1f, 1f, 1f, 0.6f);
    private static readonly Color ColorValid = new Color(0.2f, 0.9f, 0.3f, 0.55f);
    private static readonly Color ColorInvalid = new Color(0.95f, 0.2f, 0.2f, 0.55f);
    private static readonly Color ColorOccupied = new Color(0.5f, 0.5f, 0.55f, 0.4f);

    public enum CellState { Idle, Valid, Invalid, Occupied }

    [SerializeField] private Material gridMaterial; // optional override

    private Vector2Int dimensions;
    private float cellSize;
    private Vector3 originWorld;
    private CellState[] cellStates;

    // one quad GameObject per cell for colour highlighting
    private GameObject[] cellQuads;

    // shared materials (created once)
    private Material matIdle;
    private Material matValid;
    private Material matInvalid;
    private Material matOccupied;
    private Material matGL;     // for GL line drawing

    // * Setup 

    public void Initialise(Vector2Int dims, float size, Vector3 origin)
    {
        dimensions = dims;
        cellSize = size;
        originWorld = origin;
        cellStates = new CellState[dims.x * dims.y];

        BuildMaterials();
        BuildCellQuads();
    }

    private void BuildMaterials()
    {
        // shader is always available, supports transparency, ignores lighting
        // works in Built in and URP without any setup (I dont know yet what exactly we're gonna use for the actual assets)
        Shader s = Shader.Find("GUI/Text Shader")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Standard");

        matIdle = MakeMat(s, ColorIdle);
        matValid = MakeMat(s, ColorValid);
        matInvalid = MakeMat(s, ColorInvalid);
        matOccupied = MakeMat(s, ColorOccupied);

        // GL lines material
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
        // force transparency on Standard if that's all that was found
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

        for (int z = 0; z < dimensions.y; z++)
        {
            for (int x = 0; x < dimensions.x; x++)
            {
                int idx = z * dimensions.x + x;

                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = $"Cell_{x}_{z}";
                go.transform.SetParent(transform, worldPositionStays: false);

                // destroy the collider ( otherswise it interfears with raycasts)
                Destroy(go.GetComponent<Collider>());

                // position should be centre of this cell, flat on XZ plane
                Vector3 cellCentre = originWorld + new Vector3(
                    x * cellSize + cellSize * 0.5f,
                    0f,
                    z * cellSize + cellSize * 0.5f);

                go.transform.position = cellCentre;
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // face up
                go.transform.localScale = new Vector3(quadSize, quadSize, 1f);

                go.GetComponent<MeshRenderer>().sharedMaterial = matIdle;
                cellQuads[idx] = go;
            }
        }
    }

    // * GL line rendering 

    private void OnRenderObject()
    {
        if (!gameObject.activeSelf) return;

        matGL.SetPass(0);
        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);
        GL.Begin(GL.LINES);
        GL.Color(ColorGrid);

        float totalW = dimensions.x * cellSize;
        float totalD = dimensions.y * cellSize;
        float y = originWorld.y + 0.002f;

        // vertical lines
        for (int x = 0; x <= dimensions.x; x++)
        {
            float lx = originWorld.x + x * cellSize;
            GL.Vertex3(lx, y, originWorld.z);
            GL.Vertex3(lx, y, originWorld.z + totalD);
        }

        // horizontal lines
        for (int z = 0; z <= dimensions.y; z++)
        {
            float lz = originWorld.z + z * cellSize;
            GL.Vertex3(originWorld.x, y, lz);
            GL.Vertex3(originWorld.x + totalW, y, lz);
        }

        GL.End();
        GL.PopMatrix();
    }

    // * Public API 

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public void SetMaterial(Material mat)
    {
        if (mat != null) gridMaterial = mat;
    }

    public void SetCellState(int x, int z, CellState state)
    {
        if (!InBounds(x, z)) return;
        int idx = z * dimensions.x + x;
        cellStates[idx] = state;
        cellQuads[idx].GetComponent<MeshRenderer>().sharedMaterial = StateToMat(state);
    }

    public void SetFootprint(Vector2Int origin, Vector2Int size, CellState state)
    {
        for (int dx = 0; dx < size.x; dx++)
            for (int dz = 0; dz < size.y; dz++)
                SetCellState(origin.x + dx, origin.y + dz, state);
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

    // resets a section of cells back to idle after removing or picking up a pot
    public void ClearFootprint(Vector2Int origin, Vector2Int size)
    {
        SetFootprint(origin, size, CellState.Idle);
    }

    public void MarkOccupied(Vector2Int origin, Vector2Int size)
    {
        SetFootprint(origin, size, CellState.Occupied);
    }

    public bool WorldToCell(Vector3 worldPos, out Vector2Int cell)
    {
        float lx = worldPos.x - originWorld.x;
        float lz = worldPos.z - originWorld.z;

        int cx = Mathf.FloorToInt(lx / cellSize);
        int cz = Mathf.FloorToInt(lz / cellSize);

        cell = new Vector2Int(cx, cz);
        return InBounds(cx, cz);
    }

    public bool InBounds(int x, int z) =>
        x >= 0 && x < dimensions.x && z >= 0 && z < dimensions.y;

    public bool InBounds(Vector2Int c) => InBounds(c.x, c.y);

    public bool FootprintInBounds(Vector2Int origin, Vector2Int size)
    {
        return InBounds(origin.x, origin.y) &&
               InBounds(origin.x + size.x - 1, origin.y + size.y - 1);
    }

    // * Helpers 

    private Material StateToMat(CellState s) => s switch
    {
        CellState.Valid => matValid,
        CellState.Invalid => matInvalid,
        CellState.Occupied => matOccupied,
        _ => matIdle,
    };
}
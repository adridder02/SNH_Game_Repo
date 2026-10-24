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
    private static readonly Color ColorValid = new Color(0.2f, 0.9f, 0.3f, 0.3f);
    private static readonly Color ColorInvalid = new Color(0.95f, 0.2f, 0.2f, 0.3f);
    private static readonly Color ColorOccupied = new Color(0.5f, 0.5f, 0.55f, 0.4f);

    public enum CellState { Idle, Valid, Invalid, Occupied }

    [SerializeField] private Material gridMaterial; // optional override

    private Vector2Int dimensions;
    private float cellSize;
    private Vector3 originWorld;
    private static readonly Color ColorGridLocked = new Color(0.75f, 0.75f, 0.85f, 0.18f);
    private bool dimmed;
    private Vector3 axisRight = Vector3.right;
    private Vector3 axisForward = Vector3.forward;
    private float yaw;
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
        Initialise(dims, size, origin, Vector3.right, Vector3.forward);
    }

    /// <summary>right/forward are the grid's X/Z axes in world space (a rotated surface passes its own).</summary>
    public void Initialise(Vector2Int dims, float size, Vector3 origin, Vector3 right, Vector3 forward)
    {
        axisRight = right;
        axisForward = forward;
        yaw = Mathf.Atan2(-right.z, right.x) * Mathf.Rad2Deg;
        dimensions = dims;
        cellSize = size;
        originWorld = origin;
        cellStates = new CellState[dims.x * dims.y];

        BuildMaterials();
        BuildCellQuads();
        // NOTE: this used to advance the old on-screen Tutorial instruction text here
        // (Tutorial.setGridOnTable()) — not a checklist task, nothing to repoint it to
        // yet since that on-screen system hasn't been rebuilt.
    }

    private void BuildMaterials()
    {
        // shader is always available, supports transparency, ignores lighting
        // works in Built in and URP without any setup (I dont know yet what exactly we're gonna use for the actual assets)
        // "Sprites/Default" first: it is transparent and unlit like GUI/Text Shader, but it depth-tests, so
        // the grid sits UNDER the dragon, plants and pots in front of it instead of drawing over them.
        Shader s = Shader.Find("Sprites/Default")
                ?? Shader.Find("GUI/Text Shader")
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
        if (matGL.HasProperty("_ZTest")) matGL.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
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
        // Just BEFORE the standard transparent queue (3000) so the placement preview/ghost (pushed to 3100 by
        // PlacementSystem) always draws on top of the highlight instead of being washed out by it.
        m.renderQueue = 2990;
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
                go.transform.SetParent(transform, worldPositionStays: false);//BK changed this to false

                // destroy the collider ( otherswise it interfears with raycasts)
                Destroy(go.GetComponent<Collider>());

                // position should be centre of this cell, flat on XZ plane
                Vector3 cellCentre = CellPoint(x + 0.5f, z + 0.5f);

                go.transform.position = cellCentre;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f); // face up, aligned to the grid
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
        GL.Color(dimmed ? ColorGridLocked : ColorGrid);

        Vector3 lift = Vector3.up * 0.002f;

        // lines along the grid's Z axis
        for (int x = 0; x <= dimensions.x; x++)
        {
            GL.Vertex(CellPoint(x, 0f) + lift);
            GL.Vertex(CellPoint(x, dimensions.y) + lift);
        }

        // lines along the grid's X axis
        for (int z = 0; z <= dimensions.y; z++)
        {
            GL.Vertex(CellPoint(0f, z) + lift);
            GL.Vertex(CellPoint(dimensions.x, z) + lift);
        }

        GL.End();
        GL.PopMatrix();
    }

    // World position (at grid height) of a point given in cell units from the grid origin.
    private Vector3 CellPoint(float cx, float cz)
    {
        return originWorld + axisRight * (cx * cellSize) + axisForward * (cz * cellSize);
    }

    // * Public API 

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    /// <summary>Locked surface: faint grid lines and no cell fills, so it reads as unavailable.</summary>
    public void SetDimmed(bool value)
    {
        dimmed = value;
        if (cellQuads == null) return;
        foreach (GameObject q in cellQuads)
            if (q != null) q.GetComponent<MeshRenderer>().enabled = !value;
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
        float epsilon = 0.001f; // Small tolerance for edge cases
        
        Vector3 d = worldPos - originWorld;
        float lx = Vector3.Dot(d, axisRight);
        float lz = Vector3.Dot(d, axisForward);
        
        // Add epsilon to help with floating point precision at edges
        int cx = Mathf.FloorToInt((lx + epsilon) / cellSize);
        int cz = Mathf.FloorToInt((lz + epsilon) / cellSize);
        
        cell = new Vector2Int(cx, cz);
        
        // Check if within bounds with tolerance
        if (InBounds(cx, cz))
        {
            return true;
        }
        
        // If slightly out of bounds, try to clamp to nearest valid cell
        // This happens when the mouse is exactly at the edge
        if (cx == -1 || cx == dimensions.x || cz == -1 || cz == dimensions.y)
        {
            // Clamp to valid range
            cx = Mathf.Clamp(cx, 0, dimensions.x - 1);
            cz = Mathf.Clamp(cz, 0, dimensions.y - 1);
            cell = new Vector2Int(cx, cz);
            
            // Check if this clamped position is actually close to the mouse
            Vector3 cellCenter = CellPoint(cx + 0.5f, cz + 0.5f);
            
            float distance = Vector3.Distance(
                new Vector3(worldPos.x, 0, worldPos.z),
                new Vector3(cellCenter.x, 0, cellCenter.z));
            
            // If the mouse is within half a cell of this clamped position, it's valid
            if (distance <= cellSize * 0.6f)
                return true;
        }
        
        return false;
    }

    public bool InBounds(int x, int z) =>
        x >= 0 && x < dimensions.x && z >= 0 && z < dimensions.y;

    public bool InBounds(Vector2Int c) => InBounds(c.x, c.y);

   public bool FootprintInBounds(Vector2Int origin, Vector2Int size)
    {
        // Add tolerance for edge cases
        float epsilon = 0.001f;
        
        // Check if the footprint is within bounds with tolerance
        // The footprint must be completely inside the grid
        bool valid = origin.x >= -epsilon && 
                    origin.y >= -epsilon &&
                    origin.x + size.x <= dimensions.x + epsilon &&
                    origin.y + size.y <= dimensions.y + epsilon;
        
        if (!valid)
            return false;
        
        // Double-check with integer bounds (stricter check)
        // This ensures we don't place things that are partially off the grid
        if (origin.x < 0 || origin.y < 0 ||
            origin.x + size.x > dimensions.x ||
            origin.y + size.y > dimensions.y)
        {
            // If it's only off by a small amount due to floating point, clamp it
            if (origin.x < 0 && origin.x > -0.5f) 
                return false; // Too far off, reject
            if (origin.y < 0 && origin.y > -0.5f)
                return false;
            if (origin.x + size.x > dimensions.x && origin.x + size.x < dimensions.x + 0.5f)
                return false;
            if (origin.y + size.y > dimensions.y && origin.y + size.y < dimensions.y + 0.5f)
                return false;
            
            return false;
        }
        
        return true;
    }

    // * Helpers 
    public Vector3 GetCellCenter(Vector2Int cell)
    {
        if (!InBounds(cell.x, cell.y))
            return Vector3.zero;
        
        return CellPoint(cell.x + 0.5f, cell.y + 0.5f);
    }
    private Material StateToMat(CellState s) => s switch
    {
        CellState.Valid => matValid,
        CellState.Invalid => matInvalid,
        CellState.Occupied => matOccupied,
        _ => matIdle,
    };
}
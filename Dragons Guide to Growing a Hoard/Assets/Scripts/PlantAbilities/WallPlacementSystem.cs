using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// =============================================================
// WallPlacementSystem.cs
// -------------------------------------------------------------
// Standalone companion to PlacementSystem, for wall-mounted objects
// (Clovenwick) instead of pots. Deliberately NOT sharing PlacementSystem's
// GridData — a wall grid and the floor pot grid are physically different
// surfaces, so there's no overlap risk to guard against the way
// AbilityPlacementSystem has to for floor-grid placeables.
//
// Same hover/place/remove shape as PlacementSystem, trimmed down (no
// audio, no missions, no move-mode) since Clovenwick genuinely "doesn't
// have abilities per say" — just needs somewhere to sit.
// =============================================================
public class WallPlacementSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputManager inputManager;
    [SerializeField] private List<WallSurface> wallSurfaces = new List<WallSurface>();

    [Header("Mushroom Types")]
    [SerializeField] private List<WallMushroomData> availableMushrooms;

    [Header("Preview")]
    [SerializeField] private bool showPreviewObject = true;

    public enum Mode { None, Placing, Removing }
    private Mode mode = Mode.None;
    public Mode CurrentMode => mode;

    private readonly Dictionary<WallSurface, GridData> surfaceGridData = new Dictionary<WallSurface, GridData>();
    private WallSurface activeSurface;
    private int selectedIndex = 0;
    private Vector2Int lastHoveredCell = new Vector2Int(-999, -999);
    private GameObject previewObject;

    public bool IsActive => mode != Mode.None;

    private void Start()
    {
        foreach (WallSurface surface in wallSurfaces)
            if (surface != null) surfaceGridData[surface] = new GridData();
    }

    public void ToggleMushroomPlaceMode(int index)
    {
        if (mode == Mode.Placing) { CancelMode(); return; }
        if (availableMushrooms == null || index < 0 || index >= availableMushrooms.Count) return;

        CancelMode();
        selectedIndex = index;
        mode = Mode.Placing;

        foreach (WallSurface s in wallSurfaces) s?.GridVisual?.SetVisible(true);
        SpawnPreview(availableMushrooms[selectedIndex]);
    }

    public void ToggleRemoveMode()
    {
        if (mode == Mode.Removing) { CancelMode(); return; }

        CancelMode();
        mode = Mode.Removing;
        foreach (WallSurface s in wallSurfaces) s?.GridVisual?.SetVisible(true);
    }

    public void CancelMode()
    {
        mode = Mode.None;
        foreach (WallSurface s in wallSurfaces)
        {
            s?.GridVisual?.ClearHover();
            s?.GridVisual?.SetVisible(false);
        }
        DestroyPreview();
        activeSurface = null;
        lastHoveredCell = new Vector2Int(-999, -999);
    }

    private void Update()
    {
        if (mode == Mode.None || inputManager == null) return;

        Vector3 mouseWorld = inputManager.GetSelectedWallPosition();
        WallSurface hovered = GetSurfaceAtPosition(mouseWorld);

        if (hovered != activeSurface)
        {
            activeSurface?.GridVisual?.ClearHover();
            activeSurface = hovered;
            lastHoveredCell = new Vector2Int(-999, -999);
        }

        if (activeSurface == null) { SetPreviewVisible(false); return; }

        WallGridVisual gridVisual = activeSurface.GridVisual;
        GridData gridData = surfaceGridData[activeSurface];

        if (!gridVisual.WorldToCell(mouseWorld, out Vector2Int cell))
        {
            gridVisual.ClearHover();
            SetPreviewVisible(false);
            return;
        }

        if (cell != lastHoveredCell)
        {
            lastHoveredCell = cell;
            UpdateHoverVisual(cell, gridVisual, gridData);
        }

        if (previewObject != null && mode == Mode.Placing)
            previewObject.transform.position = activeSurface.CellToWorldCentre(cell, availableMushrooms[selectedIndex].size);

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (mode == Mode.Placing) TryPlace(cell, gridData, gridVisual);
            else if (mode == Mode.Removing) TryRemove(cell, gridData, gridVisual);
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
            CancelMode();
    }

    private void UpdateHoverVisual(Vector2Int cell, WallGridVisual gridVisual, GridData gridData)
    {
        gridVisual.ClearHover();

        if (mode == Mode.Placing)
        {
            WallMushroomData data = availableMushrooms[selectedIndex];
            bool canFit = gridVisual.FootprintInBounds(cell, data.size);
            bool canPlace = canFit && gridData.CanPlace(ToVec3(cell), data.size);
            gridVisual.SetFootprint(cell, data.size, canPlace ? WallGridVisual.CellState.Valid : WallGridVisual.CellState.Invalid);
            SetPreviewVisible(true);
        }
        else if (mode == Mode.Removing)
        {
            PlacementData data = gridData.GetPlacement(ToVec3(cell));
            if (data != null)
            {
                Vector2Int origin = new Vector2Int(data.Origin.x, data.Origin.z);
                gridVisual.SetFootprint(origin, data.Size, WallGridVisual.CellState.Invalid);
            }
        }
    }

    private void TryPlace(Vector2Int cell, GridData gridData, WallGridVisual gridVisual)
    {
        WallMushroomData data = availableMushrooms[selectedIndex];
        if (!gridVisual.FootprintInBounds(cell, data.size)) return;

        Vector3Int key = ToVec3(cell);
        if (!gridData.CanPlace(key, data.size)) return;

        Vector3 worldPos = activeSurface.CellToWorldCentre(cell, data.size);

        GameObject prefab = data.prefab != null ? data.prefab : null;
        GameObject placed = prefab != null
            ? Instantiate(prefab, worldPos, activeSurface.MountRotation)
            : new GameObject(data.displayName);

        placed.transform.position = worldPos;
        placed.transform.rotation = activeSurface.MountRotation;

        ClovenwickWallMount mount = placed.GetComponent<ClovenwickWallMount>();
        if (mount == null) mount = placed.AddComponent<ClovenwickWallMount>();
        mount.maxWeight = data.maxSupportedWeightKg > 12f ? ClovenwickWallMount.WeightClass.Medium : ClovenwickWallMount.WeightClass.Small;

        gridData.AddPlacement(key, data.size, placed);
        gridVisual.MarkOccupied(cell, data.size);
    }

    private void TryRemove(Vector2Int cell, GridData gridData, WallGridVisual gridVisual)
    {
        PlacementData data = gridData.GetPlacement(ToVec3(cell));
        if (data == null) return;

        Vector2Int origin = new Vector2Int(data.Origin.x, data.Origin.z);
        gridData.RemovePlacement(data.Origin);
        gridVisual.ClearFootprint(origin, data.Size);

        if (data.PlacedObject != null) Destroy(data.PlacedObject);
    }

    // ---------------------------------------------------------------
    private WallSurface GetSurfaceAtPosition(Vector3 worldPos)
    {
        WallSurface closest = null;
        float closestDepthDist = float.MaxValue;

        foreach (WallSurface surface in wallSurfaces)
        {
            if (surface == null || surface.GridVisual == null) continue;

            Vector3 origin = surface.GridOriginWorld;
            Vector2Int dims = surface.GridDimensions;
            float cs = surface.CellSize;

            bool inBounds;
            float depthDist;

            if (surface.Facing == WallSurface.FacingAxis.FacesZ)
            {
                inBounds = worldPos.x >= origin.x - 0.05f && worldPos.x <= origin.x + dims.x * cs + 0.05f &&
                           worldPos.y >= origin.y - 0.05f && worldPos.y <= origin.y + dims.y * cs + 0.05f;
                depthDist = Mathf.Abs(worldPos.z - origin.z);
            }
            else
            {
                inBounds = worldPos.z >= origin.z - 0.05f && worldPos.z <= origin.z + dims.x * cs + 0.05f &&
                           worldPos.y >= origin.y - 0.05f && worldPos.y <= origin.y + dims.y * cs + 0.05f;
                depthDist = Mathf.Abs(worldPos.x - origin.x);
            }

            if (inBounds && depthDist < closestDepthDist)
            {
                closestDepthDist = depthDist;
                closest = surface;
            }
        }
        return closest;
    }

    private void SpawnPreview(WallMushroomData data)
    {
        DestroyPreview();
        if (!showPreviewObject) return;

        GameObject prefab = data.previewPrefab != null ? data.previewPrefab : data.prefab;
        if (prefab == null) return;

        previewObject = Instantiate(prefab);
        foreach (Collider c in previewObject.GetComponentsInChildren<Collider>()) c.enabled = false;
        previewObject.SetActive(true);
    }

    private void SetPreviewVisible(bool visible) { if (previewObject != null) previewObject.SetActive(visible); }
    private void DestroyPreview() { if (previewObject != null) Destroy(previewObject); previewObject = null; }

    private static Vector3Int ToVec3(Vector2Int c) => new Vector3Int(c.x, 0, c.y);
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// F to enter/exit placement mode. Right click will also exit. Scroll to switch between pots
// G to enter grab and move and left click to confirm
// X to enter remove mode and left click to confirm
public class PlacementSystem : MonoBehaviour
{
    // * Inspector 
    [Header("References")]
    [SerializeField] private InputManager inputManager;
    [SerializeField] private GreenhouseSurface surface;

    [Header("Pot types (1x1, 2x1, 2x2 …)")]
    [SerializeField] private List<PotData> availablePots;

    [Header("Preview")]
    [SerializeField] private bool showPreviewObject = true;

    // * Placement Mode 
    private enum Mode { None, Placing, Removing, Moving }
    private Mode mode = Mode.None;

    // other vars
    private GridData gridData = new GridData();
    private GridVisual gridVisual;
    private int selectedIndex = 0;
    private Vector2Int lastHoveredCell = new Vector2Int(-999, -999);
    private GameObject previewObject;

    private PlacementData movingData;
    private GameObject movingObject;


    private void Start()
    {
        if (surface == null) { Debug.LogError("PlacementSystem: No GreenhouseSurface assigned."); return; }
        gridVisual = surface.GridVisual;
        if (availablePots == null || availablePots.Count == 0) { Debug.LogError("PlacementSystem: No pot types."); return; }
    }

    private void Update()
    {
        HandleModeToggleKeys();
        if (mode == Mode.None) return;

        // scroll only in place mode — consume the event so camera zoom doesn't also fire
        if (mode == Mode.Placing)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                if (scroll > 0) CycleSelection(1);
                else            CycleSelection(-1);
                // Don't return — still need to update hover this frame.
            }
        }

        Vector3 mouseWorld = inputManager.GetSelectedMapPosition();

        if (!gridVisual.WorldToCell(mouseWorld, out Vector2Int hoveredCell))
        {
            gridVisual.ClearHover();
            SetPreviewVisible(false);
            lastHoveredCell = new Vector2Int(-999, -999);
            return;
        }

        if (hoveredCell != lastHoveredCell)
        {
            lastHoveredCell = hoveredCell;
            UpdateHoverVisual(hoveredCell);
        }

        // move preview every frame for smooth motion
        if (showPreviewObject && previewObject != null)
            previewObject.transform.position = CellToWorldCentre(hoveredCell,
                mode == Mode.Moving ? movingData.Size : availablePots[selectedIndex].size);

        // left click action
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (mode == Mode.Placing) TryPlace(hoveredCell);
            else if (mode == Mode.Removing) TryRemove(hoveredCell);
            else if (mode == Mode.Moving) TryPickupOrDrop(hoveredCell);
        }

        // right click cancel
        if (Mouse.current.rightButton.wasPressedThisFrame) CancelMode();
    }

    // * Key handling 

    private void HandleModeToggleKeys()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (mode == Mode.Placing) CancelMode();
            else EnterPlaceMode(selectedIndex);
        }

        if (Keyboard.current.xKey.wasPressedThisFrame)
        {
            if (mode == Mode.Removing) CancelMode();
            else EnterRemoveMode();
        }

        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            if (mode == Mode.Moving) CancelMode();
            else EnterMoveMode();
        }
    }

    // * entrer / exit placement mode

    public void EnterPlaceMode(int potIndex)
    {
        if (potIndex < 0 || potIndex >= availablePots.Count) return;
        CancelMode();
        selectedIndex = potIndex;
        mode = Mode.Placing;
        gridVisual.SetVisible(true);
        lastHoveredCell = new Vector2Int(-999, -999);
        SpawnPreview(availablePots[selectedIndex]);
    }

    private void EnterRemoveMode()
    {
        CancelMode();
        mode = Mode.Removing;
        gridVisual.SetVisible(true);
        lastHoveredCell = new Vector2Int(-999, -999);
    }

    private void EnterMoveMode()
    {
        CancelMode();
        mode = Mode.Moving;
        gridVisual.SetVisible(true);
        lastHoveredCell = new Vector2Int(-999, -999);
        // movingData is null until the player clicks a pot to pick up
    }

    private void CancelMode()
    {
        // ff mid move, put the pot back
        if (mode == Mode.Moving && movingData != null)
            PutMovingPotBack();

        mode = Mode.None;
        movingData = null;
        movingObject = null;
        gridVisual?.ClearHover();
        gridVisual?.SetVisible(false);
        DestroyPreview();
    }

    // * Hover visuals 

    private void UpdateHoverVisual(Vector2Int cell)
    {
        gridVisual.ClearHover();

        switch (mode)
        {
            case Mode.Placing:
                {
                    PotData data = availablePots[selectedIndex];
                    bool canFit = gridVisual.FootprintInBounds(cell, data.size);
                    bool canPlace = canFit && gridData.CanPlace(ToGridVec3(cell), data.size);
                    gridVisual.SetFootprint(cell, data.size,
                        canPlace ? GridVisual.CellState.Valid : GridVisual.CellState.Invalid);
                    SetPreviewVisible(true);
                    break;
                }

            case Mode.Removing:
                {
                    // highlight the whole section of whatever pot is under the cursor
                    PlacementData data = gridData.GetPlacement(ToGridVec3(cell));
                    if (data != null)
                    {
                        Vector2Int origin = new Vector2Int(data.Origin.x, data.Origin.z);
                        gridVisual.SetFootprint(origin, data.Size, GridVisual.CellState.Invalid);
                    }
                    break;
                }

            case Mode.Moving when movingData == null:
                {
                    // if waiting to pick up highlight pot under cursor
                    PlacementData data = gridData.GetPlacement(ToGridVec3(cell));
                    if (data != null)
                    {
                        Vector2Int origin = new Vector2Int(data.Origin.x, data.Origin.z);
                        gridVisual.SetFootprint(origin, data.Size, GridVisual.CellState.Valid);
                    }
                    break;
                }

            case Mode.Moving when movingData != null:
                {
                    // if carrying a pot show where it can be dropped
                    bool canFit = gridVisual.FootprintInBounds(cell, movingData.Size);
                    bool canPlace = canFit && gridData.CanPlace(ToGridVec3(cell), movingData.Size);
                    gridVisual.SetFootprint(cell, movingData.Size,
                        canPlace ? GridVisual.CellState.Valid : GridVisual.CellState.Invalid);
                    SetPreviewVisible(true);
                    break;
                }
        }
    }

    // * Action types

    private void TryPlace(Vector2Int cell)
    {
        PotData data = availablePots[selectedIndex];
        if (!gridVisual.FootprintInBounds(cell, data.size)) return;

        Vector3Int gridKey = ToGridVec3(cell);
        if (!gridData.CanPlace(gridKey, data.size)) return;
        if (data.potPrefab == null) return;

        GameObject placed = Instantiate(data.potPrefab,
            CellToWorldCentre(cell, data.size), Quaternion.identity);
        gridData.AddPlacement(gridKey, data.size, placed);
        gridVisual.MarkOccupied(cell, data.size);

        // Initialise soil in the pot if it has a PotContents component.
        PotContents contents = placed.GetComponent<PotContents>();
        if (contents != null)
            contents.Initialise(data.defaultSoil);
    }

    private void TryRemove(Vector2Int cell)
    {
        PlacementData data = gridData.GetPlacement(ToGridVec3(cell));
        if (data == null) return;

        Vector2Int origin = new Vector2Int(data.Origin.x, data.Origin.z);
        gridData.RemovePlacement(data.Origin);
        gridVisual.ClearFootprint(origin, data.Size);
        Destroy(data.PlacedObject);

        lastHoveredCell = new Vector2Int(-999, -999); // force hover refresh
    }

    private void TryPickupOrDrop(Vector2Int cell)
    {
        if (movingData == null)
        {
            // try to pick up
            PlacementData data = gridData.GetPlacement(ToGridVec3(cell));
            if (data == null) return;

            movingData = data;
            movingObject = data.PlacedObject;

            // remove from grid data but keep the GameObject
            // hide it and show preview instead
            Vector2Int origin = new Vector2Int(data.Origin.x, data.Origin.z);
            gridData.RemovePlacement(data.Origin);
            gridVisual.ClearFootprint(origin, data.Size);

            movingObject.SetActive(false);
            SpawnPreviewFromObject(movingObject, data.Size);
        }
        else
        {
            // try to drop
            if (!gridVisual.FootprintInBounds(cell, movingData.Size)) return;
            Vector3Int gridKey = ToGridVec3(cell);
            if (!gridData.CanPlace(gridKey, movingData.Size)) return;

            // move the real object to the new position
            movingObject.transform.position = CellToWorldCentre(cell, movingData.Size);
            movingObject.SetActive(true);

            gridData.AddPlacement(gridKey, movingData.Size, movingObject);
            gridVisual.MarkOccupied(cell, movingData.Size);

            movingData = null;
            movingObject = null;
            DestroyPreview();
            lastHoveredCell = new Vector2Int(-999, -999);
        }
    }

    private void PutMovingPotBack()
    {
        if (movingObject == null) return;
        movingObject.SetActive(true);
        gridData.AddPlacement(movingData.Origin, movingData.Size, movingObject);
        gridVisual.MarkOccupied(
            new Vector2Int(movingData.Origin.x, movingData.Origin.z), movingData.Size);
        DestroyPreview();
    }

    // * Preview object (semi invis)

    private void SpawnPreview(PotData data)
    {
        if (!showPreviewObject) return;
        GameObject prefab = data.previewPrefab != null ? data.previewPrefab : data.potPrefab;
        if (prefab == null) return;
        previewObject = Instantiate(prefab);
        previewObject.name = "[Preview]";
        foreach (var col in previewObject.GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    private void SpawnPreviewFromObject(GameObject source, Vector2Int size)
    {
        if (!showPreviewObject) return;
        previewObject = Instantiate(source);
        previewObject.name = "[Preview]";
        previewObject.SetActive(true);
        foreach (var col in previewObject.GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    private void DestroyPreview()
    {
        if (previewObject != null) { Destroy(previewObject); previewObject = null; }
    }

    private void SetPreviewVisible(bool v)
    {
        if (previewObject != null) previewObject.SetActive(v);
    }

    // * Helpers 

    private void CycleSelection(int dir)
    {
        int next = (selectedIndex + dir + availablePots.Count) % availablePots.Count;
        EnterPlaceMode(next);
    }

    private Vector3 CellToWorldCentre(Vector2Int cell, Vector2Int size)
    {
        Vector3 origin = surface.GridOriginWorld;
        float cs = surface.CellSize;
        return new Vector3(
            origin.x + (cell.x + size.x * 0.5f) * cs,
            origin.y,
            origin.z + (cell.y + size.y * 0.5f) * cs);
    }

    private static Vector3Int ToGridVec3(Vector2Int c) => new Vector3Int(c.x, 0, c.y);
}
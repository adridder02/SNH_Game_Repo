using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlacementSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputManager inputManager;
    [SerializeField] private GreenhouseSurface surface;

    [Header("Pot Types")]
    [SerializeField] private List<PotData> availablePots;

    [Header("Preview")]
    [SerializeField] private bool showPreviewObject = true;

    private enum Mode
    {
        None,
        Placing,
        Removing,
        Moving
    }

    private Mode mode = Mode.None;

    private GridData gridData = new GridData();
    private GridVisual gridVisual;

    private int selectedIndex = 0;
    private Vector2Int lastHoveredCell = new Vector2Int(-999, -999);

    private GameObject previewObject;

    private PlacementData movingData;
    private GameObject movingObject;

    public bool IsPlacementModeActive => mode != Mode.None;

    private void Start()
    {
        if (surface == null)
        {
            Debug.LogError("PlacementSystem: No GreenhouseSurface assigned.");
            return;
        }

        gridVisual = surface.GridVisual;

        if (availablePots == null || availablePots.Count == 0)
        {
            Debug.LogError("PlacementSystem: No pots assigned.");
            return;
        }

        GameInputModeManager.Instance.SetGameplayMode();
    }

    private void Update()
    {
        HandleModeToggleKeys();

        if (mode == Mode.None)
            return;

        if (mode == Mode.Placing)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;

            if (scroll > 0f)
                CycleSelection(1);
            else if (scroll < 0f)
                CycleSelection(-1);
        }

        Vector3 mouseWorld = inputManager.GetSelectedMapPosition();

        if (!gridVisual.WorldToCell(mouseWorld, out Vector2Int hoveredCell))
        {
            gridVisual.ClearHover();
            SetPreviewVisible(false);
            return;
        }

        if (hoveredCell != lastHoveredCell)
        {
            lastHoveredCell = hoveredCell;
            UpdateHoverVisual(hoveredCell);
        }

        if (previewObject != null)
        {
            Vector2Int size =
                mode == Mode.Moving && movingData != null
                ? movingData.Size
                : availablePots[selectedIndex].size;

            previewObject.transform.position =
                CellToWorldCentre(hoveredCell, size);
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (mode == Mode.Placing)
                TryPlace(hoveredCell);
            else if (mode == Mode.Removing)
                TryRemove(hoveredCell);
            else if (mode == Mode.Moving)
                TryPickupOrDrop(hoveredCell);
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelMode();
        }
    }

    private void HandleModeToggleKeys()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (mode == Mode.Placing)
                CancelMode();
            else
                EnterPlaceMode(selectedIndex);
        }

        if (Keyboard.current.xKey.wasPressedThisFrame)
        {
            if (mode == Mode.Removing)
                CancelMode();
            else
                EnterRemoveMode();
        }

        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            if (mode == Mode.Moving)
                CancelMode();
            else
                EnterMoveMode();
        }
    }

    public void EnterPlaceMode(int potIndex)
    {
        if (potIndex < 0 || potIndex >= availablePots.Count)
            return;

        CancelMode();

        selectedIndex = potIndex;
        mode = Mode.Placing;

        gridVisual.SetVisible(true);
        SpawnPreview(availablePots[selectedIndex]);

        GameInputModeManager.Instance.SetPlacementMode();
    }

    private void EnterRemoveMode()
    {
        CancelMode();

        mode = Mode.Removing;
        gridVisual.SetVisible(true);

        GameInputModeManager.Instance.SetPlacementMode();
    }

    private void EnterMoveMode()
    {
        CancelMode();

        mode = Mode.Moving;
        gridVisual.SetVisible(true);

        GameInputModeManager.Instance.SetPlacementMode();
    }

    private void CancelMode()
    {
        if (mode == Mode.Moving && movingData != null)
            PutMovingPotBack();

        mode = Mode.None;

        movingData = null;
        movingObject = null;

        gridVisual.ClearHover();
        gridVisual.SetVisible(false);

        DestroyPreview();

        GameInputModeManager.Instance.SetGameplayMode();
    }

    private void UpdateHoverVisual(Vector2Int cell)
    {
        gridVisual.ClearHover();

        switch (mode)
        {
            case Mode.Placing:
                {
                    PotData data = availablePots[selectedIndex];

                    bool canFit =
                        gridVisual.FootprintInBounds(cell, data.size);

                    bool canPlace =
                        canFit &&
                        gridData.CanPlace(ToGridVec3(cell), data.size);

                    gridVisual.SetFootprint(
                        cell,
                        data.size,
                        canPlace
                            ? GridVisual.CellState.Valid
                            : GridVisual.CellState.Invalid
                    );

                    SetPreviewVisible(true);
                    break;
                }

            case Mode.Removing:
                {
                    PlacementData data =
                        gridData.GetPlacement(ToGridVec3(cell));

                    if (data != null)
                    {
                        Vector2Int origin =
                            new Vector2Int(data.Origin.x, data.Origin.z);

                        gridVisual.SetFootprint(
                            origin,
                            data.Size,
                            GridVisual.CellState.Invalid
                        );
                    }

                    break;
                }

            case Mode.Moving:
                {
                    if (movingData == null)
                    {
                        PlacementData data =
                            gridData.GetPlacement(ToGridVec3(cell));

                        if (data != null)
                        {
                            Vector2Int origin =
                                new Vector2Int(data.Origin.x, data.Origin.z);

                            gridVisual.SetFootprint(
                                origin,
                                data.Size,
                                GridVisual.CellState.Valid
                            );
                        }
                    }
                    else
                    {
                        bool canFit =
                            gridVisual.FootprintInBounds(cell, movingData.Size);

                        bool canPlace =
                            canFit &&
                            gridData.CanPlace(
                                ToGridVec3(cell),
                                movingData.Size
                            );

                        gridVisual.SetFootprint(
                            cell,
                            movingData.Size,
                            canPlace
                                ? GridVisual.CellState.Valid
                                : GridVisual.CellState.Invalid
                        );

                        SetPreviewVisible(true);
                    }

                    break;
                }
        }
    }

    private void TryPlace(Vector2Int cell)
    {
        PotData data = availablePots[selectedIndex];

        if (!gridVisual.FootprintInBounds(cell, data.size))
            return;

        Vector3Int key = ToGridVec3(cell);

        if (!gridData.CanPlace(key, data.size))
            return;

        // ── FIX: Use the prefab's own rotation instead of Quaternion.identity
        // so that any axis-conversion baked in during Blender→Unity import is
        // respected, matching the preview which gets its rotation from the prefab.
        GameObject placed =
            Instantiate(
                data.potPrefab,
                CellToWorldCentre(cell, data.size),
                data.potPrefab.transform.rotation
            );

        // ── Do NOT initialize the pot with soil ──
        // The pot starts completely empty.
        // PotContents.Awake() already sets hasSoil = false, waterLevel = 0.
        // Player must add soil via PotInteraction menu.

        gridData.AddPlacement(key, data.size, placed);
        gridVisual.MarkOccupied(cell, data.size);
    }

    private void TryRemove(Vector2Int cell)
    {
        PlacementData data =
            gridData.GetPlacement(ToGridVec3(cell));

        if (data == null)
            return;

        Vector2Int origin =
            new Vector2Int(data.Origin.x, data.Origin.z);

        gridData.RemovePlacement(data.Origin);
        gridVisual.ClearFootprint(origin, data.Size);

        Destroy(data.PlacedObject);
    }

    private void TryPickupOrDrop(Vector2Int cell)
    {
        if (movingData == null)
        {
            PlacementData data =
                gridData.GetPlacement(ToGridVec3(cell));

            if (data == null)
                return;

            movingData = data;
            movingObject = data.PlacedObject;

            gridData.RemovePlacement(data.Origin);

            // ── FIX: Hide plant UI when picking up pot ──
            PotContents pot = movingObject.GetComponent<PotContents>();
            if (pot != null && pot.HasPlant && pot.Plant != null)
                pot.Plant.SetUIVisible(false);

            movingObject.SetActive(false);

            SpawnPreviewFromObject(movingObject);
        }
        else
        {
            Vector3Int key = ToGridVec3(cell);

            if (!gridData.CanPlace(key, movingData.Size))
                return;

            movingObject.transform.position =
                CellToWorldCentre(cell, movingData.Size);

            movingObject.SetActive(true);

            // ── FIX: Show plant UI when placing pot down ──
            PotContents pot = movingObject.GetComponent<PotContents>();
            if (pot != null && pot.HasPlant && pot.Plant != null)
                pot.Plant.SetUIVisible(true);

            gridData.AddPlacement(
                key,
                movingData.Size,
                movingObject
            );

            movingData = null;
            movingObject = null;

            DestroyPreview();
        }
    }

    private void PutMovingPotBack()
    {
        movingObject.SetActive(true);

        // ── FIX: Show plant UI when putting pot back ──
        PotContents pot = movingObject.GetComponent<PotContents>();
        if (pot != null && pot.HasPlant && pot.Plant != null)
            pot.Plant.SetUIVisible(true);

        gridData.AddPlacement(
            movingData.Origin,
            movingData.Size,
            movingObject
        );

        DestroyPreview();
    }

    private void SpawnPreview(PotData data)
    {
        if (!showPreviewObject)
            return;

        GameObject prefab =
            data.previewPrefab != null
            ? data.previewPrefab
            : data.potPrefab;

        previewObject = Instantiate(prefab);

        foreach (Collider c in previewObject.GetComponentsInChildren<Collider>())
            c.enabled = false;
    }

    private void SpawnPreviewFromObject(GameObject source)
    {
        if (!showPreviewObject)
            return;

        previewObject = Instantiate(source);

        foreach (Collider c in previewObject.GetComponentsInChildren<Collider>())
            c.enabled = false;

        previewObject.SetActive(true);
    }

    private void DestroyPreview()
    {
        if (previewObject != null)
            Destroy(previewObject);
    }

    private void SetPreviewVisible(bool visible)
    {
        if (previewObject != null)
            previewObject.SetActive(visible);
    }

    private void CycleSelection(int dir)
    {
        selectedIndex += dir;

        if (selectedIndex >= availablePots.Count)
            selectedIndex = 0;

        if (selectedIndex < 0)
            selectedIndex = availablePots.Count - 1;

        EnterPlaceMode(selectedIndex);
    }

    private Vector3 CellToWorldCentre(Vector2Int cell, Vector2Int size)
    {
        Vector3 origin = surface.GridOriginWorld;
        float cs = surface.CellSize;

        return new Vector3(
            origin.x + (cell.x + size.x * 0.5f) * cs,
            origin.y,
            origin.z + (cell.y + size.y * 0.5f) * cs
        );
    }

    private static Vector3Int ToGridVec3(Vector2Int c)
    {
        return new Vector3Int(c.x, 0, c.y);
    }
}
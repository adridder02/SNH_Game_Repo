using UnityEngine;
using UnityEngine.InputSystem;

// =============================================================
// AbilityPlacementSystem.cs
// -------------------------------------------------------------
// Attach next to PlacementSystem (same Player/Systems object).
// Handles hover/place/remove for AbilityKind.Placeable items
// (Sparkmint Leaf, Waterbell) — deliberately a companion system
// rather than jamming pot logic and ability logic into one class,
// but it reuses PlacementSystem's surfaces, InputManager, and —
// critically — its per-surface GridData, so a placeable can never
// overlap a pot or another placeable.
//
// FLOW:
//   AbilityInventoryUIController (your ability-item "Place" button)
//   calls BeginPlacing(itemData). From there this behaves exactly
//   like PlacementSystem's own Placing mode: hover highlights the
//   footprint green/red, left-click consumes one from the ability
//   inventory and spawns/registers it, right-click cancels.
//
//   Left in Placing mode after a successful placement (rather than
//   auto-exiting) so multi-count items like Sparkmint's 10 leaves
//   can be laid out one click at a time — exits automatically once
//   the stack hits zero, or on right-click / BeginRemoving / a new
//   BeginPlacing call.
// =============================================================
public class AbilityPlacementSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlacementSystem placementSystem;
    [SerializeField] private PlayerAbilityInventory abilityInventory;

    private enum Mode { None, Placing, Removing }
    private Mode mode = Mode.None;

    private AbilityItemData pendingItem;
    private GreenhouseSurface activeSurface;
    private Vector2Int lastHoveredCell = new Vector2Int(-999, -999);
    private GameObject previewObject;

    public bool IsActive => mode != Mode.None;

    private void Awake()
    {
        if (placementSystem == null)
            Debug.LogWarning("[AbilityPlacementSystem] placementSystem not assigned — ability placeables can't be placed.", this);
        if (abilityInventory == null)
            Debug.LogWarning("[AbilityPlacementSystem] abilityInventory not assigned.", this);
    }

    // ---------------------------------------------------------------
    // PUBLIC ENTRY POINTS — called by ability-inventory UI
    // ---------------------------------------------------------------
    public void BeginPlacing(AbilityItemData item)
    {
        if (item == null || item.kind != AbilityKind.Placeable) return;
        if (abilityInventory == null || abilityInventory.GetCount(item) <= 0) return;

        placementSystem?.CancelActiveMode();
        CancelSelf(suppressGridHide: true);

        pendingItem = item;
        mode = Mode.Placing;

        SetAllGridsVisible(true);
        SpawnPreview(item);
    }

    public void BeginRemoving()
    {
        placementSystem?.CancelActiveMode();
        CancelSelf(suppressGridHide: true);

        mode = Mode.Removing;
        SetAllGridsVisible(true);
    }

    public void Cancel() => CancelSelf(suppressGridHide: false);

    // ---------------------------------------------------------------
    private void Update()
    {
        if (mode == Mode.None) return;

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            Cancel();
            return;
        }

        if (placementSystem == null || placementSystem.InputManager == null) return;

        Vector3 mouseWorld = placementSystem.InputManager.GetSelectedMapPosition();
        GreenhouseSurface hovered = placementSystem.GetSurfaceAtWorldPosition(mouseWorld);

        if (hovered != activeSurface)
        {
            activeSurface?.GridVisual.ClearHover();
            activeSurface = hovered;
            lastHoveredCell = new Vector2Int(-999, -999);
        }

        if (activeSurface == null)
        {
            SetPreviewVisible(false);
            return;
        }

        GridVisual gridVisual = activeSurface.GridVisual;
        GridData gridData = placementSystem.GetGridData(activeSurface);

        if (!gridVisual.WorldToCell(mouseWorld, out Vector2Int cell))
        {
            gridVisual.ClearHover();
            SetPreviewVisible(false);
            return;
        }

        if (cell != lastHoveredCell)
        {
            lastHoveredCell = cell;
            gridVisual.ClearHover();

            if (mode == Mode.Placing && pendingItem != null)
            {
                bool canFit = gridVisual.FootprintInBounds(cell, pendingItem.footprint);
                bool canPlace = canFit && gridData != null && gridData.CanPlace(ToVec3(cell), pendingItem.footprint);
                gridVisual.SetFootprint(cell, pendingItem.footprint,
                    canPlace ? GridVisual.CellState.Valid : GridVisual.CellState.Invalid);
                SetPreviewVisible(true);

                if (previewObject != null)
                    previewObject.transform.position = gridVisual.GetCellCenter(cell) +
                        new Vector3((pendingItem.footprint.x - 1) * activeSurface.CellSize * 0.5f, 0f,
                                    (pendingItem.footprint.y - 1) * activeSurface.CellSize * 0.5f);
            }
            else if (mode == Mode.Removing && gridData != null)
            {
                PlacementData data = gridData.GetPlacement(ToVec3(cell));
                if (data != null)
                {
                    Vector2Int origin = new Vector2Int(data.Origin.x, data.Origin.z);
                    gridVisual.SetFootprint(origin, data.Size, GridVisual.CellState.Invalid);
                }
            }
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (mode == Mode.Placing) TryPlace(cell, gridData, gridVisual);
            else if (mode == Mode.Removing) TryRemove(cell, gridData, gridVisual);
        }
    }

    // ---------------------------------------------------------------
    private void TryPlace(Vector2Int cell, GridData gridData, GridVisual gridVisual)
    {
        if (pendingItem == null || gridData == null) return;
        if (!gridVisual.FootprintInBounds(cell, pendingItem.footprint)) return;

        Vector3Int origin = ToVec3(cell);
        if (!gridData.CanPlace(origin, pendingItem.footprint)) return;

        if (abilityInventory == null || !abilityInventory.TryConsume(pendingItem, 1)) return;

        GameObject prefab = pendingItem.placedPrefab;
        GameObject go = prefab != null
            ? Instantiate(prefab)
            : new GameObject(pendingItem.displayName);

        go.transform.position = gridVisual.GetCellCenter(cell) +
            new Vector3((pendingItem.footprint.x - 1) * activeSurface.CellSize * 0.5f, 0f,
                        (pendingItem.footprint.y - 1) * activeSurface.CellSize * 0.5f);

        gridData.AddPlacement(origin, pendingItem.footprint, go);
        gridVisual.MarkOccupied(cell, pendingItem.footprint);

        AbilityPlaceable placeable = go.GetComponent<AbilityPlaceable>();
        if (placeable != null)
            placeable.Initialise(pendingItem, origin, pendingItem.footprint, gridData, activeSurface);
        else
            Debug.LogWarning($"[AbilityPlacementSystem] '{pendingItem.displayName}'s placedPrefab has no " +
                              "AbilityPlaceable-derived component — it was placed but does nothing.");

        // Keep placing if there's more of this item; otherwise stop automatically.
        if (abilityInventory.GetCount(pendingItem) <= 0)
            Cancel();
    }

    private void TryRemove(Vector2Int cell, GridData gridData, GridVisual gridVisual)
    {
        if (gridData == null) return;

        PlacementData data = gridData.GetPlacement(ToVec3(cell));
        if (data == null) return;

        AbilityPlaceable placeable = data.PlacedObject != null ? data.PlacedObject.GetComponent<AbilityPlaceable>() : null;
        placeable?.NotifyRemoved();

        Vector2Int origin = new Vector2Int(data.Origin.x, data.Origin.z);
        gridData.RemovePlacement(data.Origin);
        gridVisual.ClearFootprint(origin, data.Size);

        if (data.PlacedObject != null) Destroy(data.PlacedObject);
    }

    // ---------------------------------------------------------------
    private void CancelSelf(bool suppressGridHide)
    {
        mode = Mode.None;
        pendingItem = null;
        DestroyPreview();

        activeSurface?.GridVisual.ClearHover();
        activeSurface = null;
        lastHoveredCell = new Vector2Int(-999, -999);

        if (!suppressGridHide) SetAllGridsVisible(false);
    }

    private void SetAllGridsVisible(bool visible)
    {
        if (placementSystem == null) return;
        foreach (GreenhouseSurface s in placementSystem.Surfaces)
            s?.GridVisual?.SetVisible(visible);
    }

    private void SpawnPreview(AbilityItemData item)
    {
        DestroyPreview();
        if (item.placedPrefab == null) return;

        previewObject = Instantiate(item.placedPrefab);
        foreach (Collider c in previewObject.GetComponentsInChildren<Collider>())
            c.enabled = false;
        previewObject.SetActive(false);
    }

    private void SetPreviewVisible(bool visible)
    {
        if (previewObject != null) previewObject.SetActive(visible);
    }

    private void DestroyPreview()
    {
        if (previewObject != null) Destroy(previewObject);
        previewObject = null;
    }

    private static Vector3Int ToVec3(Vector2Int c) => new Vector3Int(c.x, 0, c.y);
}

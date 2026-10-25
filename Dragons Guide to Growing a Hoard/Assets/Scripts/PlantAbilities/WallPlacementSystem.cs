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
    [Tooltip("Optional. If assigned, entering wall placement/remove mode here cancels any active pot " +
             "tool (Place/Remove/Move) first, so the wall grid and the pot grid can never both be " +
             "'hot' at the same time.")]
    [SerializeField] private PlacementSystem placementSystem;

    [Header("Mushroom Types")]
    [SerializeField] private List<WallMushroomData> availableMushrooms;

    [Tooltip("Where the Clovenwick 'Shelf' items live. Auto-found on the player if left empty.")]
    [SerializeField] private PlayerAbilityInventory abilityInventory;

    [Header("Preview")]
    [SerializeField] private bool showPreviewObject = true;

    public enum Mode { None, Placing, Removing }
    private Mode mode = Mode.None;
    public Mode CurrentMode => mode;

    /// <summary>Fires whenever mode changes, for whatever reason (button, keybind, cancel, the
    /// pot<->wall Tab toggle in PlacementSystem, ...). Added for MainUIController's Floor/Wall
    /// Placement Mode banner — nothing here previously needed to observe this from outside.</summary>
    public event System.Action OnModeChanged;

    /// <summary>Fires when the number of placeable items changes (picked up, placed, taken back), so the
    /// HUD can redraw the count next to the wall-mushroom icon.</summary>
    public event System.Action OnCountChanged;

    private readonly Dictionary<WallSurface, GridData> surfaceGridData = new Dictionary<WallSurface, GridData>();
    private WallSurface activeSurface;
    private int selectedIndex = 0;
    private Vector2Int lastHoveredCell = new Vector2Int(-999, -999);
    private GameObject previewObject;
    private bool previewUsesMountRotation; // only when the preview IS the real prefab (a separate preview prefab keeps its own authored rotation)

    public bool IsActive => mode != Mode.None;

    // Save/load: what's been mounted where (the scene itself doesn't remember surface/cell).
    public struct WallRecord { public GameObject obj; public WallMushroomData data; public int surfaceIndex; public Vector3Int origin; }
    private readonly List<WallRecord> placedRecords = new List<WallRecord>();

    public IReadOnlyList<WallRecord> PlacedRecords
    {
        get { placedRecords.RemoveAll(r => r.obj == null); return placedRecords; }
    }

    /// <summary>Which wall-mushroom index Placing mode would use right now (last selected). Read
    /// by PlacementSystem's Tab-toggle so switching TO wall placement preserves whatever type was
    /// last selected instead of always resetting to index 0.</summary>
    public int SelectedIndex => selectedIndex;

    /// <summary>Read-only view of the wall-mushroom types available to place — used by
    /// MainUIController's wall-mushroom selector HUD to build its icon list in the same order.</summary>
    public IReadOnlyList<WallMushroomData> AvailableMushrooms => availableMushrooms;

    private void Start()
    {
        foreach (WallSurface surface in wallSurfaces)
            if (surface != null) surfaceGridData[surface] = new GridData();

        if (abilityInventory == null) abilityInventory = FindAnyObjectByType<PlayerAbilityInventory>();
        if (abilityInventory != null) abilityInventory.OnChanged += HandleInventoryChanged;
    }

    private void OnDestroy()
    {
        if (abilityInventory != null) abilityInventory.OnChanged -= HandleInventoryChanged;
    }

    private void HandleInventoryChanged()
    {
        lastHoveredCell = new Vector2Int(-999, -999); // re-evaluate valid/invalid colour on the next frame
        OnCountChanged?.Invoke();
    }

    /// <summary>How many of this type the player can place right now. 0 if the type has no required
    /// item at all (those can't be wall-placed).</summary>
    public int GetAvailableCount(int index)
    {
        if (availableMushrooms == null || index < 0 || index >= availableMushrooms.Count) return 0;
        return GetAvailableCount(availableMushrooms[index]);
    }

    public int GetAvailableCount(WallMushroomData data)
    {
        if (data == null || data.requiredItem == null || abilityInventory == null) return 0;
        return abilityInventory.GetCount(data.requiredItem);
    }

    /// <summary>How many of the currently selected type the player has.</summary>
    public int SelectedCount => GetAvailableCount(selectedIndex);

    private bool HasItemFor(WallMushroomData data) => GetAvailableCount(data) > 0;

    // Tutorial locking: wall placement is only usable while a step is teaching it (or ticked Wall Placement).
    private static readonly string[] WallIds = { "entered_wall_placement_mode", "placed_wall_mushroom" };
    private static bool TutorialAllows()
    {
        TutorialSequenceController t = TutorialSequenceController.Instance;
        return t == null || t.IsWorldActionAllowed(WorldAction.WallPlacement, WallIds);
    }

    public void ToggleMushroomPlaceMode(int index)
    {
        if (mode == Mode.Placing) { CancelMode(); return; }
        if (!TutorialAllows()) return;
        if (availableMushrooms == null || index < 0 || index >= availableMushrooms.Count) return;

        placementSystem?.CancelActiveMode(); // wall grid and pot grid can never both be active at once
        CancelMode();
        selectedIndex = index;
        mode = Mode.Placing;

        foreach (WallSurface s in wallSurfaces) s?.GridVisual?.SetVisible(true);
        SpawnPreview(availableMushrooms[selectedIndex]);
        if (GameInputModeManager.Instance != null) GameInputModeManager.Instance.SetPlacementMode();

        // Tutorial hook — matches a step reading something like "press Tab to enter wall
        // placement". This is the only way into wall-Placing mode right now (PlacementSystem's
        // Tab toggle is the sole caller), so firing here covers it regardless of entry point.
        TutorialSequenceController.Instance?.NotifyExternalTrigger("entered_wall_placement_mode");

        OnModeChanged?.Invoke();
    }

    public void ToggleRemoveMode()
    {
        if (mode == Mode.Removing) { CancelMode(); return; }
        if (!TutorialAllows()) return;

        placementSystem?.CancelActiveMode();
        CancelMode();
        mode = Mode.Removing;
        foreach (WallSurface s in wallSurfaces) s?.GridVisual?.SetVisible(true);
        if (GameInputModeManager.Instance != null) GameInputModeManager.Instance.SetPlacementMode();
        OnModeChanged?.Invoke();
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
        if (GameInputModeManager.Instance != null) GameInputModeManager.Instance.SetGameplayMode();
        OnModeChanged?.Invoke();
    }

    private void Update()
    {
        HandleModeToggleKeys();

        if (mode == Mode.None || inputManager == null) return;

        // Escape is now handled centrally by ExitMenuController, which calls CancelMode() directly
        // rather than this polling for it independently — see PlacementSystem's matching comment
        // for why (a script-execution-order race between multiple independent pollers of the same
        // keypress within the same frame).

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
        {
            WallMushroomData pd = availableMushrooms[selectedIndex];
            previewObject.transform.position = activeSurface.MountPosition(cell, pd.size, pd.wallOffset, pd.planeNudge);
            if (previewUsesMountRotation) previewObject.transform.rotation = activeSurface.MountRotation;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame && TutorialAllows())
        {
            if (mode == Mode.Placing) TryPlace(cell, gridData, gridVisual);
            else if (mode == Mode.Removing) TryRemove(cell, gridData, gridVisual);
        }

        // Right-click no longer cancels the mode — it now holds to rotate the camera instead (see
        // ThirdPersonCameraController.AllowRotationWhileLockedIfRightClickHeld, set by
        // GameInputModeManager.SetPlacementMode()).
    }

    private void HandleModeToggleKeys()
    {
        // R and the pot-place<->wall-place Tab toggle now both live entirely in PlacementSystem —
        // see its HandleModeToggleKeys(). Having each script independently poll the SAME key for a
        // toggle that affects BOTH of them is exactly the F-vs-R race the comment used to warn
        // about here: whichever script's Update() runs first would act on its own (just-changed)
        // mode state before the other script even gets a chance to see the original press, and the
        // two could flip back and forth within the same frame. One script owning the whole toggle
        // avoids that. Nothing to poll here anymore.
    }

    private void UpdateHoverVisual(Vector2Int cell, WallGridVisual gridVisual, GridData gridData)
    {
        gridVisual.ClearHover();

        if (mode == Mode.Placing)
        {
            WallMushroomData data = availableMushrooms[selectedIndex];
            bool canFit = gridVisual.FootprintInBounds(cell, data.size);
            bool canPlace = canFit && HasItemFor(data) && gridData.CanPlace(ToVec3(cell), data.size);
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

        // Only mature Clovenwicks (the Shelf item) can go on a wall. No item = no placing.
        if (data.requiredItem == null)
        {
            Debug.LogWarning($"[WallPlacementSystem] '{data.displayName}' has no Required Item assigned, so it can't be " +
                             "placed on a wall. Assign the Shelf ability item on its WallMushroomData.");
            return;
        }
        if (abilityInventory == null || !abilityInventory.TryConsume(data.requiredItem, 1)) return;

        Vector3 worldPos = activeSurface.MountPosition(cell, data.size, data.wallOffset, data.planeNudge);

        GameObject prefab = data.prefab != null ? data.prefab : null;
        GameObject placed = prefab != null
            ? Instantiate(prefab, worldPos, activeSurface.MountRotation)
            : new GameObject(data.displayName);

        placed.transform.position = worldPos;
        placed.transform.rotation = activeSurface.MountRotation;

        ClovenwickWallMount mount = placed.GetComponent<ClovenwickWallMount>();
        if (mount == null) mount = placed.AddComponent<ClovenwickWallMount>();
        mount.maxWeight = data.maxSupportedWeightKg > 12f ? ClovenwickWallMount.WeightClass.Medium : ClovenwickWallMount.WeightClass.Small;
        mount.fullWeightKg = data.maxSupportedWeightKg;
        mount.sourceData = data;

        gridData.AddPlacement(key, data.size, placed);
        gridVisual.MarkOccupied(cell, data.size);
        placedRecords.Add(new WallRecord { obj = placed, data = data, surfaceIndex = wallSurfaces.IndexOf(activeSurface), origin = key });

        // Tutorial hook — matches a step reading something like "left-click to place it",
        // the wall-placement equivalent of PlacementSystem.TryPlace's own trigger calls.
        TutorialSequenceController.Instance?.NotifyExternalTrigger("placed_wall_mushroom");
    }

    /// <summary>Save/load: re-mounts a mushroom shelf at its saved wall cell without consuming an item.</summary>
    public bool RestoreMushroom(WallMushroomData data, int surfaceIndex, Vector3Int origin)
    {
        if (data == null || surfaceIndex < 0 || surfaceIndex >= wallSurfaces.Count) return false;
        WallSurface surface = wallSurfaces[surfaceIndex];
        if (surface == null || surface.GridVisual == null || !surfaceGridData.TryGetValue(surface, out GridData gridData)) return false;
        if (!gridData.CanPlace(origin, data.size)) return false;

        Vector2Int cell = new Vector2Int(origin.x, origin.z);
        Vector3 worldPos = surface.MountPosition(cell, data.size, data.wallOffset, data.planeNudge);

        GameObject placed = data.prefab != null
            ? Instantiate(data.prefab, worldPos, surface.MountRotation)
            : new GameObject(data.displayName);
        placed.transform.position = worldPos;
        placed.transform.rotation = surface.MountRotation;

        ClovenwickWallMount mount = placed.GetComponent<ClovenwickWallMount>();
        if (mount == null) mount = placed.AddComponent<ClovenwickWallMount>();
        mount.maxWeight = data.maxSupportedWeightKg > 12f ? ClovenwickWallMount.WeightClass.Medium : ClovenwickWallMount.WeightClass.Small;
        mount.fullWeightKg = data.maxSupportedWeightKg;
        mount.sourceData = data;

        gridData.AddPlacement(origin, data.size, placed);
        surface.GridVisual.MarkOccupied(cell, data.size);
        placedRecords.Add(new WallRecord { obj = placed, data = data, surfaceIndex = surfaceIndex, origin = origin });
        return true;
    }

    private void TryRemove(Vector2Int cell, GridData gridData, WallGridVisual gridVisual)
    {
        PlacementData data = gridData.GetPlacement(ToVec3(cell));
        if (data == null) return;

        Vector2Int origin = new Vector2Int(data.Origin.x, data.Origin.z);
        gridData.RemovePlacement(data.Origin);
        placedRecords.RemoveAll(r => r.obj == data.PlacedObject);
        gridVisual.ClearFootprint(origin, data.Size);

        if (data.PlacedObject != null)
        {
            ClovenwickWallMount mount = data.PlacedObject.GetComponent<ClovenwickWallMount>();
            WallMushroomData source = mount != null ? mount.sourceData : null;
            if (source != null && source.refundOnRemove && source.requiredItem != null && abilityInventory != null)
                abilityInventory.Add(source.requiredItem, 1);

            Destroy(data.PlacedObject);
        }
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

            if (surface.RunsAlongX)
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

        previewUsesMountRotation = data.previewPrefab == null;
        previewObject = Instantiate(prefab);
        foreach (Collider c in previewObject.GetComponentsInChildren<Collider>()) c.enabled = false;
        previewObject.SetActive(true);
    }

    private void SetPreviewVisible(bool visible) { if (previewObject != null) previewObject.SetActive(visible); }
    private void DestroyPreview() { if (previewObject != null) Destroy(previewObject); previewObject = null; }

    private static Vector3Int ToVec3(Vector2Int c) => new Vector3Int(c.x, 0, c.y);
}
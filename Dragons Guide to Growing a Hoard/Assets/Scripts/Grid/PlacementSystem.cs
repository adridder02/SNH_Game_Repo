using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlacementSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputManager inputManager;
    [Tooltip("All greenhouse surfaces in the scene. The system will auto-detect which one the mouse is over.")]
    [SerializeField] private List<GreenhouseSurface> surfaces = new List<GreenhouseSurface>();

    [Header("Pot Types")]
    [SerializeField] private List<PotData> availablePots;

    [Header("Preview")]
    [SerializeField] private bool showPreviewObject = true;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    [Header("Audio - Placement SFX")]
    [SerializeField] private AudioClip placeSoundClip;
    [SerializeField] private AudioClip pickupSoundClip;
    [SerializeField] private AudioClip dropSoundClip;
    [SerializeField] private AudioClip removeSoundClip;
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [Header("Audio - Ambient Music")]
    [SerializeField] private AudioClip ambientMusicClip;
    [Range(0f, 1f)]
    [SerializeField] private float ambientVolume = 0.4f;
    [SerializeField] private bool loopAmbientMusic = true;

    private AudioSource sfxSource;
    private AudioSource ambientSource;

    [Header("Missions")]
    [Tooltip("The five-task tutorial mission (RemovedPlant/MovePot/AddedSoil/PlantedSeed/AddedWater). " +
             "Only index 1 (MovePot) is reported from this script.")]
    [SerializeField] private MissionData tutorialMission;
    [Tooltip("Task 0 (OpenedInventory) is completed from InventoryUIController — assign the SAME asset " +
             "there. Tasks 1/2/3 (Small/Medium/Large pot planted) are completed from here, offset by +1 " +
             "from availablePots' index since task 0 is taken by OpenedInventory.")]
    [SerializeField] private MissionData collectionMission;
    public enum Mode
    {
        None,
        Placing,
        Removing,
        Moving
    }

    private Mode mode = Mode.None;

    /// <summary>
    /// Fired whenever the active tool actually changes (Place/Remove/Move/None), regardless of
    /// whether the change came from a keybind or from UI. Anything driving tool-button highlight
    /// state should subscribe to this instead of polling, so button and key stay in sync.
    /// </summary>
    public event System.Action<Mode> OnModeChanged;

    /// <summary>The currently active tool, or Mode.None if no tool is in use.</summary>
    public Mode CurrentMode => mode;

    /// <summary>Which pot index Placing mode would use right now (last cycled-to / selected pot).</summary>
    public int SelectedPotIndex => selectedIndex;

    // One GridData per surface
    private Dictionary<GreenhouseSurface, GridData> surfaceGridData = new Dictionary<GreenhouseSurface, GridData>();

    // Currently active surface (the one mouse is hovering over)
    private GreenhouseSurface activeSurface;

    private int selectedIndex = 0;
    private Vector2Int lastHoveredCell = new Vector2Int(-999, -999);

    private GameObject previewObject;

    private PlacementData movingData;
    private GameObject movingObject;
    private GreenhouseSurface movingSourceSurface; // track which surface the object was picked up from

    public bool IsPlacementModeActive => mode != Mode.None;

    private void Awake()
    {
        // SFX source — short one-shot sounds
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.volume = sfxVolume;

        // Ambient source — looping background music
        ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.playOnAwake = false;
        ambientSource.loop = loopAmbientMusic;
        ambientSource.volume = ambientVolume;

        if (ambientMusicClip != null)
        {
            ambientSource.clip = ambientMusicClip;
            ambientSource.Play();
        }
    }

    private void Start()
    {
        if (surfaces == null || surfaces.Count == 0)
        {
            Debug.LogError("PlacementSystem: No GreenhouseSurfaces assigned.");
            return;
        }

        // Initialize GridData for each surface
        foreach (var surface in surfaces)
        {
            if (surface != null)
            {
                surfaceGridData[surface] = new GridData();
                Debug.Log($"PlacementSystem: Initialized surface '{surface.name}' with dimensions {surface.GridDimensions} at origin {surface.GridOriginWorld}");
            }
            else
            {
                Debug.LogWarning("PlacementSystem: Null surface found in surfaces list!");
            }
        }

        if (availablePots == null || availablePots.Count == 0)
        {
            Debug.LogError("PlacementSystem: No pots assigned.");
            return;
        }

        Debug.Log($"PlacementSystem: Initialized with {surfaceGridData.Count} surfaces and {availablePots.Count} pot types.");

        GameInputModeManager.Instance.SetGameplayMode();
    }

    // Keep inspector-adjusted volumes live during Play Mode
    private void OnValidate()
    {
        if (sfxSource != null)
            sfxSource.volume = sfxVolume;

        if (ambientSource != null)
        {
            ambientSource.volume = ambientVolume;
            ambientSource.loop = loopAmbientMusic;
        }
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

        // Determine which surface the mouse is over
        GreenhouseSurface hoveredSurface = GetSurfaceAtPosition(mouseWorld);

        if (debugMode && Time.frameCount % 30 == 0) // Log every 30 frames to avoid spam
        {
            Debug.Log($"Mode: {mode}, Mouse world pos: {mouseWorld}, Hovered surface: {(hoveredSurface != null ? hoveredSurface.name : "NONE")}, Active surface: {(activeSurface != null ? activeSurface.name : "NONE")}");
        }

        // If we moved to a different surface or off all surfaces
        if (hoveredSurface != activeSurface)
        {
            // Clear hover on old surface
            if (activeSurface != null)
            {
                activeSurface.GridVisual.ClearHover();
            }

            activeSurface = hoveredSurface;
            // Force hover update to fire immediately on the new surface
            lastHoveredCell = new Vector2Int(-999, -999);

            if (debugMode && activeSurface != null)
            {
                Debug.Log($"Switched to surface: {activeSurface.name}");
            }
        }

        // No valid surface under mouse
        if (activeSurface == null)
        {
            SetPreviewVisible(false);
            if (debugMode && Time.frameCount % 60 == 0)
                Debug.Log("No active surface - preview hidden");
            return;
        }

        GridVisual gridVisual = activeSurface.GridVisual;

        if (!gridVisual.WorldToCell(mouseWorld, out Vector2Int hoveredCell))
        {
            gridVisual.ClearHover();
            SetPreviewVisible(false);
            if (debugMode && Time.frameCount % 60 == 0)
                Debug.Log($"WorldToCell failed for position {mouseWorld}");
            return;
        }

        if (hoveredCell != lastHoveredCell)
        {
            lastHoveredCell = hoveredCell;
            UpdateHoverVisual(hoveredCell);
            if (debugMode)
                Debug.Log($"Hovering cell: {hoveredCell} on surface '{activeSurface.name}'");
        }

        if (previewObject != null)
        {
            Vector2Int size =
                mode == Mode.Moving && movingData != null
                ? movingData.Size
                : availablePots[selectedIndex].size;

            previewObject.transform.position =
                CellToWorldCentre(hoveredCell, size, activeSurface);
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (debugMode)
                Debug.Log($"Left click at cell {hoveredCell}, mode: {mode}");

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
            TogglePlaceMode();

        if (Keyboard.current.xKey.wasPressedThisFrame)
            ToggleRemoveMode();

        if (Keyboard.current.gKey.wasPressedThisFrame)
            ToggleMoveMode();
    }

    // ---------------------------------------------------------------
    // Public toggle API — the single entry point for turning a tool on/off.
    // Both keybinds (above) and UI tool buttons call these, so "is the tool
    // currently in use" can never disagree between the two: whichever one
    // fires second just flips the same underlying `mode` back off again.
    // ---------------------------------------------------------------

    /// <summary>Toggles Placing mode using whatever pot is currently selected.</summary>
    public void TogglePlaceMode() => TogglePlaceMode(selectedIndex);

    /// <summary>Toggles Placing mode for a specific pot index (e.g. from a pot-specific button).</summary>
    public void TogglePlaceMode(int potIndex)
    {
        if (mode == Mode.Placing)
            CancelMode();
        else
            EnterPlaceMode(potIndex);
    }

    public void ToggleRemoveMode()
    {
        if (mode == Mode.Removing)
            CancelMode();
        else
            EnterRemoveMode();
    }

    public void ToggleMoveMode()
    {
        if (mode == Mode.Moving)
            CancelMode();
        else
            EnterMoveMode();
    }

    public void EnterPlaceMode(int potIndex)
    { // lock camera here....
        if (potIndex < 0 || potIndex >= availablePots.Count)
            return;

        CancelMode(suppressEvent: true);

        selectedIndex = potIndex;
        mode = Mode.Placing;

        // Show all grids
        foreach (var surface in surfaces)
        {
            if (surface != null && surface.GridVisual != null)
            {
                surface.GridVisual.SetVisible(true);
                if (debugMode)
                    Debug.Log($"EnterPlaceMode: Showing grid for surface '{surface.name}'");
            }
            else if (debugMode)
            {
                Debug.LogWarning($"EnterPlaceMode: Surface or GridVisual is null!");
            }
        }

        SpawnPreview(availablePots[selectedIndex]);

        if (debugMode)
            Debug.Log($"EnterPlaceMode: Entered placing mode with pot index {potIndex}");

        GameInputModeManager.Instance.SetPlacementMode();

        OnModeChanged?.Invoke(mode);
    }

    private void EnterRemoveMode()
    {
        CancelMode(suppressEvent: true);

        mode = Mode.Removing;

        // Show all grids
        foreach (var surface in surfaces)
        {
            if (surface != null)
                surface.GridVisual.SetVisible(true);
        }

        GameInputModeManager.Instance.SetPlacementMode();

        OnModeChanged?.Invoke(mode);
    }

    private void EnterMoveMode()
    {
        CancelMode(suppressEvent: true);

        mode = Mode.Moving;

        // Show all grids
        foreach (var surface in surfaces)
        {
            if (surface != null)
                surface.GridVisual.SetVisible(true);
        }

        GameInputModeManager.Instance.SetPlacementMode();

        OnModeChanged?.Invoke(mode);
    }

    private void CancelMode(bool suppressEvent = false)
    {// unlock camera here...
        if (mode == Mode.Moving && movingData != null)
            PutMovingPotBack();

        mode = Mode.None;

        movingData = null;
        movingObject = null;
        movingSourceSurface = null;

        // Hide and clear all grids
        foreach (var surface in surfaces)
        {
            if (surface != null)
            {
                surface.GridVisual.ClearHover();
                surface.GridVisual.SetVisible(false);
            }
        }

        DestroyPreview();

        GameInputModeManager.Instance.SetGameplayMode();

        if (!suppressEvent)
            OnModeChanged?.Invoke(mode);
    }

    private void UpdateHoverVisual(Vector2Int cell)
    {
        if (activeSurface == null)
            return;

        GridVisual gridVisual = activeSurface.GridVisual;
        GridData gridData = surfaceGridData[activeSurface];

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
        if (activeSurface == null)
        {
            if (debugMode)
                Debug.LogWarning("TryPlace: No active surface!");
            return;
        }

        PotData data = availablePots[selectedIndex];
        GridVisual gridVisual = activeSurface.GridVisual;
        GridData gridData = surfaceGridData[activeSurface];
        
        // Safety clamp - ensure cell is within bounds
        Vector2Int gridDims = activeSurface.GridDimensions;
        cell.x = Mathf.Clamp(cell.x, 0, gridDims.x - 1);
        cell.y = Mathf.Clamp(cell.y, 0, gridDims.y - 1);

        if (debugMode)
            Debug.Log($"TryPlace: Attempting to place pot at cell {cell} on surface '{activeSurface.name}'");

        if (!gridVisual.FootprintInBounds(cell, data.size))
        {
            if (debugMode)
                Debug.LogWarning($"TryPlace: Footprint not in bounds! Cell: {cell}, Size: {data.size}, Grid dimensions: {activeSurface.GridDimensions}");
            return;
        }

        Vector3Int key = ToGridVec3(cell);

        if (!gridData.CanPlace(key, data.size))
        {
            if (debugMode)
                Debug.LogWarning($"TryPlace: CanPlace returned false for cell {cell}");
            return;
        }

        Vector3 worldPos = CellToWorldCentre(cell, data.size, activeSurface);

        if (debugMode)
            Debug.Log($"TryPlace: Placing pot at world position {worldPos}");

        GameObject placed =
            Instantiate(
                data.potPrefab,
                worldPos,
                data.potPrefab.transform.rotation
            );

        //! --- Insertation
        PotContents pc = placed.GetComponent<PotContents>();
        if (pc != null)
        {
            pc.GridOrigin = key;        // key is the Vector3Int placement origin
            pc.GridData = gridData;     // This surface's gridData instance
            pc.CachePlantReference();
            if (pc.Plant != null)
                pc.Plant.SetPotContents(pc);
        }
        //!

        gridData.AddPlacement(key, data.size, placed);
        gridVisual.MarkOccupied(cell, data.size);

        if (debugMode)
            Debug.Log($"TryPlace: Successfully placed pot '{placed.name}' at {worldPos}");

        PlaySFX(placeSoundClip);

        if (tutorialMission != null && tutorialMission.tasks.Count > 1)
            MissionProgressManager.Instance?.CompleteTask(tutorialMission, tutorialMission.tasks[1]); // MovePot

        int potSizeTaskIndex = selectedIndex + 1; // index 0 is OpenedInventory (completed elsewhere)
        if (collectionMission != null && potSizeTaskIndex >= 1 && potSizeTaskIndex < collectionMission.tasks.Count)
            MissionProgressManager.Instance?.CompleteTask(collectionMission, collectionMission.tasks[potSizeTaskIndex]);

        // The old Tutorial_1.Instance.SetGridOnTable() call here only advanced the on-screen
        // instruction text (not a checklist task) — nothing to repoint it to yet since that
        // on-screen system hasn't been rebuilt.
    }

    private void TryRemove(Vector2Int cell)
    {
        if (activeSurface == null)
            return;

        GridData gridData = surfaceGridData[activeSurface];
        GridVisual gridVisual = activeSurface.GridVisual;

        PlacementData data =
            gridData.GetPlacement(ToGridVec3(cell));

        if (data == null)
            return;

        PotContents pc = data.PlacedObject.GetComponent<PotContents>();

        if (pc != null)
            pc.ClearGridInfo();

        Vector2Int origin =
            new Vector2Int(data.Origin.x, data.Origin.z);

        gridData.RemovePlacement(data.Origin);
        gridVisual.ClearFootprint(origin, data.Size);

        Destroy(data.PlacedObject);

        PlaySFX(removeSoundClip);
    }

    private void TryPickupOrDrop(Vector2Int cell)
    {
        if (activeSurface == null)
            return;

        GridData gridData = surfaceGridData[activeSurface];
        GridVisual gridVisual = activeSurface.GridVisual;

        if (movingData == null)
        {
            // Picking up
            PlacementData data =
                gridData.GetPlacement(ToGridVec3(cell));

            if (data == null)
                return;

            movingData = data;
            movingObject = data.PlacedObject;
            movingSourceSurface = activeSurface; // remember where we picked it up from

            gridData.RemovePlacement(data.Origin);

            PotContents pot = movingObject.GetComponent<PotContents>();
            if (pot != null && pot.HasPlant && pot.Plant != null)
                pot.Plant.SetUIVisible(false);

            movingObject.SetActive(false);

            SpawnPreviewFromObject(movingObject);

            PlaySFX(pickupSoundClip);
        }
        else
        {
            // Dropping
            Vector3Int key = ToGridVec3(cell);

            if (!gridData.CanPlace(key, movingData.Size))
                return;

            movingObject.transform.position =
                CellToWorldCentre(cell, movingData.Size, activeSurface);

            movingObject.SetActive(true);

            //! --- Insertation
            PotContents pc = movingObject.GetComponent<PotContents>();
            if (pc != null)
            {
                pc.GridOrigin = key;
                pc.GridData = gridData; // Update to the new surface's grid data
                pc.CachePlantReference();
                if (pc.Plant != null)
                {
                    pc.Plant.SetPotContents(pc);
                    pc.Plant.SetUIVisible(true);
                }
            }
            //!

            gridData.AddPlacement(
                key,
                movingData.Size,
                movingObject
            );

            movingData = null;
            movingObject = null;
            movingSourceSurface = null;

            DestroyPreview();

            PlaySFX(dropSoundClip);
        }
    }

    private void PutMovingPotBack()
    {
        if (movingSourceSurface == null)
            return;

        GridData gridData = surfaceGridData[movingSourceSurface];

        movingObject.SetActive(true);

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
        previewObject.SetActive(true); // ensure visible immediately; SetPreviewVisible(false) hides it when off-surface

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

    private void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null)
            return;

        sfxSource.volume = sfxVolume;
        sfxSource.PlayOneShot(clip);
    }

    /// <summary>
/// Determines which GreenhouseSurface the given world position is over.
/// Returns null if not over any surface.
/// </summary>
private GreenhouseSurface GetSurfaceAtPosition(Vector3 worldPos)
{
    // Find the closest surface to the mouse position (based on XZ plane)
    GreenhouseSurface closestSurface = null;
    float closestYDist = float.MaxValue;
    float epsilon = 0.001f; // Small tolerance for edge detection

    foreach (var surface in surfaces)
    {
        if (surface == null || surface.GridVisual == null)
            continue;

        Vector3 origin = surface.GridOriginWorld;
        Vector2Int dims = surface.GridDimensions;
        float cellSize = surface.CellSize;

        // Calculate the bounds of this surface on the XZ plane WITH TOLERANCE
        float minX = origin.x - epsilon;
        float maxX = origin.x + dims.x * cellSize + epsilon;
        float minZ = origin.z - epsilon;
        float maxZ = origin.z + dims.y * cellSize + epsilon;

        // Check if the worldPos is within the XZ bounds of this surface
        if (worldPos.x >= minX && worldPos.x <= maxX &&
            worldPos.z >= minZ && worldPos.z <= maxZ)
        {
            // This position is within the XZ bounds
            // Check if it's the closest surface vertically
            float yDist = Mathf.Abs(worldPos.y - origin.y);
            if (yDist < closestYDist)
            {
                closestYDist = yDist;
                closestSurface = surface;
            }
        }
    }

    return closestSurface;
    }

    private Vector3 CellToWorldCentre(Vector2Int cell, Vector2Int size, GreenhouseSurface surface)
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
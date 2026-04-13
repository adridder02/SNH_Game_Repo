using UnityEngine;
using UnityEngine.InputSystem;         // Core new Input System namespace
using System.Collections.Generic;

/// <summary>
/// Handles placing and removing procedurally-generated pot plants on grid cells.
/// Uses Unity's NEW Input System (com.unity.inputsystem package).
///
/// PACKAGE REQUIREMENT
/// -------------------
/// Make sure the Input System package is installed:
///   Window → Package Manager → search "Input System" → Install
/// When prompted, allow Unity to enable the new backend and restart.
/// In Project Settings → Player → Other Settings → Active Input Handling
/// must be set to "Input System Package (New)" or "Both".
///
/// HOW PLACEMENT WORKS
/// -------------------
/// 1. The player clicks a cell  → ColoredGrid highlights it and stores
///    the selected cell in ColoredGrid.SelectedCell.
/// 2. The player presses the Place action (default: Space)  →
///    PlantPlacer reads the selected cell, checks it is free, spawns a plant
///    at the cell's world-centre, and tells ColoredGrid to mark it occupied.
/// 3. The player presses the Remove action (default: Delete) on a selected
///    occupied cell  → the plant is destroyed and the cell is freed.
/// 4. Left-click is now also handled here via the new Input System so
///    everything is unified in one place; ColoredGrid no longer polls
///    Input.GetMouseButtonDown itself.
///
/// INPUT ACTIONS (configured in code via InputAction)
/// -------------------
/// We create three InputAction objects directly in code so you do NOT need
/// to create an Input Action Asset in the Project.  If you already have an
/// Input Action Asset you prefer, see the "USING AN ACTION ASSET" section
/// at the bottom of this file for how to swap in.
///
/// SETUP
/// -----
/// 1. Attach this script to the SAME GameObject as ColoredGrid.
/// 2. Optionally assign a prefab to `plantPrefab`.  Leave empty for the
///    built-in procedural pot plant.
/// 3. Press Play — click a cell, press Space to place, Delete to remove.
///
/// EXTENDING
/// ----------
/// • Swap `plantPrefab` for any 3-D model.
/// • Rebind actions at runtime via action.ApplyBindingOverride().
/// • Add gamepad support by adding a second binding to each InputAction.
/// </summary>
[RequireComponent(typeof(ColoredGrid))]
public class PlantPlacer : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector-exposed configuration
    // -------------------------------------------------------------------------

    [Header("Plant prefab (optional)")]
    [Tooltip("Drag a prefab here to use as the plant model. " +
             "Leave empty and a simple procedural plant is generated.")]
    public GameObject plantPrefab;

    [Header("Plant scale")]
    [Tooltip("Uniform scale applied to every spawned plant.")]
    public float plantScale = 0.4f;

    [Header("Random variety")]
    [Tooltip("If true, each placed plant gets a slightly randomised scale " +
             "and Y-rotation so they do not all look identical.")]
    public bool randomiseVariety = true;

    // -------------------------------------------------------------------------
    // Input Actions (created in code — no Action Asset needed)
    // -------------------------------------------------------------------------

    // Each InputAction wraps a single binding path.
    // The path strings use Unity's control path syntax:
    //   "<Keyboard>/space"   → Space key on any keyboard
    //   "<Keyboard>/delete"  → Delete key on any keyboard
    //   "<Mouse>/leftButton" → Left mouse button
    // Full path reference: https://docs.unity3d.com/Packages/com.unity.inputsystem@latest

    /// <summary>Place action — fires when the place key is pressed.</summary>
    private InputAction _placeAction;

    /// <summary>Remove action — fires when the remove key is pressed.</summary>
    private InputAction _removeAction;

    /// <summary>Click action — fires on left mouse button press.</summary>
    private InputAction _clickAction;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    /// <summary>Reference to the grid component on the same GameObject.</summary>
    private ColoredGrid _grid;

    /// <summary>
    /// Maps each occupied cell coordinate to the plant GameObject there.
    /// Needed so we can find and destroy the right object on removal.
    /// </summary>
    private Dictionary<Vector2Int, GameObject> _plants
        = new Dictionary<Vector2Int, GameObject>();

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // --- Grab the grid component ----------------------------------------
        _grid = GetComponent<ColoredGrid>();

        // Auto-add a BoxCollider sized to the grid so raycasts can hit it
        _grid.SetupCollider();

        // --- Create and configure the Input Actions --------------------------
        // InputAction constructor: (name, type, binding)
        // ActionType.Button = fires once per press (not held), which is what
        // we want for discrete placement/removal actions.

        _placeAction = new InputAction(
            name:    "Place",
            type:    InputActionType.Button,
            binding: "<Keyboard>/space"   // Space key
        );

        _removeAction = new InputAction(
            name:    "Remove",
            type:    InputActionType.Button,
            binding: "<Keyboard>/delete"  // Delete key
        );

        // Button type — we read Mouse.current.position separately in the
        // callback to get the screen-space cursor position for raycasting.
        _clickAction = new InputAction(
            name:    "Click",
            type:    InputActionType.Button,
            binding: "<Mouse>/leftButton"
        );

        // --- Register callbacks ----------------------------------------------
        // The new Input System is EVENT-DRIVEN.  Instead of polling
        // Input.GetKeyDown every frame, we subscribe a method to the action's
        // .performed event.  The callback fires exactly once per press.

        _placeAction.performed  += OnPlacePerformed;
        _removeAction.performed += OnRemovePerformed;
        _clickAction.performed  += OnClickPerformed;

        // --- Enable the actions ----------------------------------------------
        // Actions must be explicitly enabled before they fire callbacks.
        // We enable them here rather than OnEnable so they are ready
        // immediately after Awake (before the first Update).
        _placeAction.Enable();
        _removeAction.Enable();
        _clickAction.Enable();
    }

    private void OnDestroy()
    {
        // Always unsubscribe callbacks and disable/dispose actions when the
        // object is destroyed.  Failing to do so causes callbacks to fire on
        // a destroyed object and leaks the unmanaged InputAction resources.

        _placeAction.performed  -= OnPlacePerformed;
        _removeAction.performed -= OnRemovePerformed;
        _clickAction.performed  -= OnClickPerformed;

        _placeAction.Disable();
        _removeAction.Disable();
        _clickAction.Disable();

        // Dispose frees the native memory backing the action
        _placeAction.Dispose();
        _removeAction.Dispose();
        _clickAction.Dispose();
    }

    // We no longer need Update() for input — callbacks handle everything.

    // -------------------------------------------------------------------------
    // Input callbacks
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by the Input System exactly once when the left mouse button
    /// is pressed.  Casts a ray into the scene and tells the grid which
    /// cell was clicked.
    ///
    /// CallbackContext carries information about the action event:
    ///   ctx.performed  → true when the action just fired
    ///   ctx.ReadValue  → reads the bound control's current value
    ///   ctx.control    → the specific control that triggered the action
    /// </summary>
    private void OnClickPerformed(InputAction.CallbackContext ctx)
    {
        // Mouse.current is the new Input System equivalent of Input.mousePosition.
        // We read screen position here rather than from ctx because the click
        // action is Button type and carries no positional value of its own.
        Vector2 screenPos = Mouse.current.position.ReadValue();

        // Build a ray from the main camera through the screen-space point
        Ray ray = Camera.main.ScreenPointToRay(screenPos);

        // Raycast against all scene colliders
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        // Only respond if the hit object is this grid (or a direct child of it)
        if (hit.collider.gameObject != gameObject &&
            hit.collider.transform.parent?.gameObject != gameObject)
            return;

        // Convert the world-space hit point to a local grid cell index.
        // Subtract this object's world position so the math works even when
        // the grid is not at the scene origin.
        Vector3 localHit = hit.point - transform.position;

        int cellX = Mathf.FloorToInt(localHit.x / _grid.cellSize);
        int cellZ = Mathf.FloorToInt(localHit.z / _grid.cellSize);

        // Clamp to valid range (floating-point can land exactly on an edge)
        cellX = Mathf.Clamp(cellX, 0, _grid.columns - 1);
        cellZ = Mathf.Clamp(cellZ, 0, _grid.rows    - 1);

        // Delegate selection logic to the grid
        _grid.SelectCell(cellX, cellZ);
    }

    /// <summary>
    /// Called by the Input System once when the Place key (Space) is pressed.
    /// Attempts to place a plant on the currently selected cell.
    /// </summary>
    private void OnPlacePerformed(InputAction.CallbackContext ctx)
    {
        Vector2Int sel = _grid.SelectedCell;

        // Nothing selected → ignore
        if (sel.x < 0) return;

        PlaceAtCell(sel.x, sel.y);
    }

    /// <summary>
    /// Called by the Input System once when the Remove key (Delete) is pressed.
    /// Attempts to remove the plant on the currently selected cell.
    /// </summary>
    private void OnRemovePerformed(InputAction.CallbackContext ctx)
    {
        Vector2Int sel = _grid.SelectedCell;

        if (sel.x < 0) return;

        RemoveAtCell(sel.x, sel.y);
    }

    // -------------------------------------------------------------------------
    // Public placement API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Places a plant at the given grid cell if it is free.
    /// Safe to call from other scripts for programmatic placement.
    /// </summary>
    public bool PlaceAtCell(int x, int z)
    {
        if (!_grid.IsCellAvailable(x, z))
        {
            Debug.Log($"[PlantPlacer] Cell ({x},{z}) is not available.");
            return false;
        }

        Vector3 spawnPos = _grid.GetCellWorldCenter(x, z);

        // Instantiate from prefab or generate a procedural plant
        GameObject plant = plantPrefab != null
            ? Instantiate(plantPrefab, spawnPos, Quaternion.identity)
            : CreateProceduralPlant(spawnPos);

        float scale = plantScale;

        if (randomiseVariety)
        {
            // Vary size ±20 % so plants look natural side-by-side
            scale *= Random.Range(0.8f, 1.2f);

            // Random Y rotation so plants face different directions
            plant.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        }

        plant.transform.localScale = Vector3.one * scale;

        // Parent to this GameObject so the plant moves with the grid and
        // stays tidy in the Hierarchy
        plant.transform.SetParent(transform, worldPositionStays: true);
        plant.name = $"Plant_{x}_{z}";

        // Register in the lookup dictionary so we can find it for removal
        _plants[new Vector2Int(x, z)] = plant;

        // Tell the grid to mark the cell occupied and recolour it green
        _grid.OccupyCell(x, z);

        Debug.Log($"[PlantPlacer] Plant placed at ({x},{z}).");
        return true;
    }

    /// <summary>
    /// Removes the plant at the given grid cell if one exists.
    /// Safe to call from other scripts.
    /// </summary>
    public bool RemoveAtCell(int x, int z)
    {
        Vector2Int key = new Vector2Int(x, z);

        if (!_plants.ContainsKey(key))
        {
            Debug.Log($"[PlantPlacer] No plant at ({x},{z}) to remove.");
            return false;
        }

        Destroy(_plants[key]);
        _plants.Remove(key);

        // Restore the checkerboard colour on the now-empty cell
        _grid.FreeCell(x, z);

        Debug.Log($"[PlantPlacer] Plant removed from ({x},{z}).");
        return true;
    }

    // -------------------------------------------------------------------------
    // Procedural plant mesh generator
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a simple pot-plant from Unity primitives when no prefab is set.
    /// </summary>
    private GameObject CreateProceduralPlant(Vector3 position)
    {
        // Root — parent of all parts; this is what gets Destroyed on removal
        GameObject root = new GameObject("ProceduralPlant");
        root.transform.position = position;

        // Pot body — terracotta cylinder
        MakePart("PotBody", root,
            localPos:   new Vector3(0, 0.25f, 0),
            localScale: new Vector3(0.55f, 0.25f, 0.55f),
            color:      new Color(0.72f, 0.35f, 0.18f),
            primitive:  PrimitiveType.Cylinder);

        // Pot rim — slightly wider, very flat cylinder on top of the body
        MakePart("PotRim", root,
            localPos:   new Vector3(0, 0.52f, 0),
            localScale: new Vector3(0.65f, 0.04f, 0.65f),
            color:      new Color(0.55f, 0.25f, 0.12f),
            primitive:  PrimitiveType.Cylinder);

        // Soil disc — dark brown flat cylinder sitting inside the rim
        MakePart("Soil", root,
            localPos:   new Vector3(0, 0.50f, 0),
            localScale: new Vector3(0.50f, 0.02f, 0.50f),
            color:      new Color(0.25f, 0.15f, 0.08f),
            primitive:  PrimitiveType.Cylinder);

        // Stem — thin tall cylinder
        MakePart("Stem", root,
            localPos:   new Vector3(0, 0.75f, 0),
            localScale: new Vector3(0.05f, 0.18f, 0.05f),
            color:      new Color(0.25f, 0.45f, 0.15f),
            primitive:  PrimitiveType.Cylinder);

        // Main foliage sphere — slightly randomised shape per plant
        float lx = Random.Range(0.38f, 0.55f);
        float ly = Random.Range(0.32f, 0.50f);
        float lz = Random.Range(0.38f, 0.55f);
        float g  = Random.Range(0.45f, 0.75f);

        MakePart("Leaves", root,
            localPos:   new Vector3(0, 1.05f, 0),
            localScale: new Vector3(lx, ly, lz),
            color:      new Color(0.1f, g, 0.1f),
            primitive:  PrimitiveType.Sphere);

        // Optional second foliage cluster offset to one side (60 % chance)
        if (Random.value > 0.4f)
        {
            float ox = Random.Range(-0.12f, 0.12f);
            float oz = Random.Range(-0.12f, 0.12f);
            float s2 = Random.Range(0.22f, 0.35f);
            float g2 = Random.Range(0.35f, 0.65f);

            MakePart("Leaves2", root,
                localPos:   new Vector3(ox, 0.95f, oz),
                localScale: Vector3.one * s2,
                color:      new Color(0.08f, g2, 0.08f),
                primitive:  PrimitiveType.Sphere);
        }

        return root;
    }

    /// <summary>
    /// Creates a single primitive part, parents it, positions it, colours it,
    /// and removes its auto-added Collider.
    /// Extracted to a helper to keep CreateProceduralPlant readable.
    /// </summary>
    private GameObject MakePart(string partName, GameObject parent,
        Vector3 localPos, Vector3 localScale, Color color,
        PrimitiveType primitive)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = partName;
        part.transform.SetParent(parent.transform, worldPositionStays: false);
        part.transform.localPosition = localPos;
        part.transform.localScale    = localScale;

        // New material instance per part so colours do not bleed across objects
        Renderer rend = part.GetComponent<Renderer>();
        Material mat  = new Material(Shader.Find("Standard"));
        mat.color     = color;
        rend.material = mat;

        // Strip the auto-added Collider — per-part physics is not needed
        Destroy(part.GetComponent<Collider>());

        return part;
    }
}

// =============================================================================
// USING AN INPUT ACTION ASSET (optional alternative to inline InputActions)
// =============================================================================
// If you have an Input Action Asset (.inputactions file) with a "Gameplay"
// action map containing "Place", "Remove", and "Click" actions, replace the
// inline InputAction fields and Awake setup with:
//
//   public InputActionAsset actionAsset;   // drag .inputactions file here
//
//   private InputAction _placeAction;
//   private InputAction _removeAction;
//   private InputAction _clickAction;
//
//   private void Awake()
//   {
//       var map = actionAsset.FindActionMap("Gameplay", throwIfNotFound: true);
//       _placeAction  = map.FindAction("Place",  throwIfNotFound: true);
//       _removeAction = map.FindAction("Remove", throwIfNotFound: true);
//       _clickAction  = map.FindAction("Click",  throwIfNotFound: true);
//
//       _placeAction.performed  += OnPlacePerformed;
//       _removeAction.performed += OnRemovePerformed;
//       _clickAction.performed  += OnClickPerformed;
//
//       map.Enable();
//   }
//
//   private void OnDestroy()
//   {
//       _placeAction.performed  -= OnPlacePerformed;
//       _removeAction.performed -= OnRemovePerformed;
//       _clickAction.performed  -= OnClickPerformed;
//       actionAsset.FindActionMap("Gameplay").Disable();
//   }
//
// Everything else (the callbacks, PlaceAtCell, RemoveAtCell) stays identical.
// =============================================================================

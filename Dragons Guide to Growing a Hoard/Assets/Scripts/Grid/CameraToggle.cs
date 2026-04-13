using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Toggles the active camera between the Main Camera and a second camera
/// assigned to Display 2, with raycasting always targeting the active camera.
///
/// CURSOR INDICATOR (Display 2 feature)
/// -------------------------------------
/// When Display 2 is active a world-space cursor marker sits on the grid
/// surface directly beneath the mouse pointer. It is always visible in both
/// cameras simultaneously, so while the player looks at Display 2 they can
/// still see where their cursor is hovering on the grid.
///
/// The indicator is a flat disc (cylinder scaled very thin on Y) with a
/// pulsing scale animation driven in Update(). It updates its world position
/// every frame by casting a ray from the ACTIVE camera through the current
/// mouse position — the same ray ColoredGrid uses for cell selection — so
/// the visual always agrees with what will actually be selected on click.
///
/// The indicator is:
///   • Shown when Display 2 camera is active
///   • Hidden when Main Camera is active (not needed there)
///   • Hidden when the ray misses the grid (cursor off-grid)
///   • Rendered on a layer that both cameras can see (default layer 0)
///
/// SCENE SETUP
/// -----------
/// 1. Your existing scene camera should already be tagged "MainCamera".
/// 2. Create a second Camera:
///      • Name it e.g. "DisplayCamera2"
///      • Target Display → Display 2
///      • Tag: Untagged (this script manages the tag at runtime)
/// 3. Create an empty GameObject "CameraToggle", attach this script.
/// 4. Assign mainCamera, displayCamera2, and the ColoredGrid in the Inspector.
/// 5. Game view toolbar → Display dropdown → enable Display 2.
/// 6. Press Tab to toggle cameras, hover the mouse to see the cursor disc.
///
/// CONTROLS
/// --------
///   Tab (default) → toggle cameras
/// </summary>
public class CameraToggle : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector fields
    // -------------------------------------------------------------------------

    [Header("Cameras")]
    [Tooltip("Your original Main Camera (tagged 'MainCamera' in the scene).")]
    public Camera mainCamera;

    [Tooltip("The second camera set to Target Display → Display 2.")]
    public Camera displayCamera2;

    [Header("Grid reference")]
    [Tooltip("The ColoredGrid GameObject. Used to raycast the cursor position " +
             "against the grid collider for the cursor indicator.")]
    public ColoredGrid grid;

    [Header("Toggle input")]
    [Tooltip("Input System control path for the toggle key. " +
             "Default: Tab. Change to e.g. '<Keyboard>/t' without recompiling.")]
    public string toggleBinding = "<Keyboard>/tab";

    [Header("Cursor indicator")]
    [Tooltip("Colour of the world-space cursor disc shown in Display 2.")]
    public Color cursorColor = new Color(1f, 1f, 1f, 0.6f);

    [Tooltip("Base diameter of the cursor disc in world units. " +
             "Should be slightly smaller than one grid cell so it fits inside.")]
    public float cursorBaseSize = 0.6f;

    [Tooltip("How fast the cursor disc pulses in and out (cycles per second).")]
    public float cursorPulseSpeed = 2.5f;

    [Tooltip("How much the disc grows/shrinks during the pulse (0 = no pulse, " +
             "0.15 = ±15 % size change).")]
    [Range(0f, 0.4f)]
    public float cursorPulseAmount = 0.12f;

    [Tooltip("Vertical offset above the grid surface so the disc does not " +
             "z-fight with the mesh.")]
    public float cursorYOffset = 0.005f;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    /// <summary>Input action that fires when the toggle key is pressed.</summary>
    private InputAction _toggleAction;

    /// <summary>Input action that reads the mouse position every frame.</summary>
    private InputAction _mousePosAction;

    /// <summary>True = mainCamera active, False = displayCamera2 active.</summary>
    private bool _mainCameraActive = true;

    /// <summary>
    /// The world-space cursor indicator GameObject.
    /// A thin cylinder (disc) that hovers on the grid surface under the cursor.
    /// </summary>
    private GameObject _cursorIndicator;

    /// <summary>
    /// Renderer on the cursor indicator, cached to avoid GetComponent every frame.
    /// </summary>
    private Renderer _cursorRenderer;

    /// <summary>
    /// Material on the cursor indicator. Stored so we can update its color
    /// without creating a new material instance every frame.
    /// </summary>
    private Material _cursorMaterial;

    /// <summary>
    /// Tracks whether the cursor ray hit the grid last frame.
    /// Used to show/hide the indicator when the cursor moves on/off the grid.
    /// </summary>
    private bool _cursorOnGrid = false;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // --- Validate assignments -------------------------------------------
        if (mainCamera == null)
        {
            Debug.LogError("[CameraToggle] 'mainCamera' is not assigned.");
            enabled = false;
            return;
        }
        if (displayCamera2 == null)
        {
            Debug.LogError("[CameraToggle] 'displayCamera2' is not assigned.");
            enabled = false;
            return;
        }
        if (grid == null)
        {
            Debug.LogError("[CameraToggle] 'grid' is not assigned. " +
                           "Drag the ColoredGrid GameObject into the slot.");
            enabled = false;
            return;
        }

        // --- Set starting camera state --------------------------------------
        ApplyCameraState(activateMain: true);

        // --- Build Input Actions --------------------------------------------

        // Toggle action: Button type, fires once per press
        _toggleAction = new InputAction(
            name:    "CameraToggle",
            type:    InputActionType.Button,
            binding: toggleBinding
        );
        _toggleAction.performed += OnTogglePerformed;
        _toggleAction.Enable();

        // Mouse position action: Value type, reads a Vector2 every frame.
        // We use this to get the screen-space cursor position for raycasting
        // the cursor indicator without depending on the old Input.mousePosition.
        _mousePosAction = new InputAction(
            name:    "MousePosition",
            type:    InputActionType.Value,
            binding: "<Mouse>/position"
        );
        // No callback needed — we poll ReadValue<Vector2>() in Update()
        // because the cursor indicator updates every frame, not on an event.
        _mousePosAction.Enable();

        // --- Build the cursor indicator ------------------------------------
        BuildCursorIndicator();
    }

    private void Start()
    {
        // Activate Display 2 for standalone builds.
        // Safe to call in the editor (no effect there).
        if (Display.displays.Length > 1)
        {
            Display.displays[1].Activate();
            Debug.Log("[CameraToggle] Display 2 activated.");
        }
    }

    private void Update()
    {
        // Update the cursor indicator position every frame.
        // We only show it when Display 2 is active — in the main camera view
        // the OS cursor is visible directly so a world-space disc is redundant.
        if (!_mainCameraActive)
            UpdateCursorIndicator();
    }

    private void OnDestroy()
    {
        // Clean up Input Actions to prevent stale callbacks and memory leaks
        if (_toggleAction != null)
        {
            _toggleAction.performed -= OnTogglePerformed;
            _toggleAction.Disable();
            _toggleAction.Dispose();
        }
        if (_mousePosAction != null)
        {
            _mousePosAction.Disable();
            _mousePosAction.Dispose();
        }

        // Destroy the cursor indicator GameObject
        if (_cursorIndicator != null)
            Destroy(_cursorIndicator);
    }

    // -------------------------------------------------------------------------
    // Input callback — toggle
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called once per key press. Swaps the active camera and shows/hides
    /// the cursor indicator accordingly.
    /// </summary>
    private void OnTogglePerformed(InputAction.CallbackContext ctx)
    {
        _mainCameraActive = !_mainCameraActive;
        ApplyCameraState(_mainCameraActive);

        // Hide the indicator immediately when switching back to main camera.
        // It will reappear automatically if Display 2 becomes active again.
        if (_mainCameraActive && _cursorIndicator != null)
            _cursorIndicator.SetActive(false);

        Debug.Log($"[CameraToggle] Active camera: " +
                  $"{(_mainCameraActive ? mainCamera.name : displayCamera2.name)}");
    }

    // -------------------------------------------------------------------------
    // Camera state management
    // -------------------------------------------------------------------------

    /// <summary>
    /// Assigns the "MainCamera" tag to the active camera and "Untagged" to
    /// the inactive one, then enables/disables the Camera components.
    ///
    /// Tag is stripped from the outgoing camera FIRST to avoid a frame where
    /// two cameras hold "MainCamera" simultaneously — which would make
    /// Camera.main return an undefined result.
    ///
    /// camera.enabled is used instead of SetActive so that any scripts on
    /// the camera GameObject (e.g. GridCameraController) keep running.
    /// </summary>
    private void ApplyCameraState(bool activateMain)
    {
        if (activateMain)
        {
            displayCamera2.tag = "Untagged";
            displayCamera2.enabled = false;

            mainCamera.tag = "MainCamera";
            mainCamera.enabled = true;
        }
        else
        {
            mainCamera.tag = "Untagged";
            mainCamera.enabled = false;

            displayCamera2.tag = "MainCamera";
            displayCamera2.enabled = true;
        }
    }

    // -------------------------------------------------------------------------
    // Cursor indicator — construction
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates the world-space cursor disc from a Unity Cylinder primitive.
    ///
    /// A Cylinder in Unity is 2 units tall and 1 unit wide by default.
    /// We squash it to near-zero on Y to make a flat disc, then scale X and Z
    /// to match cursorBaseSize. The pivot is at the cylinder's centre, so we
    /// add cursorYOffset on Y to lift it just above the grid mesh.
    ///
    /// The disc starts hidden — UpdateCursorIndicator() shows it once the
    /// cursor moves over the grid.
    /// </summary>
    private void BuildCursorIndicator()
    {
        _cursorIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _cursorIndicator.name = "CursorIndicator";

        // Remove the auto-added collider so the cursor disc does not interfere
        // with the grid raycasts that drive cell selection and indicator position.
        Destroy(_cursorIndicator.GetComponent<Collider>());

        // Flatten into a disc.
        // X/Z = cursorBaseSize, Y = very thin (0.002 world units).
        // The scale animation in UpdateCursorIndicator modifies X and Z only.
        _cursorIndicator.transform.localScale = new Vector3(
            cursorBaseSize,
            0.002f,         // essentially flat
            cursorBaseSize
        );

        // Build a transparent material so the disc is visible but not opaque.
        // Standard shader with Transparent rendering mode.
        _cursorMaterial = new Material(Shader.Find("Standard"));

        // Switch the Standard shader to Transparent mode.
        // These four property/keyword settings are the Unity-documented way
        // to switch render mode in code without a custom shader.
        _cursorMaterial.SetFloat("_Mode", 3);                         // 3 = Transparent
        _cursorMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _cursorMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _cursorMaterial.SetInt("_ZWrite", 0);
        _cursorMaterial.DisableKeyword("_ALPHATEST_ON");
        _cursorMaterial.EnableKeyword("_ALPHABLEND_ON");
        _cursorMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        _cursorMaterial.renderQueue = 3000; // Transparent queue

        _cursorMaterial.color = cursorColor;

        _cursorRenderer = _cursorIndicator.GetComponent<Renderer>();
        _cursorRenderer.material = _cursorMaterial;

        // Start hidden — shown once the cursor hovers over the grid
        _cursorIndicator.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Cursor indicator — per-frame update
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called every frame when Display 2 is active.
    ///
    /// Casts a ray from the ACTIVE camera (Camera.main, which is currently
    /// displayCamera2 due to the tag swap) through the screen-space mouse
    /// position. If the ray hits the grid collider, the disc snaps to the
    /// hit point on the grid surface and pulses. If the ray misses, the disc
    /// is hidden.
    ///
    /// Using Camera.main here (rather than a stored reference to displayCamera2)
    /// is intentional — it means this code requires no changes if more cameras
    /// are added in future, because it always uses whichever camera is active.
    /// </summary>
    private void UpdateCursorIndicator()
    {
        // Read the current screen-space mouse position via the new Input System
        Vector2 screenPos = _mousePosAction.ReadValue<Vector2>();

        // Build a ray from the active camera through the cursor position.
        // Camera.main currently points to displayCamera2 because of the tag swap.
        Ray ray = Camera.main.ScreenPointToRay(screenPos);

        // Raycast against the grid collider only.
        // We filter by checking the hit object belongs to the grid GameObject,
        // so the cursor disc does not "snap" to plants or other scene objects.
        bool hitGrid = Physics.Raycast(ray, out RaycastHit hit) &&
                       (hit.collider.gameObject == grid.gameObject ||
                        hit.collider.transform.parent?.gameObject == grid.gameObject);

        if (hitGrid)
        {
            // Position the disc on the grid surface at the hit point.
            // hit.point is in world space; we add cursorYOffset on Y so the
            // disc floats just above the mesh and avoids z-fighting.
            Vector3 discPos  = hit.point;
            discPos.y       += cursorYOffset;
            _cursorIndicator.transform.position = discPos;

            // --- Pulse animation -------------------------------------------
            // A simple sine wave drives the scale between (1 - amount) and
            // (1 + amount) relative to cursorBaseSize.
            // Time.time gives seconds since the game started.
            float pulse = 1f + Mathf.Sin(Time.time * cursorPulseSpeed * Mathf.PI * 2f)
                              * cursorPulseAmount;

            float size = cursorBaseSize * pulse;
            _cursorIndicator.transform.localScale = new Vector3(size, 0.002f, size);

            // Show the indicator if it was previously hidden
            if (!_cursorOnGrid)
            {
                _cursorIndicator.SetActive(true);
                _cursorOnGrid = true;
            }
        }
        else
        {
            // Cursor is off the grid — hide the disc
            if (_cursorOnGrid)
            {
                _cursorIndicator.SetActive(false);
                _cursorOnGrid = false;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Switches directly to the Main Camera from any other script.</summary>
    public void ActivateMainCamera()
    {
        _mainCameraActive = true;
        ApplyCameraState(true);
        if (_cursorIndicator != null)
            _cursorIndicator.SetActive(false);
    }

    /// <summary>Switches directly to Display Camera 2 from any other script.</summary>
    public void ActivateDisplayCamera2()
    {
        _mainCameraActive = false;
        ApplyCameraState(false);
    }

    /// <summary>True if the Main Camera is currently active.</summary>
    public bool IsMainCameraActive => _mainCameraActive;
}

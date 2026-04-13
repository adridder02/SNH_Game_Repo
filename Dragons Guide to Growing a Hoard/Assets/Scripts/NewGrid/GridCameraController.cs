using UnityEngine;

/// <summary>
/// A lightweight orbit camera designed for viewing a flat grid from above.
///
/// CONTROLS
/// --------
///   Right-mouse drag   → orbit (rotate) around the grid centre
///   Middle-mouse drag  → pan (translate) the camera target point
///   Scroll wheel       → zoom in / out
///
/// SETUP
/// -----
/// 1. Create a new empty GameObject called "CameraRig" in your scene.
/// 2. Attach this script to it.
/// 3. Set `target` to the grid's centre in the Inspector, or leave it and the
///    script will orbit the world origin.  To auto-centre on the grid,
///    assign the ColoredGrid GameObject to `gridObject` below and the script
///    will compute the centre automatically on Start.
/// 4. The script repositions the Main Camera — make sure one exists with the
///    "MainCamera" tag.
/// </summary>
public class GridCameraController : MonoBehaviour
{
    [Header("Auto-centre on grid (optional)")]
    [Tooltip("Assign the ColoredGrid GameObject and the camera will " +
             "automatically orbit its centre.  Leave empty to use `target`.")]
    public ColoredGrid gridObject;

    [Header("Orbit target")]
    [Tooltip("World-space point the camera orbits around.  " +
             "Overridden if gridObject is set.")]
    public Vector3 target = Vector3.zero;

    [Header("Initial angles")]
    [Tooltip("Starting horizontal angle around Y axis (degrees).")]
    public float startYaw   = 30f;

    [Tooltip("Starting vertical angle above the horizon (degrees).  " +
             "90 = top-down, 0 = side-on.")]
    public float startPitch = 55f;

    [Header("Initial zoom")]
    [Tooltip("Starting distance from target (world units).")]
    public float startDistance = 12f;

    [Header("Orbit sensitivity")]
    public float orbitSpeed = 180f; // degrees per second at full drag speed

    [Header("Pan sensitivity")]
    public float panSpeed = 0.01f;  // world units per pixel of drag

    [Header("Zoom")]
    public float zoomSpeed    = 2f;
    public float minDistance  = 2f;
    public float maxDistance  = 40f;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private float _yaw;       // current horizontal angle
    private float _pitch;     // current vertical angle
    private float _distance;  // current distance from target
    private Camera _cam;      // cached reference to Main Camera

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        // Cache the main camera
        _cam = Camera.main;
        if (_cam == null)
        {
            Debug.LogError("[GridCameraController] No Main Camera found. " +
                           "Tag your camera as 'MainCamera'.");
            enabled = false;
            return;
        }

        // If a grid object is assigned, orbit its centre
        if (gridObject != null)
        {
            // The grid's centre is half its total world-space extent on X and Z
            target = gridObject.transform.position + new Vector3(
                gridObject.columns * gridObject.cellSize * 0.5f,
                0f,
                gridObject.rows    * gridObject.cellSize * 0.5f
            );

            // Also auto-set a sensible starting distance based on grid size
            float diag = Mathf.Sqrt(
                Mathf.Pow(gridObject.columns * gridObject.cellSize, 2) +
                Mathf.Pow(gridObject.rows    * gridObject.cellSize, 2)
            );
            startDistance = Mathf.Clamp(diag * 0.8f, minDistance, maxDistance);
        }

        // Initialise angles and distance from inspector values
        _yaw      = startYaw;
        _pitch    = startPitch;
        _distance = startDistance;

        // Apply immediately so the first frame is not at the origin
        ApplyCameraTransform();
    }

    private void LateUpdate()
    {
        // LateUpdate runs after all Update calls, which is the correct place
        // to move the camera so it reflects any scene changes from Update.

        HandleOrbit();
        HandlePan();
        HandleZoom();
        ApplyCameraTransform();
    }

    // -------------------------------------------------------------------------
    // Input handlers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Right-mouse drag → change yaw and pitch.
    /// </summary>
    private void HandleOrbit()
    {
        if (!Input.GetMouseButton(1)) return; // right mouse button held

        // Input.GetAxis returns a value normalised to roughly ±1 per frame,
        // independent of frame rate.  Multiply by orbitSpeed to get degrees/sec.
        _yaw   += Input.GetAxis("Mouse X") * orbitSpeed * Time.deltaTime;
        _pitch -= Input.GetAxis("Mouse Y") * orbitSpeed * Time.deltaTime;

        // Clamp pitch so the camera cannot flip upside-down
        _pitch = Mathf.Clamp(_pitch, 5f, 89f);
    }

    /// <summary>
    /// Middle-mouse drag → pan the orbit target point.
    /// Panning moves in the camera's local XY plane so "up" on screen
    /// always moves the target "up" on screen regardless of orbit angle.
    /// </summary>
    private void HandlePan()
    {
        if (!Input.GetMouseButton(2)) return; // middle mouse button held

        float dx = Input.GetAxis("Mouse X");
        float dy = Input.GetAxis("Mouse Y");

        // Move target along camera's right and up vectors, scaled by distance
        // so panning feels the same speed at any zoom level.
        target -= _cam.transform.right * (dx * panSpeed * _distance);
        target -= _cam.transform.up    * (dy * panSpeed * _distance);
    }

    /// <summary>
    /// Scroll wheel → zoom (change distance from target).
    /// </summary>
    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Approximately(scroll, 0f)) return;

        // Negative scroll = zoom out, positive = zoom in
        _distance -= scroll * zoomSpeed * _distance; // scale by distance for
                                                      // smooth feel at all ranges
        _distance  = Mathf.Clamp(_distance, minDistance, maxDistance);
    }

    // -------------------------------------------------------------------------
    // Camera transform application
    // -------------------------------------------------------------------------

    /// <summary>
    /// Converts the current yaw, pitch, and distance into a camera position
    /// and rotation, then applies them to the Main Camera.
    ///
    /// The orbit maths:
    ///   1. Start at (0, 0, -distance) in camera-local space (behind the target).
    ///   2. Rotate by pitch around the X axis (tilt up/down).
    ///   3. Rotate by yaw   around the Y axis (spin left/right).
    ///   4. Add the target offset to get world position.
    ///   5. Point the camera back at the target with LookAt.
    /// </summary>
    private void ApplyCameraTransform()
    {
        // Build a rotation from the current yaw and pitch angles
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);

        // Offset from target: start pointing along -Z, then apply rotation
        Vector3 offset = rotation * new Vector3(0f, 0f, -_distance);

        // Place the camera at target + offset
        _cam.transform.position = target + offset;

        // Always look at the target
        _cam.transform.LookAt(target);
    }
}

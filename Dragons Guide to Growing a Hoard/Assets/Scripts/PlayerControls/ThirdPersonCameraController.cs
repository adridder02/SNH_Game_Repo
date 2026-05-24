using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCameraController : MonoBehaviour
{
    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float zoomLerpSpeed = 10f;
    [SerializeField] private float minDistance = 3f;
    [SerializeField] private float maxDistance = 15f;

    [Header("Sensitivity")]
    [SerializeField] private float mouseSensitivityX = 4.5f;
    [SerializeField] private float mouseSensitivityY = 4.5f;

    [Header("Vertical Look Limits (degrees)")]
    [Tooltip("How far down the camera can look (use a negative value, e.g. -30).")]
    [SerializeField] private float minPitchAngle = -30f;
    [Tooltip("How far up the camera can look (e.g. 70).")]
    [SerializeField] private float maxPitchAngle = 70f;

    [Header("Collision")]
    [Tooltip("Layers the camera will collide with. Exclude the Player layer.")]
    [SerializeField] private LayerMask collisionMask = ~0;         // everything by default
    [Tooltip("How quickly the camera pulls in when a collision is detected.")]
    [SerializeField] private float collisionPullInSpeed = 20f;
    [Tooltip("How quickly the camera eases back out once the obstruction clears.")]
    [SerializeField] private float collisionPullOutSpeed = 4f;
    [Tooltip("Small buffer so the camera doesn't sit flush against a surface.")]
    [SerializeField] private float collisionBuffer = 0.3f;

    // ── private ──────────────────────────────────────
    private PlayerControls controls;
    private CinemachineCamera cam;
    private CinemachineOrbitalFollow orbital;
    private CinemachineInputAxisController inputAxis;

    private Vector2 scrollDelta;
    private float targetZoom;
    private float currentZoom;

    // collision — tracks the radius we actually hand to Cinemachine each frame
    private float collisionZoom;

    public static bool CameraLocked = false;

    // ─────────────────────────────────────────────────
    void Start()
    {
        controls = new PlayerControls();
        controls.Enable();
        controls.Camera.MouseZoom.performed += HandleMouseScroll;

        cam = GetComponent<CinemachineCamera>();
        orbital = cam.GetComponent<CinemachineOrbitalFollow>();
        inputAxis = cam.GetComponent<CinemachineInputAxisController>();

        targetZoom = currentZoom = collisionZoom = orbital.Radius;

        ConfigureAxes();
    }

    private void OnDestroy()
    {
        if (controls != null)
        {
            controls.Camera.MouseZoom.performed -= HandleMouseScroll;
            controls.Disable();
        }
    }

    // ─────────────────────────────────────────────────
    private void HandleMouseScroll(InputAction.CallbackContext context)
    {
        scrollDelta = context.ReadValue<Vector2>();
    }

    void Update()
    {
        // CameraLocked = true  → menu is open, lock the camera.
        // Placement mode alone (grid mode via F, no menu) → camera stays free.
        if (CameraLocked)
        {
            scrollDelta = Vector2.zero;
            if (inputAxis != null) inputAxis.enabled = false;
            return;
        }

        // Re-enable look input when not locked
        if (inputAxis != null) inputAxis.enabled = true;

        // ── Zoom intent ───────────────────────────────
        if (scrollDelta.y != 0f && orbital != null)
        {
            targetZoom = Mathf.Clamp(
                orbital.Radius - scrollDelta.y * zoomSpeed,
                minDistance, maxDistance);
            scrollDelta = Vector2.zero;
        }

        currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * zoomLerpSpeed);

        // ── Collision check ───────────────────────────
        float desiredRadius = ResolveCollision(currentZoom);

        float lerpSpeed = desiredRadius < collisionZoom
            ? collisionPullInSpeed   // obstruction found  → snap in fast
            : collisionPullOutSpeed; // obstruction gone   → ease back slowly

        collisionZoom = Mathf.Lerp(collisionZoom, desiredRadius, Time.deltaTime * lerpSpeed);

        orbital.Radius = collisionZoom;

        // Re-apply pitch limits every frame — Cinemachine can drift these
        // when the radius changes, causing the look-up range to shrink on zoom.
        if (orbital != null)
        {
            orbital.VerticalAxis.Range = new Vector2(minPitchAngle, maxPitchAngle);
            orbital.VerticalAxis.Wrap = false;
        }
    }

    // ─────────────────────────────────────────────────
    /// <summary>
    /// Casts a ray from the follow target toward the ideal camera position.
    /// Returns the safe radius (≤ currentZoom) that keeps the camera clear of geometry.
    /// </summary>
    private float ResolveCollision(float desiredRadius)
    {
        if (orbital == null) return desiredRadius;

        // The follow target is whatever the orbital is tracking.
        // We grab it from the CinemachineCamera's Follow transform.
        Transform follow = cam.Follow;
        if (follow == null) return desiredRadius;

        // Direction from target toward where the camera wants to sit.
        // CinemachineOrbitalFollow positions the camera behind/above the target
        // along the orbit rig's current orientation — we approximate that with
        // -cam.transform.forward, which is accurate once Cinemachine has updated.
        Vector3 origin = follow.position;
        Vector3 direction = -cam.transform.forward;

        if (Physics.SphereCast(
                origin,
                collisionBuffer,
                direction,
                out RaycastHit hit,
                desiredRadius,
                collisionMask,
                QueryTriggerInteraction.Ignore))
        {
            // Pull the camera in to just before the hit point.
            return Mathf.Clamp(hit.distance, minDistance, desiredRadius);
        }

        return desiredRadius;
    }

    // ─────────────────────────────────────────────────
    /// <summary>
    /// Sets sensitivity on the InputAxisController (gain only — that's all the
    /// Controller struct exposes). Pitch limits are set directly on the
    /// CinemachineOrbitalFollow.VerticalAxis, which owns the Range/Wrap fields.
    /// </summary>
    private void ConfigureAxes()
    {
        // ── Sensitivity (via CinemachineInputAxisController) ──────────────
        if (inputAxis != null)
        {
            foreach (var c in inputAxis.Controllers)
            {
                if (c.Name == "Look Orbit X")
                    c.Input.Gain = mouseSensitivityX;
                else if (c.Name == "Look Orbit Y")
                    c.Input.Gain = -mouseSensitivityY;
            }
        }

        // ── Pitch limits (via CinemachineOrbitalFollow.VerticalAxis) ──────
        if (orbital != null)
        {
            orbital.VerticalAxis.Range = new Vector2(minPitchAngle, maxPitchAngle);
            orbital.VerticalAxis.Wrap = false;
        }
    }

    // ─────────────────────────────────────────────────
    /// <summary>Public API — call from a Settings UI.</summary>
    public void SetSensitivity(float horizontal, float vertical)
    {
        mouseSensitivityX = horizontal;
        mouseSensitivityY = vertical;
        ConfigureAxes();
    }

    /// <summary>Backward-compatible single-value overload.</summary>
    public void setSensitivity(float newSpeed) => SetSensitivity(newSpeed, newSpeed);
}
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

    // ── private ──────────────────────────────────────
    private PlayerControls controls;
    private CinemachineCamera cam;
    private CinemachineOrbitalFollow orbital;
    private CinemachineInputAxisController inputAxis;

    private Vector2 scrollDelta;
    private float targetZoom;
    private float currentZoom;

    // ─────────────────────────────────────────────────
    void Start()
    {
        controls = new PlayerControls();
        controls.Enable();
        controls.Camera.MouseZoom.performed += HandleMouseScroll;

        cam = GetComponent<CinemachineCamera>();
        orbital = cam.GetComponent<CinemachineOrbitalFollow>();
        inputAxis = cam.GetComponent<CinemachineInputAxisController>();

        targetZoom = currentZoom = orbital.Radius;

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
        if (GameInputModeManager.Instance != null &&
            GameInputModeManager.Instance.CurrentMode ==
            GameInputModeManager.InputMode.Placement)
        {
            scrollDelta = Vector2.zero;
            return;
        }

        if (scrollDelta.y != 0f && orbital != null)
        {
            targetZoom = Mathf.Clamp(
                orbital.Radius - scrollDelta.y * zoomSpeed,
                minDistance, maxDistance);
            scrollDelta = Vector2.zero;
        }

        currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * zoomLerpSpeed);
        orbital.Radius = currentZoom;
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
        // VerticalAxis.Range clamps how many degrees up/down the camera can go.
        // Wrap must be false, otherwise it loops past the limits.
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
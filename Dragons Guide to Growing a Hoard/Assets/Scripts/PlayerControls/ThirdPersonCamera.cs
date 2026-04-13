using UnityEngine;
using UnityEngine.InputSystem;
/// <summary>
/// Attaches to the Main Camera. Follows and orbits a target (the CameraTarget
/// child of the player). No Cinemachine required.
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The CameraTarget transform on the player (empty child GameObject).")]
    [SerializeField] private Transform target;

    [Header("Distance")]
    [SerializeField] private float distance       = 4f;
    [SerializeField] private float minDistance    = 1f;
    [SerializeField] private float maxDistance    = 8f;
    [SerializeField] private float zoomSpeed      = 2f;
    [SerializeField] private float zoomSmoothTime = 0.15f;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionLayers;
    [SerializeField] private float collisionRadius = 0.2f;
    [SerializeField] private float collisionOffset = 0.1f;

    [Header("Follow Smoothing")]
    [SerializeField] private float positionSmoothTime = 0.1f;

    // Private state
    private float   _currentDistance;
    private float   _targetDistance;
    private float   _zoomVelocity;
    private Vector3 _positionVelocity;

    private void Awake()
    {
        _currentDistance = distance;
        _targetDistance  = distance;

        // Hide and lock cursor for mouse-look
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    /// <summary>
    /// LateUpdate ensures the camera moves AFTER the player has been moved
    /// by ThirdPersonController this frame.
    /// </summary>
    private void LateUpdate()
    {
        if (target == null) return;

        HandleZoom();
        FollowTarget();
    }

    private InputAction scrollAction;

    void OnEnable()
    {
        // Create a new InputAction bound to the mouse scroll
        scrollAction = new InputAction(type: InputActionType.Value, binding: "<Mouse>/scroll");
        scrollAction.Enable();
    }

    void OnDisable()
    {
        scrollAction.Disable();
    }

    private void HandleZoom()
    {  
        // Read the scroll value (Vector2: x = horizontal, y = vertical) 
        Vector2 scrollValue = scrollAction.ReadValue<Vector2>();
        // Equivalent to old Input.GetAxis("Mouse ScrollWheel")
        float scroll = scrollValue.y;
        _targetDistance = Mathf.Clamp(_targetDistance - scroll * zoomSpeed,
                                      minDistance, maxDistance);

        _currentDistance = Mathf.SmoothDamp(
            _currentDistance, _targetDistance,
            ref _zoomVelocity, zoomSmoothTime);
    }

    private void FollowTarget()
    {
        // The CameraTarget already stores the correct pitch/yaw rotation
        // (set each frame by ThirdPersonController.HandleCameraRotation).
        Quaternion rotation  = target.rotation;
        Vector3    direction = rotation * Vector3.back; // "behind" the target

        // Collision: push camera forward if something is in the way
        float actualDistance = _currentDistance;

        if (Physics.SphereCast(
                target.position,
                collisionRadius,
                direction,
                out RaycastHit hit,
                _currentDistance,
                collisionLayers,
                QueryTriggerInteraction.Ignore))
        {
            actualDistance = Mathf.Max(hit.distance - collisionOffset, minDistance);
        }

        // Desired camera position
        Vector3 desiredPosition = target.position + direction * actualDistance;

        // Smooth follow
        transform.position = Vector3.SmoothDamp(
            transform.position, desiredPosition,
            ref _positionVelocity, positionSmoothTime);

        // Always look at the camera target
        transform.LookAt(target.position);
    }

    /// <summary>Call from a pause menu to release/restore cursor.</summary>
    public static void SetCursorState(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !locked;
    }
}

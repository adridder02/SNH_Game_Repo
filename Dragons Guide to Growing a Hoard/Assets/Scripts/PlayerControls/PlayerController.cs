using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  Inspector
    // ──────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -9.8f;
    [SerializeField] private bool shouldFaceMoveDirection = false;

    [Header("Sprint")]
    [Tooltip("Speed multiplier applied while holding Left or Right Shift.")]
    [SerializeField] private float sprintMultiplier = 2f;

    [Header("Flying")]
    [Tooltip("Time window (seconds) to press Space a second time to enter fly mode.")]
    [SerializeField] private float doubleTapWindow = 0.3f;
    [Tooltip("Upward burst speed applied on fly mode entry to clear the ground quickly.")]
    [SerializeField] private float flyLiftSpeed = 12f;
    [Tooltip("How long (seconds) after entering fly mode before ground detection re-enables.")]
    [SerializeField] private float flyGroundGracePeriod = 0.4f;
    [Tooltip("Vertical speed while flying (Space = up, Ctrl = down).")]
    [SerializeField] private float flyVerticalSpeed = 5f;
    [Tooltip("How strongly camera pitch steers vertical movement while flying (0 = off).")]
    [SerializeField] private float flyPitchInfluence = 1f;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    // ──────────────────────────────────────────────
    //  Private state
    // ──────────────────────────────────────────────
    private enum LocomotionState { Grounded, Jumping, Flying }

    private InputActionMap gameplayMap;
    private InputAction flyAction;   // Existing "Fly" action (Space)

    private CharacterController controller;

    private Vector2 moveInput;
    private Vector3 velocity;   // gravity / jump velocity (unused during Flying)

    private LocomotionState locomotionState = LocomotionState.Grounded;

    // Double-tap tracking
    private float lastSpacePressTime = -999f;

    // Flying vertical intent
    private bool flyAscendHeld;       // Space held while flying
    private float flyGroundGraceTimer; // countdown after entering fly, ignores isGrounded
    // Ctrl is polled via Keyboard API — no InputActionAsset mutation needed

    private bool movementEnabled = true;

    // ──────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────

    /// <summary>Returns true when either Shift key is held.</summary>
    private bool IsSprinting =>
        Keyboard.current != null &&
        (Keyboard.current.leftShiftKey.isPressed ||
         Keyboard.current.rightShiftKey.isPressed);

    /// <summary>Current speed, boosted by sprintMultiplier while sprinting.</summary>
    private float CurrentSpeed => speed * (IsSprinting ? sprintMultiplier : 1f);

    // ──────────────────────────────────────────────
    //  Unity lifecycle
    // ──────────────────────────────────────────────
    private void Awake()
    {
        gameplayMap = inputActions.FindActionMap("GamePlay", true);
        flyAction = gameplayMap.FindAction("Fly", true);
    }

    private void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        gameplayMap.Enable();
        flyAction.performed += OnSpacePressed;
        flyAction.canceled += OnSpaceReleased;
    }

    private void OnDisable()
    {
        if (flyAction != null)
        {
            flyAction.performed -= OnSpacePressed;
            flyAction.canceled -= OnSpaceReleased;
        }
        gameplayMap?.Disable();
    }

    // ──────────────────────────────────────────────
    //  Public API (used by GameInputModeManager)
    // ──────────────────────────────────────────────
    public void SetMovementEnabled(bool value)
    {
        movementEnabled = value;
        if (!value)
        {
            moveInput = Vector2.zero;
            velocity = Vector3.zero;
            flyAscendHeld = false;
        }
    }

    // ──────────────────────────────────────────────
    //  Input callbacks (Inspector-wired)
    // ──────────────────────────────────────────────
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    /// <summary>
    /// Kept so existing Inspector wiring continues to work.
    /// Actual logic is handled via the subscribed OnSpacePressed.
    /// </summary>
    public void OnFly(InputAction.CallbackContext context) { }

    // ──────────────────────────────────────────────
    //  Space press / release
    // ──────────────────────────────────────────────
    private void OnSpacePressed(InputAction.CallbackContext ctx)
    {
        if (!movementEnabled) return;

        switch (locomotionState)
        {
            case LocomotionState.Grounded:
                Jump();
                locomotionState = LocomotionState.Jumping;
                lastSpacePressTime = Time.time;
                break;

            case LocomotionState.Jumping:
                if (Time.time - lastSpacePressTime <= doubleTapWindow)
                    EnterFlyMode();
                else
                    lastSpacePressTime = Time.time; // reset window, no double-jump
                break;

            case LocomotionState.Flying:
                flyAscendHeld = true;
                break;
        }
    }

    private void OnSpaceReleased(InputAction.CallbackContext ctx)
    {
        flyAscendHeld = false;
    }

    // ──────────────────────────────────────────────
    //  Update
    // ──────────────────────────────────────────────
    private void Update()
    {
        if (!movementEnabled) return;

        switch (locomotionState)
        {
            case LocomotionState.Grounded:
            case LocomotionState.Jumping:
                UpdateNormalLocomotion();
                break;

            case LocomotionState.Flying:
                UpdateFlyingLocomotion();
                break;
        }
    }

    // ──────────────────────────────────────────────
    //  Normal locomotion (walk + gravity)
    // ──────────────────────────────────────────────
    private void UpdateNormalLocomotion()
    {
        WalkHorizontal();

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        if (controller.isGrounded)
        {
            if (locomotionState == LocomotionState.Jumping)
                locomotionState = LocomotionState.Grounded;

            if (velocity.y < 0f)
                velocity.y = -2f; // small constant keeps player pressed to ground
        }
    }

    private void Jump()
    {
        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    // ──────────────────────────────────────────────
    //  Flying locomotion
    // ──────────────────────────────────────────────
    private void EnterFlyMode()
    {
        locomotionState = LocomotionState.Flying;
        velocity = Vector3.zero;
        flyGroundGraceTimer = flyGroundGracePeriod; // ignore isGrounded briefly
        Debug.Log("[PlayerController] Fly mode ON");
    }

    private void UpdateFlyingLocomotion()
    {
        // Horizontal — yaw-only camera direction, sprint-aware
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 horizontalMove =
            (forward * moveInput.y + right * moveInput.x) * CurrentSpeed;

        // Vertical
        float verticalMove = 0f;

        // Tick grace timer — during this window the player lifts off and we
        // ignore isGrounded so the mode doesn't cancel itself immediately.
        if (flyGroundGraceTimer > 0f)
        {
            flyGroundGraceTimer -= Time.deltaTime;
            // Apply a strong upward burst until clear of the ground
            verticalMove += flyLiftSpeed;
        }
        else
        {
            // Normal fly controls only after grace period

            // Space held → ascend (sprint multiplier applies here too)
            if (flyAscendHeld)
                verticalMove += flyVerticalSpeed * (IsSprinting ? sprintMultiplier : 1f);

            // Ctrl held → descend (sprint multiplier applies here too)
            if (Keyboard.current != null &&
                (Keyboard.current.leftCtrlKey.isPressed ||
                 Keyboard.current.rightCtrlKey.isPressed))
            {
                verticalMove -= flyVerticalSpeed * (IsSprinting ? sprintMultiplier : 1f);
            }

            // Camera pitch → nudge vertical ONLY while actively moving horizontally.
            // This prevents passive hover from drifting due to any non-zero camera pitch.
            if (flyPitchInfluence > 0f && moveInput.sqrMagnitude > 0.01f)
            {
                // cameraTransform.forward.y == sin(pitch): +1 straight up, -1 straight down
                verticalMove += cameraTransform.forward.y * flyPitchInfluence * CurrentSpeed;
            }

            // Grounded → exit fly mode (only checked after grace period)
            if (controller.isGrounded)
            {
                ExitFlyMode();
                return;
            }
        }

        controller.Move((horizontalMove + Vector3.up * verticalMove) * Time.deltaTime);

        // Optional: face horizontal movement direction
        if (shouldFaceMoveDirection && horizontalMove.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(horizontalMove.normalized, Vector3.up),
                10f * Time.deltaTime);
        }
    }

    private void ExitFlyMode()
    {
        locomotionState = LocomotionState.Grounded;
        velocity = Vector3.zero;
        flyAscendHeld = false;
        Debug.Log("[PlayerController] Fly mode OFF – landed");
    }

    // ──────────────────────────────────────────────
    //  Shared helpers
    // ──────────────────────────────────────────────
    private void WalkHorizontal()
    {
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 dir = forward * moveInput.y + right * moveInput.x;
        controller.Move(dir * CurrentSpeed * Time.deltaTime);

        if (shouldFaceMoveDirection && dir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(dir, Vector3.up),
                10f * Time.deltaTime);
        }
    }
}
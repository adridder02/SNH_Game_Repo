using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  Inspector
    // ──────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float jumpHeight = 5f;//changed jump hieght
    [SerializeField] private float gravity = -9.8f;

    //Finding layers to jump from 
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private bool shouldFaceMoveDirection = true;

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
    private bool escPressed = false;

    [Header("Tutorial Hookup")]
    [Tooltip("The MissionData asset that owns the six 'movement basics' bottom-bar tutorial tasks " +
             "(WASD move, jump, double-space fly, tilt up, tilt down, land). Leave blank to disable " +
             "auto-completion — nothing else here changes. Each MissionTaskEntry's Task Id on that " +
             "asset must exactly match one of the TaskXxx constants below.")]
    [SerializeField] private MissionData movementMission;

    // Task Ids — copy these exactly into the matching MissionTaskEntry's "Task Id" field on the
    // movementMission asset, and into each bottom-bar TutorialStep's Linked Task Id in the Inspector.
    private const string TaskMoveWasd = "move_wasd";
    private const string TaskJumpSpace = "jump_space";
    private const string TaskFlyDoubleSpace = "fly_double_space";
    private const string TaskFlyUpTilt = "fly_up_tilt";
    private const string TaskFlyDownTilt = "fly_down_tilt";
    private const string TaskLandGround = "land_ground";

    /*[SerializeField] private Animator animator;

    // Animator hashes — faster than string lookups
    private static readonly int HashSpeed      = Animator.StringToHash("Speed");
    private static readonly int HashIsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int HashIsFlying   = Animator.StringToHash("IsFlying");
    private static readonly int HashJump       = Animator.StringToHash("Jump");
    */

    // ──────────────────────────────────────────────
    //  Private state
    // ──────────────────────────────────────────────
    private enum LocomotionState { Grounded, Jumping, Flying }
    private playerAnimation playerAnim;

    private InputActionMap gameplayMap;
    private InputAction flyAction;   // Existing "Fly" action (Space)
    private InputAction inventoryAction;
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
        Keyboard.current.leftShiftKey.isPressed //||
                                                //Keyboard.current.rightShiftKey.isPressed)
         ;

    /// <summary>Current speed, boosted by sprintMultiplier while sprinting.</summary>
    private float CurrentSpeed => speed * (IsSprinting ? sprintMultiplier : 1f);

    // ──────────────────────────────────────────────
    //  Unity lifecycle
    // ──────────────────────────────────────────────
    private void Awake()
    {
        if (inputActions != null)
        {
            gameplayMap = inputActions.FindActionMap("GamePlay", true);
            flyAction = gameplayMap?.FindAction("Fly", true);
            inventoryAction = gameplayMap?.FindAction("Inventory", true);
        }
    }

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        playerAnim = GetComponent<playerAnimation>();
        if (playerAnim == null)
        {
            Debug.LogError("playerAnimation component is missing from " + gameObject.name);
        }

    }

    private void OnEnable()
    {
        gameplayMap?.Enable();

        if (flyAction != null)
        {
            flyAction.Enable();
            flyAction.performed += OnSpacePressed;
            flyAction.canceled += OnSpaceReleased;
        }

        if (inventoryAction != null)
        {
            inventoryAction.performed += OnInventory;
        }
    }

    private void OnDisable()
    {
        if (flyAction != null)
        {
            flyAction.performed -= OnSpacePressed;
            flyAction.canceled -= OnSpaceReleased;
        }

        if (inventoryAction != null)
        {
            inventoryAction.performed -= OnInventory;
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
    //  Public API (used by external launchers, e.g. SproionshroomLauncher)
    // ──────────────────────────────────────────────
    /// <summary>
    /// Called by external objects (launch pads, bounce plants, etc.) that want to override the
    /// player's current gravity/jump velocity directly. Puts the controller into the Jumping state
    /// so normal gravity integration in UpdateNormalLocomotion() takes over from the given velocity
    /// on the very next frame, and so the landing/animator logic treats this like an ordinary
    /// airborne arc. Does nothing while flying or while movement is disabled, since both of those
    /// states manage `velocity` themselves and would otherwise immediately overwrite this.
    /// </summary>
    public void ApplyExternalLaunch(Vector3 launchVelocity)
    {
        if (!movementEnabled || locomotionState == LocomotionState.Flying) return;

        velocity = launchVelocity;
        locomotionState = LocomotionState.Jumping;
        lastSpacePressTime = Time.time;
        playerAnim?.jump();
    }

    // ──────────────────────────────────────────────
    //  Tutorial task reporting
    // ──────────────────────────────────────────────
    /// <summary>No-ops safely if movementMission isn't assigned or MissionProgressManager doesn't
    /// exist yet — safe to call every frame from Update, MissionProgressManager.CompleteTask only
    /// fires its changed event the first time a given task is actually completed. Also gated to only
    /// complete taskId if it's the mission's next incomplete task, so an early/stray action (e.g.
    /// landing after the very first jump, well before the tutorial's "land" step) doesn't get banked
    /// out of order and cause the sequence to skip steps later on.</summary>
    private void CompleteMovementTask(string taskId)
    {
        if (movementMission == null || MissionProgressManager.Instance == null) return;
        if (!MissionProgressManager.Instance.IsNextTask(movementMission, taskId)) return;
        MissionProgressManager.Instance.CompleteTask(movementMission.ResolvedId, taskId);
    }

    // ──────────────────────────────────────────────
    //  Input callbacks
    // ──────────────────────────────────────────────
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();

        if (moveInput.sqrMagnitude > 0.01f)
            CompleteMovementTask(TaskMoveWasd);
    }

    public void OnInventory(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            // Let InventoryUIController handle the actual toggle
            // (it already listens directly to the action)
            Debug.Log("Inventory key pressed");
        }
    }

    /// <summary>
    /// Kept so existing Inspector wiring continues to work.
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
                    lastSpacePressTime = Time.time;
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

        UpdateAnimator();
        MenuVisiablity();
    }

    void MenuVisiablity()
    {
        // if (Keyboard.current == null) return;

        // bool altHeld = Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed;

        // if (altHeld)
        // {
        //     Cursor.lockState = CursorLockMode.None;
        //     Cursor.visible = true;
        //     ThirdPersonCameraController.CameraLocked = true;
        // }
        // // else if (Keyboard.current.escapeKey.wasPressedThisFrame)
        // // {
        // //     escPressed = !escPressed;

        // //     if (escPressed)
        // //     {
        // //         Cursor.lockState = CursorLockMode.None;
        // //         Cursor.visible = true;
        // //         ThirdPersonCameraController.CameraLocked = true;
        // //     }
        // //     else
        // //     {
        // //         Cursor.lockState = CursorLockMode.Locked;
        // //         Cursor.visible = false;
        // //         ThirdPersonCameraController.CameraLocked = false;
        // //     }
        // // }
        // else
        // {
        //     Cursor.lockState = CursorLockMode.Locked;
        //     Cursor.visible = false;
        //     ThirdPersonCameraController.CameraLocked = false;
        // }
    }

    //Collision Detection test
    void OnCollisionEnter(Collision collision)
    {
        GameObject otherObj = collision.gameObject;

        // Objects that apply their own external launch (e.g. SproionshroomLauncher) manage the
        // player's velocity/state themselves via ApplyExternalLaunch(). Skip the normal auto-jump
        // here so this handler doesn't race it and stomp the launch velocity back down to a regular
        // jump height on the same frame (script execution order between two GameObjects reacting to
        // the same collision isn't guaranteed).
        if (otherObj.GetComponent<SproionshroomLauncher>() != null) return;

        if (locomotionState == LocomotionState.Grounded && ((groundLayers.value & (1 << otherObj.layer)) != 0))
        {
            Jump();
            locomotionState = LocomotionState.Jumping;
            lastSpacePressTime = Time.time;
        }
    }

    private void UpdateAnimator()
    {
        if (playerAnim == null) return;

        // Grounded locomotion
        if (locomotionState == LocomotionState.Grounded)
        {
            float inputMagnitude = moveInput.magnitude;
            bool isSprinting = IsSprinting;

            if (inputMagnitude > 0.8f && isSprinting)
            {
                playerAnim.setRunning();
            }
            else if (inputMagnitude > 0.1f)
            {
                playerAnim.setWalking();
                // NOTE: this used to advance the old on-screen Tutorial instruction text here
                // (Tutorial_1.Instance.OnMove()) — not a checklist task, nothing to repoint it
                // to yet since that on-screen system hasn't been rebuilt.
            }
            else
            {
                playerAnim.setIdel();
            }
        }

        // Flying locomotion
        if (locomotionState == LocomotionState.Flying)
        {
            float inputMagnitude = moveInput.magnitude;

            if (inputMagnitude > 0.1f || flyAscendHeld ||
                (Keyboard.current != null &&
                (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed)))
            {
                playerAnim.setWalking();
            }
            else
            {
                playerAnim.setIdel();
            }
        }

        // Landing transition
        if (locomotionState != LocomotionState.Flying && controller.isGrounded)
        {
            if (!wasGrounded)
            {
                playerAnim.setJumpFalse();
                playerAnim.notInAir();
                CompleteMovementTask(TaskLandGround);
            }
        }

        wasGrounded = controller.isGrounded;
    }
    private bool wasGrounded = true;

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
                velocity.y = -2f;

            // Ordinary jumps only ever set velocity.y (see Jump() below), so x/z here are
            // normally already zero. But ApplyExternalLaunch() (bounce plants, launch pads, etc.)
            // sets the full 3D velocity, including a sideways "outward" kick - without clearing
            // x/z on landing, that leftover launch velocity never goes away and keeps nudging the
            // player (and therefore the camera, which follows the player) in that direction every
            // frame forever, masked while you're holding a move key but obvious the moment you
            // release input. WalkHorizontal() already handles all normal ground movement on its
            // own, so velocity's x/z has no further job once grounded.
            velocity.x = 0f;
            velocity.z = 0f;
        }
    }

    private void Jump()
    {
        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        playerAnim.jump();
        CompleteMovementTask(TaskJumpSpace);
    }

    // ──────────────────────────────────────────────
    //  Flying locomotion
    // ──────────────────────────────────────────────
    private void EnterFlyMode()
    {
        locomotionState = LocomotionState.Flying;
        velocity = Vector3.zero;
        // NOTE: this used to advance the old on-screen Tutorial instruction text here
        // (Tutorial_1.Instance.FlyOnTable()) — not a checklist task, nothing to repoint it
        // to yet since that on-screen system hasn't been rebuilt.
        ThirdPersonCameraController.setCameraZoomLimitOnFly(true);
        flyGroundGraceTimer = flyGroundGracePeriod;
        Debug.Log("[PlayerController] Fly mode ON");
        playerAnim.fly();
        CompleteMovementTask(TaskFlyDoubleSpace);
    }

    private void UpdateFlyingLocomotion()
    {
        if (cameraTransform == null) return;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 horizontalMove =
            (forward * moveInput.y + right * moveInput.x) * CurrentSpeed;

        float verticalMove = 0f;

        if (flyGroundGraceTimer > 0f)
        {
            flyGroundGraceTimer -= Time.deltaTime;
            verticalMove += flyLiftSpeed;
        }
        else
        {
            bool ctrlHeld = Keyboard.current != null &&
                (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);

            // Tracks only what the player explicitly asked for (Space/Ctrl + idle drift), kept
            // separate from camera-pitch steering below. The exit check uses this instead of the
            // combined verticalMove so that looking slightly upward while approaching a landing spot
            // can't cancel out a held Ctrl and silently prevent the player from ever landing.
            float intentionalVertical = 0f;

            if (flyAscendHeld)
                intentionalVertical += flyVerticalSpeed * (IsSprinting ? sprintMultiplier : 1f);

            if (ctrlHeld)
                intentionalVertical -= flyVerticalSpeed * (IsSprinting ? sprintMultiplier : 1f);

            verticalMove += intentionalVertical;

            if (flyPitchInfluence > 0f && moveInput.sqrMagnitude > 0.01f)
            {
                float pitchVertical = cameraTransform.forward.y * flyPitchInfluence * CurrentSpeed;
                verticalMove += pitchVertical;

                // Tilt tasks: only count this as "tilt to fly up/down" when Space/Ctrl aren't already
                // driving the vertical move — i.e. the mouse pitch alone is what's moving the player,
                // not the explicit ascend/descend keys.
                if (!flyAscendHeld && !ctrlHeld)
                {
                    if (pitchVertical > 0.1f) CompleteMovementTask(TaskFlyUpTilt);
                    else if (pitchVertical < -0.1f) CompleteMovementTask(TaskFlyDownTilt);
                }
            }

            if (controller.isGrounded && intentionalVertical <= 0f)
            {
                ExitFlyMode();
                return;
            }
        }

        controller.Move((horizontalMove + Vector3.up * verticalMove) * Time.deltaTime);

        if (shouldFaceMoveDirection && horizontalMove.sqrMagnitude > 0.001f)
        {
            float verticalIntent = cameraTransform.forward.y * flyPitchInfluence;
            Vector3 flightDirection = horizontalMove.normalized + Vector3.up * verticalIntent;

            if (flightDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(flightDirection.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
            }
        }
    }

    private void ExitFlyMode()
    {
        locomotionState = LocomotionState.Grounded;
        velocity = Vector3.zero;
        flyAscendHeld = false;
        ThirdPersonCameraController.setCameraZoomLimitOnFly(false);

        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        if (flatForward.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);

        playerAnim.notInAir();
    }

    // ──────────────────────────────────────────────
    //  Shared helpers
    // ──────────────────────────────────────────────
    private void WalkHorizontal()
    {
        if (cameraTransform == null) return;

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
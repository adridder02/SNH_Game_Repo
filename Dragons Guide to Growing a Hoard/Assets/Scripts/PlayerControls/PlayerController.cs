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
    [Tooltip("Speed multiplier applied while sprinting AND flying. Separate from the ground sprintMultiplier so flying sprint can be tuned independently.")]
    [SerializeField] private float flySprintMultiplier = 2f;
    [Tooltip("How long (seconds) the takeoff animation is protected from being interrupted by movement/sprint animation changes after double-tapping Space. Should roughly match your takeoff clip's length.")]
    [SerializeField] private float flyTakeoffLockDuration = 0.6f;

    [Tooltip("Downward speed while auto-descending (double-tapped Space while already flying, or " +
             "FlightBlocked forcing you down). Deliberately separate from flyVerticalSpeed (the " +
             "manual Ctrl-held descent speed) so this can be tuned to feel gradual/controlled rather " +
             "than a straight drop, independent of manual descent speed.")]
    [SerializeField] private float autoDescendSpeed = 3f;

    [Header("Miasma Flight Restriction")]
    [Tooltip("Additional downward force applied while flying and ForceGradualDescend is true — " +
             "biases altitude downward over time, but (unlike autoDescending/FlightBlocked) Space/" +
             "Ctrl/WASD all still work normally alongside it, so the player CAN fight it off by " +
             "actively holding Space. A headwind, not a full override.")]
    [SerializeField] private float miasmaGradualDescendForce = 1.5f;

    /// <summary>Passive downward bias while flying — set externally (MiasmaScreenEffectController,
    /// for the miasma's middle stage in whatever zone the player's currently in). Space/Ctrl/WASD
    /// all still work normally alongside this; it has no effect while autoDescending/FlightBlocked
    /// already have full control (those take priority — see UpdateFlyingLocomotion).</summary>
    public bool ForceGradualDescend { get; set; }

    /// <summary>Blocks flight entirely while true — set externally (MiasmaScreenEffectController,
    /// for the miasma's worst stage). Can't enter fly mode (OnSpacePressed's double-tap silently
    /// does nothing while this is true), and if already flying when this turns on, immediately
    /// forced into the same clean auto-descend-to-landing double-tap-while-flying already uses.</summary>
    public bool FlightBlocked
    {
        get => flightBlocked;
        set
        {
            bool wasBlocked = flightBlocked;
            flightBlocked = value;
            if (flightBlocked && !wasBlocked && locomotionState == LocomotionState.Flying)
                autoDescending = true; // force them down the instant this turns on, not next input
        }
    }
    private bool flightBlocked = false;

    [Tooltip("Forward speed automatically added while auto-descending, so it reads as a glide down " +
             "rather than dropping straight down with no forward motion. Added on TOP of whatever " +
             "WASD gives — the player can still steer left/right during the glide, this just " +
             "guarantees baseline forward motion even with no input held.")]
    [SerializeField] private float autoDescendForwardSpeed = 3f;

    [Tooltip("How strongly camera pitch steers vertical movement while flying (0 = off).")]
    [SerializeField] private float flyPitchInfluence = 1f;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

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

    [Tooltip("How long (seconds) the dragon must be continuously airborne WITHOUT having jumped " +
             "before the fall animation kicks in. CharacterController.isGrounded can flicker false " +
             "for a single frame during completely normal walking (stairs, bumpy terrain, the tail " +
             "end of landing) — without this debounce, every one of those blips triggered the fall " +
             "animation, which read as firing constantly during ordinary movement.")]
    [SerializeField] private float fallAnimationDelay = 0.15f;
    private float ungroundedTimer = 0f;

    [Tooltip("Mirror of fallAnimationDelay for the OPPOSITE direction. controller.isGrounded can " +
             "also flicker TRUE for a single frame while genuinely still airborne (grazing a bump, a " +
             "sloped collider edge, foliage, a ledge corner) — without this debounce, that one frame " +
             "instantly fired the Landing transition (unsetting IsFalling, snapping the animator back " +
             "to idle/walk), then the very next frame re-entered the ungrounded branch and needed a " +
             "fresh fallAnimationDelay before the fall animation played again. On bumpy/cluttered " +
             "terrain during an actual fall this repeats rapidly - a Fall/Idle pose ping-pong that " +
             "reads as violent camera shaking, since the camera follows a rig transform whose position " +
             "differs a lot between those two poses. Keep this short (landings should still feel " +
             "instant) - it only needs to be longer than a single-frame flicker.")]
    [SerializeField] private float landingConfirmDelay = 0.05f;
    private float groundedTimer = 0f;
    private bool wasConfirmedGrounded = true;

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
    private InputAction moveAction;  // "Move" action (WASD) - subscribed in code so it doesn't
                                      // depend on a PlayerInput component's Inspector-wired Unity
                                      // Event (which can silently fail to carry into a build while
                                      // everything code-subscribed, like Fly, keeps working).
    private InputAction flyAction;   // Existing "Fly" action (Space)
    private InputAction inventoryAction;
    private CharacterController controller;

    // Captured once in Start() — where the player actually started this play session (NOT reset by a
    // scene reload, since that just runs Start() again wherever the scene puts the player). Used only
    // by ResetToSpawnPosition() below, for the Exit Menu's "Unstuck" button.
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private Vector2 moveInput;
    private Vector3 velocity;   // gravity / jump velocity (unused during Flying)
    private LocomotionState locomotionState = LocomotionState.Grounded;
    // Double-tap tracking
    private float lastSpacePressTime = -999f;
    // Flying vertical intent
    private bool flyAscendHeld;       // Space held while flying
    private float flyGroundGraceTimer; // countdown after entering fly, ignores isGrounded
    private float flyTakeoffLockTimer; // countdown after entering fly, blocks WASD/sprint animation
                                        // changes so the takeoff clip always plays start-to-finish
                                        // uninterrupted instead of racing with UpdateAnimator()
                                        // switching to Walk/Run the instant Flying starts.
    private bool autoDescending;       // double-tapped Space while already flying — see OnSpacePressed
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

    /// <summary>Current flying speed, boosted by flySprintMultiplier while sprinting - kept
    /// separate from CurrentSpeed/sprintMultiplier so flying sprint can be tuned without
    /// affecting ground sprint speed.</summary>
    private float CurrentFlySpeed => speed * (IsSprinting ? flySprintMultiplier : 1f);

    // ──────────────────────────────────────────────
    //  Unity lifecycle
    // ──────────────────────────────────────────────
    private void Awake()
    {
        if (inputActions != null)
        {
            gameplayMap = inputActions.FindActionMap("GamePlay", true);
            moveAction = gameplayMap?.FindAction("Move", true);
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

        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    private void OnEnable()
    {
        gameplayMap?.Enable();

        if (moveAction != null)
        {
            moveAction.Enable();
            moveAction.performed += OnMove;
            moveAction.canceled += OnMove;
        }

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
        if (moveAction != null)
        {
            moveAction.performed -= OnMove;
            moveAction.canceled -= OnMove;
        }

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

    /// <summary>Teleports the player back to wherever they started THIS play session (captured once in
    /// Start() — not reset by a scene reload/Restart, since that runs Start() again at whatever position
    /// the reloaded scene places the player). Used by the Exit Menu's "Unstuck" button — only resets
    /// position/rotation/velocity, nothing else (inventory, planted pots, mission progress, etc. are all
    /// untouched, unlike an actual game restart).</summary>
    public void ResetToSpawnPosition()
    {
        // CharacterController fights a direct transform.position set while enabled (it recomputes
        // movement from its own internal state next Move() call and can shove the player right back
        // out) — the standard workaround is to disable it for the actual teleport, then re-enable.
        if (controller != null) controller.enabled = false;

        transform.position = spawnPosition;
        transform.rotation = spawnRotation;

        velocity = Vector3.zero;
        locomotionState = LocomotionState.Grounded;
        flyAscendHeld = false;

        if (controller != null) controller.enabled = true;

        // Clears any stuck jump/fall animation state left over from wherever the player was before.
        playerAnim?.setJumpFalse();
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

        // A menu (Inventory/Journal/Pot Menu/etc.) deliberately leaves movement enabled while it's
        // open, so Space still jumps by default even with one up. But when the tutorial itself is
        // currently using Space for something (dismissing a Portable prompt like the pot menu's health
        // bar callout, or a Sidebar page's Next/Complete button), that same press shouldn't ALSO make
        // the dragon jump underneath it.
        if (TutorialSequenceController.Instance != null && TutorialSequenceController.Instance.IsConsumingSpacebar)
            return;

        switch (locomotionState)
        {
            case LocomotionState.Grounded:
                Jump();
                locomotionState = LocomotionState.Jumping;
                lastSpacePressTime = Time.time;
                break;

            case LocomotionState.Jumping:
                if (Time.time - lastSpacePressTime <= doubleTapWindow)
                {
                    if (flightBlocked)
                    {
                        // Miasma's worst stage in this room — can't take off here right now.
                        // Silently ignored rather than logged/messaged; the screen overlay (image 3,
                        // MiasmaScreenEffectController) is what communicates why to the player.
                        lastSpacePressTime = Time.time;
                        break;
                    }

                    EnterFlyMode();
                    // Refresh the timestamp the instant flying actually starts — without this, it
                    // stays stale from the FIRST of the two launch taps, and a normal eager third
                    // tap (tap-tap to launch, tap again quickly to ascend) lands within
                    // doubleTapWindow of that stale time and gets misread as a double-tap-while-
                    // flying, triggering autoDescending immediately on takeoff and stomping the
                    // takeoff animation before it can play.
                    lastSpacePressTime = Time.time;
                }
                else
                    lastSpacePressTime = Time.time;
                break;

            case LocomotionState.Flying:
                if (autoDescending)
                {
                    if (flightBlocked)
                    {
                        // Miasma's worst stage forced this descent (FlightBlocked's setter) — unlike
                        // the voluntary double-tap case below, this can't be cancelled by pressing
                        // Space. Without this check, a player who habitually holds/taps Space to stay
                        // airborne would cancel the forced landing on their very next press, undoing
                        // the restriction almost immediately.
                        lastSpacePressTime = Time.time;
                        break;
                    }

                    // Pressing Space again mid-descent cancels it and hands control back to the
                    // player, rather than stacking/ignoring the press — in case they change their
                    // mind about where to land.
                    autoDescending = false;
                    flyAscendHeld = true;
                }
                else if (Time.time - lastSpacePressTime <= doubleTapWindow)
                {
                    // Double-tapped Space again while already flying — begin a controlled, gradual
                    // descent (see autoDescendSpeed / UpdateFlyingLocomotion) instead of the usual
                    // single-press ascend, and play the dragon_fall animation for it (fall() also
                    // drops IsFlying itself, so Fly Idle doesn't keep competing with it).
                    autoDescending = true;
                    flyAscendHeld = false;
                    playerAnim.fall();
                }
                else
                {
                    flyAscendHeld = true;
                }
                lastSpacePressTime = Time.time;
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

        // Continuous grounded/ungrounded timers so a single-frame flicker of controller.isGrounded
        // (documented below — happens constantly on bumpy terrain, slopes, and even mid-fall when
        // grazing geometry) can't snap the animator back and forth on its own. ungroundedTimer/
        // fallAnimationDelay already debounced entering the fall animation this way; groundedTimer/
        // landingConfirmDelay does the same for leaving it — see that field's tooltip for why a
        // single flickered "grounded" frame mid-fall used to cause a rapid Fall/Idle pose ping-pong
        // that read as the camera itself shaking.
        if (controller.isGrounded)
        {
            groundedTimer += Time.deltaTime;
            ungroundedTimer = 0f;
        }
        else
        {
            ungroundedTimer += Time.deltaTime;
            groundedTimer = 0f;
        }
        bool confirmedGrounded = groundedTimer >= landingConfirmDelay;

        // Grounded locomotion
        if (locomotionState == LocomotionState.Grounded)
        {
            if (!controller.isGrounded)
            {
                // Walked off a ledge without jumping — nothing else ever transitions
                // locomotionState to Jumping unless Space was actually pressed, so without this
                // check walk/run/idle kept playing the whole way down. The landing transition below
                // already handles this correctly with no further changes — it's keyed on
                // confirmedGrounded, not on how the player became airborne.
                //
                // Debounced (fallAnimationDelay) rather than firing on the very first ungrounded
                // frame — CharacterController.isGrounded flickers false for single frames during
                // completely normal walking (stairs, bumpy terrain), which was triggering this
                // constantly. Below the threshold, just leave whatever animation was already
                // playing alone rather than switching to anything.
                if (ungroundedTimer >= fallAnimationDelay)
                {
                    // dragon_fall plays on its own now (playerAnimation.fall() drives "IsFalling"
                    // directly) — no more setIdel() here to fake it via Fly Idle's Speed float.
                    playerAnim.fall();
                }
            }
            else
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
        }

        // Flying locomotion
        if (locomotionState == LocomotionState.Flying)
        {
            // While the takeoff lock is active, don't touch the Speed parameter at all - let
            // whatever transition/state your Animator Controller set up for takeoff play out on
            // its own, instead of us immediately forcing Walk/Run and racing with it.
            if (flyTakeoffLockTimer <= 0f)
            {
                // Back to using Fly Idle properly (the TEMP always-walk/run workaround is reverted
                // now that Fly Idle is reliable again) - walk/run only while there's actual flight
                // input, idle otherwise, matching the un-frozen ground-locomotion pattern below.
                // Only touches the Speed float, so this stays harmless during autoDescending too -
                // fall() already dropped IsFlying for that case, so this can't re-trigger Fly Idle.
                bool hasFlightInput = moveInput.sqrMagnitude > 0.01f || flyAscendHeld ||
                    (Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed));

                if (hasFlightInput)
                {
                    if (IsSprinting)
                        playerAnim.setRunning();
                    else
                        playerAnim.setWalking();
                }
                else
                {
                    playerAnim.setIdel();
                }
            }
        }

        // Landing transition — gated on confirmedGrounded (landingConfirmDelay), not a raw single-
        // frame isGrounded flip, so a chatter blip mid-fall can't fire this prematurely (see
        // landingConfirmDelay's tooltip).
        if (locomotionState != LocomotionState.Flying && confirmedGrounded && !wasConfirmedGrounded)
        {
            playerAnim.setJumpFalse();
            playerAnim.notInAir();
            CompleteMovementTask(TaskLandGround);
        }

        wasConfirmedGrounded = confirmedGrounded;
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
        autoDescending = false; // defensive — shouldn't ever be true entering fresh, but don't inherit stale state
        // NOTE: this used to advance the old on-screen Tutorial instruction text here
        // (Tutorial_1.Instance.FlyOnTable()) — not a checklist task, nothing to repoint it
        // to yet since that on-screen system hasn't been rebuilt.
        ThirdPersonCameraController.setCameraZoomLimitOnFly(true);
        flyGroundGraceTimer = flyGroundGracePeriod;
        flyTakeoffLockTimer = flyTakeoffLockDuration;
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
            (forward * moveInput.y + right * moveInput.x) * CurrentFlySpeed;

        // Guarantees forward motion during an auto-descend even with no WASD held, so it reads as
        // a glide down rather than dropping straight down. Added on top of whatever WASD already
        // gave above — the player can still steer, this just sets a baseline.
        if (autoDescending)
            horizontalMove += forward * autoDescendForwardSpeed;

        // Ignore WASD entirely while the takeoff animation is protected - the vertical lift
        // below (flyGroundGraceTimer) still runs as normal so you still rise up off the ground,
        // but nothing can turn/strafe you or trigger a Walk/Run animation switch until the
        // takeoff clip has had its full, uninterrupted window to play.
        if (flyTakeoffLockTimer > 0f)
        {
            flyTakeoffLockTimer -= Time.deltaTime;
            horizontalMove = Vector3.zero;
        }

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

            if (autoDescending)
            {
                // Double-tapped Space while flying — steady, gradual descent rather than a straight
                // drop, and deliberately NOT steered by camera pitch (see the skipped block below),
                // so it stays controlled regardless of where the player happens to be looking.
                // Horizontal WASD movement still works normally during this — it's just gliding
                // down, not frozen in place.
                intentionalVertical -= autoDescendSpeed;
            }
            else
            {
                if (flyAscendHeld)
                    intentionalVertical += flyVerticalSpeed * (IsSprinting ? flySprintMultiplier : 1f);

                if (ctrlHeld)
                    intentionalVertical -= flyVerticalSpeed * (IsSprinting ? flySprintMultiplier : 1f);

                // Miasma's middle stage in whatever zone the player's currently in — a headwind, not
                // a full override, so it stacks with whatever the player's already doing above
                // rather than replacing it. FlightBlocked (worst stage) doesn't need its own branch
                // here — it just sets autoDescending=true the instant it turns on (see the
                // FlightBlocked property), so it's already covered by the branch above.
                if (ForceGradualDescend)
                    intentionalVertical -= miasmaGradualDescendForce;
            }

            verticalMove += intentionalVertical;

            if (!autoDescending && flyPitchInfluence > 0f && moveInput.sqrMagnitude > 0.01f)
            {
                float pitchVertical = cameraTransform.forward.y * flyPitchInfluence * CurrentFlySpeed;
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
                // Covers both the normal manual landing (fly yourself down) and auto-descending
                // touching ground — autoDescending is reset inside ExitFlyMode() itself, so this
                // needs no special-casing here at all; intentionalVertical is already <= 0 either way.
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
        else if (shouldFaceMoveDirection)
        {
            // Idling in place while flying (no WASD input) - nothing above ever resets pitch/roll,
            // so without this the dragon stays frozen at whatever tilt it last had from looking
            // up/down. Keep the current yaw (heading) but smoothly level pitch/roll back to zero.
            Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);
            if (flatForward.sqrMagnitude > 0.001f)
            {
                Quaternion levelRotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, levelRotation, 6f * Time.deltaTime);
            }
        }
    }

    private void ExitFlyMode()
    {
        locomotionState = LocomotionState.Grounded;
        velocity = Vector3.zero;
        flyAscendHeld = false;
        autoDescending = false;
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
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 9f;
    [SerializeField] private float rotationSmoothTime = 0.12f;
    [SerializeField] private float speedChangeRate = 10f;

    [Header("Jump / Fly Settings")]
    [SerializeField] private float jumpImpulse = 6f;        // velocity added on each tap
    [SerializeField] private float flyHoldForce = 18f;      // upward force while holding Space in air
    [SerializeField] private float maxFlyVelocity = 10f;    // terminal upward velocity while flying
    [SerializeField] private float gravity = -20f;          // gravity (negative)
    [SerializeField] private float fallMultiplier = 1.5f;   // snappier fall when not flapping

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float groundedOffset = -0.14f;
    [SerializeField] private float groundedRadius = 0.28f;

    [Header("Camera")]
    [SerializeField] private GameObject cameraTarget;
    [SerializeField] private float topClamp = 70f;
    [SerializeField] private float bottomClamp = -30f;

    // Components
    private CharacterController _controller;
    private PlayerInput _playerInput;
    private Animator _animator;
    private bool _hasAnimator;

    // Input state
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _jumpHeld;             // is Space currently held?
    private bool _jumpTappedThisFrame;  // fresh press detected this frame

    // Physics state
    private float _verticalVelocity;
    private float _speed;
    private float _targetRotation;
    private float _rotationVelocity;
    private bool _grounded;

    // Camera state
    private float _cameraTargetYaw;
    private float _cameraTargetPitch;
    private Camera _mainCamera;

    // Animator IDs
    private int _animIDSpeed;
    private int _animIDGrounded;
    private int _animIDJump;
    private int _animIDFlying;
    private int _animIDFreeFall;
    private int _animIDMotionSpeed;

    private void Awake()
    {
        _mainCamera  = Camera.main;
        _controller  = GetComponent<CharacterController>();
        _playerInput = GetComponent<PlayerInput>();
        _hasAnimator = TryGetComponent(out _animator);

        AssignAnimationIDs();
        _cameraTargetYaw = transform.eulerAngles.y;
    }

    private void AssignAnimationIDs()
    {
        _animIDSpeed       = Animator.StringToHash("Speed");
        _animIDGrounded    = Animator.StringToHash("Grounded");
        _animIDJump        = Animator.StringToHash("Jump");
        _animIDFlying      = Animator.StringToHash("Flying");
        _animIDFreeFall    = Animator.StringToHash("FreeFall");
        _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
    }

    // ── Input System callbacks ────────────────────────────────────────────────

    public void OnMove(InputValue value) => _moveInput = value.Get<Vector2>();
    public void OnLook(InputValue value) => _lookInput = value.Get<Vector2>();

    public void OnJump(InputValue value)
    {
        bool pressed = value.isPressed;

        // Detect exact frame the button transitions released → pressed
        if (pressed && !_jumpHeld)
            _jumpTappedThisFrame = true;

        _jumpHeld = pressed;
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Update()
    {
        GroundedCheck();
        HandleFlightPhysics();
        HandleMovement();

        // Always clear the per-frame tap flag at the END of Update
        _jumpTappedThisFrame = false;
    }

    private void LateUpdate() => HandleCameraRotation();

    // ── Ground detection ──────────────────────────────────────────────────────

    private void GroundedCheck()
    {
        Vector3 spherePos = new Vector3(
            transform.position.x,
            transform.position.y - groundedOffset,
            transform.position.z);

        _grounded = Physics.CheckSphere(
            spherePos, groundedRadius, groundLayers,
            QueryTriggerInteraction.Ignore);

        if (_hasAnimator)
            _animator.SetBool(_animIDGrounded, _grounded);
    }

    // ── Flight / jump physics ─────────────────────────────────────────────────
    //
    //  State         | Tap Space           | Hold Space
    // ───────────────|─────────────────────|──────────────────────────────────
    //  On ground     | Normal jump impulse  | Same as tap (jump off ground)
    //  In the air    | Flap — velocity set  | Continuous upward force (hover /
    //                |   to jumpImpulse     |   slow climb), capped at maxFly
    //
    //  Gravity is always active. fallMultiplier makes descent snappier when
    //  the player isn't actively flying, giving that Flappy Bird "weight".

    private void HandleFlightPhysics()
    {
        if (_grounded)
        {
            // Glue to slopes / flat ground
            if (_verticalVelocity < 0f)
                _verticalVelocity = -2f;

            if (_jumpTappedThisFrame)
            {
                _verticalVelocity = jumpImpulse;

                if (_hasAnimator)
                    _animator.SetBool(_animIDJump, true);
            }
        }
        else
        {
            // ── Airborne ─────────────────────────────────────────────────────

            if (_jumpTappedThisFrame)
            {
                // Each tap gives a consistent upward kick (Flappy Bird flap)
                _verticalVelocity = jumpImpulse;
            }
            else if (_jumpHeld)
            {
                // Holding gives a softer, sustained lift
                _verticalVelocity += flyHoldForce * Time.deltaTime;
                _verticalVelocity  = Mathf.Min(_verticalVelocity, maxFlyVelocity);
            }

            if (_hasAnimator)
            {
                _animator.SetBool(_animIDJump,     false);
                _animator.SetBool(_animIDFlying,   _jumpHeld);
                _animator.SetBool(_animIDFreeFall, _verticalVelocity < -3f && !_jumpHeld);
            }
        }

        // Gravity — apply extra multiplier when falling freely (not flying)
        bool fallingFreely = !_grounded && _verticalVelocity < 0f && !_jumpHeld;
        float gravityScale = fallingFreely ? fallMultiplier : 1f;

        _verticalVelocity += gravity * gravityScale * Time.deltaTime;
    }

    // ── Horizontal movement ───────────────────────────────────────────────────

    private void HandleMovement()
    {
        float targetSpeed = (_moveInput != Vector2.zero) ? walkSpeed : 0f;

        float currentHSpeed = new Vector3(
            _controller.velocity.x, 0f, _controller.velocity.z).magnitude;

        float inputMagnitude = _moveInput.magnitude;

        if (Mathf.Abs(currentHSpeed - targetSpeed) > 0.1f)
        {
            _speed = Mathf.Lerp(currentHSpeed, targetSpeed * inputMagnitude,
                                Time.deltaTime * speedChangeRate);
            _speed = Mathf.Round(_speed * 1000f) / 1000f;
        }
        else
        {
            _speed = targetSpeed;
        }

        Vector3 inputDirection = new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;

        if (_moveInput != Vector2.zero)
        {
            _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z)
                * Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;

            float rotation = Mathf.SmoothDampAngle(
                transform.eulerAngles.y, _targetRotation,
                ref _rotationVelocity, rotationSmoothTime);

            transform.rotation = Quaternion.Euler(0f, rotation, 0f);
        }

        Vector3 targetDir = Quaternion.Euler(0f, _targetRotation, 0f) * Vector3.forward;

        _controller.Move(
            targetDir.normalized * (_speed * Time.deltaTime) +
            new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime);

        if (_hasAnimator)
        {
            _animator.SetFloat(_animIDSpeed,       inputMagnitude != 0 ? _speed : 0f);
            _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
        }
    }

    // ── Camera rotation ───────────────────────────────────────────────────────

    private void HandleCameraRotation()
    {
        if (_lookInput.sqrMagnitude >= 0.01f)
        {
            float dtMul = IsCurrentDeviceMouse() ? 1f : Time.deltaTime;
            _cameraTargetYaw   += _lookInput.x * dtMul;
            _cameraTargetPitch -= _lookInput.y * dtMul;
        }

        _cameraTargetYaw   = ClampAngle(_cameraTargetYaw,   float.MinValue, float.MaxValue);
        _cameraTargetPitch = ClampAngle(_cameraTargetPitch, bottomClamp, topClamp);

        cameraTarget.transform.rotation = Quaternion.Euler(
            _cameraTargetPitch, _cameraTargetYaw, 0f);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool IsCurrentDeviceMouse() =>
        _playerInput.currentControlScheme == "KeyboardMouse";

    private static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360f) angle += 360f;
        if (angle >  360f) angle -= 360f;
        return Mathf.Clamp(angle, min, max);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _grounded
            ? new Color(0f, 1f, 0f, 0.35f)
            : new Color(1f, 0f, 0f, 0.35f);

        Gizmos.DrawSphere(
            new Vector3(transform.position.x,
                        transform.position.y - groundedOffset,
                        transform.position.z),
            groundedRadius);
    }
}

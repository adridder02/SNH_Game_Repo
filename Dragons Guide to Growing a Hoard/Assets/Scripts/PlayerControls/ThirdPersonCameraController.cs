using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

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

    [Header("Vertical Look Limits")]
    [Tooltip("Pitch range (degrees) while grounded.")]
    [SerializeField] private float groundedMinPitch = -40f;
    [SerializeField] private float groundedMaxPitch = 40f;
    [Tooltip("Pitch range (degrees) while flying - wider by default so you can look further up/down in the air.")]
    [SerializeField] private float flyingMinPitch = -70f;
    [SerializeField] private float flyingMaxPitch = 70f;

    // Current effective range - starts at the grounded values, switched by setCameraZoomLimitOnFly()
    // below. No longer static (see chat history: a [SerializeField] static field doesn't serialize
    // per-instance the way it looks like it should, and could leak a runtime value back into Edit
    // mode across Play sessions with Domain Reload disabled).
    private float minPitchAngle;
    private float maxPitchAngle;

    [Header("Framing")]
    [Tooltip("Vertical offset (world units) added to where the camera orbits/aims, relative to the dragon's own pivot. Raising this aims the camera above the dragon's actual body, which pushes the dragon lower in the frame instead of dead-center.")]
    [SerializeField] private float groundedTargetOffsetY = 0.5f;
    [Tooltip("Same idea as above, used while flying - typically higher than the grounded value so the camera sits a bit further up relative to the dragon in the air.")]
    [SerializeField] private float flyingTargetOffsetY = 1.5f;
    [Tooltip("How quickly the framing offset eases between the grounded and flying values on takeoff/landing (seconds - lower = snappier).")]
    [SerializeField] private float targetOffsetLerpSpeed = 4f;

    // Tracks flying vs grounded purely to pick which TargetOffset value to ease toward above -
    // set from setCameraZoomLimitOnFly() below, the same signal PlayerController already sends
    // on every takeoff/landing.
    private bool isFlying = false;

    [Header("Flight Recentering")]
    [Tooltip("While flying, once movement/look input has stopped for a bit, smoothly eases the camera's pitch back to match the dragon's own current pitch - so it settles level with wherever the dragon is actually pointing, including straight up or down, instead of staying wherever you last looked.")]
    [SerializeField] private bool recenterVerticalWhileFlying = true;
    [Tooltip("Same idea as vertical recentering above, but for YAW (horizontal heading) - eases the camera back to face the same direction the dragon is actually flying, once input has stopped. Same trigger timing (recenterDelay) as vertical; uses its own smoothing state so the two axes don't interfere with each other.")]
    [SerializeField] private bool recenterHorizontalWhileFlying = true;
    [Tooltip("Seconds of no flight input (mouse look OR movement/ascend/descend keys) before recentering kicks in.")]
    [SerializeField] private float recenterDelay = 0.8f;
    [Tooltip("How quickly the camera eases back to centered once recentering starts (seconds - lower = snappier). Shared by both vertical and horizontal recentering.")]
    [SerializeField] private float recenterSmoothTime = 0.5f;
    [Tooltip("Flip if recentering pushes the vertical angle the wrong way for your rig - Cinemachine's sign convention for VerticalAxis.Value can go either way depending on setup.")]
    [SerializeField] private bool invertVerticalRecenterSign = false;
    [Tooltip("Same as Invert Vertical Recenter Sign above, but for HorizontalAxis.Value - separate flag since the two axes can have independent sign conventions depending on rig setup.")]
    [SerializeField] private bool invertHorizontalRecenterSign = false;
    [Tooltip("Added to the computed target pitch before clamping - use this to bias where recentering settles when the dragon is level, e.g. a slightly downward default framing rather than dead-level. Positive/negative direction depends on your rig's sign convention (see Invert above).")]
    [SerializeField] private float verticalRecenterCenterOffset = 0f;
    [Tooltip("Which object's rotation to recenter toward - should be whatever GameObject PlayerController actually rotates during flight (confirmed via chat: same object as cam.Follow works). Leave empty to fall back to cam.Follow directly.")]
    [SerializeField] private Transform recenterReferenceTransform;

    private float timeSinceFlightInput = 0f;
    private float verticalRecenterVelocity = 0f;
    private float horizontalRecenterVelocity = 0f;

    [Header("Object Transparency (for all other layers)")]
    [SerializeField] private LayerMask transparentMask = ~0;
    [SerializeField] private float fadeSpeed = 5f;
    [SerializeField] [Range(0f, 1f)] private float targetAlpha = 0.2f;
    
    [Header("Camera Pull Settings")]
    [SerializeField] private float collisionPullInSpeed = 20f;
    [SerializeField] private float collisionPullOutSpeed = 15f;
    [SerializeField] private float collisionBuffer = 0.3f;
    
    [Header("Collision Box Settings")]
    [SerializeField] private bool showDebugBox = true;

    [Header("Dragon Hide-on-Collision")]
    [Tooltip("Renderers to hide while the camera is blocked by a solid object (floor, wall, etc.), e.g. body, wings, horns.")]
    [SerializeField] private Renderer[] dragonRenderers;
    [Tooltip("Also hide the dragon whenever the camera gets closer than this to the dragon itself (regardless of whether it's colliding with anything) - avoids the model filling/clipping through the view in tight spaces.")]
    [SerializeField] private float dragonHideDistance = 1.2f;
    [Tooltip("Extra distance the camera must move back out past dragonHideDistance before the dragon reappears. Prevents rapid show/hide flicker while hovering right at the threshold.")]
    [SerializeField] private float dragonShowDistanceBuffer = 0.3f;

    [Header("Performance")]
    [SerializeField] private bool enableDebugLogs = false;

    // Transparency system variables
    private Dictionary<Renderer, float> currentAlphas = new Dictionary<Renderer, float>();
    private HashSet<Renderer> activeFades = new HashSet<Renderer>();
    private Dictionary<Renderer, MaterialPropertyBlock> propertyBlocks = new Dictionary<Renderer, MaterialPropertyBlock>();
    private Dictionary<Renderer, Color> originalColors = new Dictionary<Renderer, Color>();
    private Dictionary<Renderer, Material> originalMaterials = new Dictionary<Renderer, Material>();
    private Dictionary<Renderer, bool> wasTransparent = new Dictionary<Renderer, bool>();

    // Camera pull system variables
    private float collisionZoom;
    private float targetZoom;
    private float currentZoom;
    private bool isCollidingWithPullObject = false;
    private float collisionDistance = 0f;
    private GameObject currentCollidingObject = null;

    // Dragon hide-on-close state
    private bool dragonHidden = false;

    // Original variables
    private PlayerControls controls;
    private CinemachineCamera cam;
    private CinemachineOrbitalFollow orbital;
    private CinemachineInputAxisController inputAxis;
    private Vector2 scrollDelta;

    // The actual rendered/output camera (Camera.main by default). Decollider (and anything
    // else that corrects the final blended camera pose) writes its correction to THIS
    // transform, not to `cam`'s (the virtual CinemachineCamera's) transform - so anything
    // that needs the true on-screen camera position (like the dragon-hide distance check)
    // has to read from here, not from `transform`/`cam.transform`.
    [Header("Output Camera")]
    [Tooltip("The actual rendering Camera (usually Camera.main). Used for distance checks that need the real on-screen camera position, since Decollider's wall-pushback correction is applied here but not to this virtual camera's own transform. Leave empty to auto-find Camera.main at Start.")]
    [SerializeField] private Camera outputCamera;

    public static bool CameraLocked = false;

    /// <summary>When true AND CameraLocked is also true, holding the right mouse button
    /// temporarily re-enables rotation anyway — released, it drops back to fully locked. Set by
    /// GameInputModeManager.SetPlacementMode() (and cleared by its other Set*Mode methods) so
    /// Placement mode gets "camera stays put by default, hold right-click to reposition it"
    /// instead of either fully free (swivels while just trying to aim at a cell) or fully frozen
    /// (no way to look around without backing out of the mode entirely).</summary>
    public static bool AllowRotationWhileLockedIfRightClickHeld = false;

    /// <summary>When true, scroll wheel input should NOT zoom the camera — something else (e.g.
    /// PotInteraction's Interact/Water Plant prompt selection) is consuming it instead this frame.
    /// Deliberately separate from CameraLocked, which also locks mouse-look rotation — this only
    /// suppresses zoom, looking around still works normally.</summary>
    public static bool ScrollSuppressed = false;

    // See Start() / setCameraZoomLimitOnFly() below - lets that static method reach this
    // instance's (now non-static) pitch-range fields without PlayerController needing to hold
    // or pass a direct reference.
    private static ThirdPersonCameraController Instance;

    // URP Shader property IDs
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int SurfaceProperty = Shader.PropertyToID("_Surface");
    private static readonly int BlendProperty = Shader.PropertyToID("_Blend");
    private static readonly int SrcBlendProperty = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendProperty = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWriteProperty = Shader.PropertyToID("_ZWrite");

    void Start()
    {
        // So the static setCameraZoomLimitOnFly() below (called from PlayerController without
        // holding a direct reference) can still reach this instance's pitch-range fields now that
        // they're no longer static themselves.
        Instance = this;

        controls = new PlayerControls();
        controls.Enable();
        controls.Camera.MouseZoom.performed += HandleMouseScroll;

        cam = GetComponent<CinemachineCamera>();
        orbital = cam.GetComponent<CinemachineOrbitalFollow>();
        inputAxis = cam.GetComponent<CinemachineInputAxisController>();

        if (outputCamera == null)
            outputCamera = Camera.main;
        if (outputCamera == null && enableDebugLogs)
            Debug.LogWarning("[ThirdPersonCameraController] outputCamera not assigned and Camera.main is null - dragon-hide distance will fall back to this virtual camera's transform, which won't reflect Decollider's wall-pushback correction.");

        // Start grounded - setCameraZoomLimitOnFly(true) switches these to the flying range
        // whenever PlayerController enters fly mode.
        minPitchAngle = groundedMinPitch;
        maxPitchAngle = groundedMaxPitch;

        targetZoom = currentZoom = collisionZoom = orbital.Radius;
        ConfigureAxes();
        
        // Setup or find existing collider
        SetupCameraCollisionBox();
    }

    private void SetupCameraCollisionBox()
    {
        // Try to get existing collider first
        Collider existingCollider = GetComponent<Collider>();
        
        if (existingCollider == null)
        {
            // Create a box collider if none exists
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(0.5f, 0.5f, 2f);
            boxCollider.center = Vector3.forward;
            
            if (enableDebugLogs)
                Debug.Log("Created new BoxCollider for camera collision detection");
        }
        else
        {
            // Use existing collider
            existingCollider.isTrigger = true;
            if (enableDebugLogs)
                Debug.Log($"Using existing {existingCollider.GetType().Name} for camera collision detection");
        }
        
        // Add rigidbody for trigger detection (must be kinematic)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void OnTriggerStay(Collider other)
    {
        // The layer filtering is handled by the Box Collider's Include/Exclude settings
        // So we don't need to check layers here - if we got this event, it's already filtered!
        
        // Calculate distance from camera to the object
        Vector3 closestPoint = GetSafeClosestPoint(other);
        float distance = Vector3.Distance(transform.position, closestPoint);
        
        // Check if this is closer than previous collisions
        if (!isCollidingWithPullObject || distance < collisionDistance)
        {
            isCollidingWithPullObject = true;
            collisionDistance = distance;
            currentCollidingObject = other.gameObject;
            
            if (enableDebugLogs)
            {
                Debug.Log($"Camera colliding with MainInteraction: {other.gameObject.name} at distance {distance}");
            }
        }
    }

    /// <summary>
    /// Collider.ClosestPoint() only supports Box/Sphere/Capsule colliders and *convex*
    /// Mesh Colliders - it throws at runtime on a non-convex MeshCollider, which is
    /// exactly what most floor/wall level geometry uses. Fall back to the collider's
    /// AABB bounds in that case; ClosestPoint on Bounds works for any collider type
    /// and is accurate enough for camera-pull purposes on flat surfaces.
    /// </summary>
    private Vector3 GetSafeClosestPoint(Collider other)
    {
        bool supportsClosestPoint = other is BoxCollider
            || other is SphereCollider
            || other is CapsuleCollider
            || (other is MeshCollider meshCollider && meshCollider.convex);

        return supportsClosestPoint
            ? other.ClosestPoint(transform.position)
            : other.bounds.ClosestPoint(transform.position);
    }

    void OnTriggerExit(Collider other)
    {
        // FIX: only clear collision state if the object exiting is the one we're
        // actually tracking. Previously this cleared state whenever ANY collider
        // exited, even if a different (still-overlapping) collider was closer and
        // still active - causing the camera to snap out and immediately snap back in.
        if (other.gameObject == currentCollidingObject)
        {
            isCollidingWithPullObject = false;
            collisionDistance = 0f;
            currentCollidingObject = null;

            if (enableDebugLogs)
            {
                Debug.Log($"Camera exited collision with: {other.gameObject.name}");
            }
        }
    }

    /// <summary>
    /// Safety net: restores whatever this component was visually changing (hidden dragon,
    /// faded renderers) the moment it stops running for ANY reason - toggled off in the
    /// Inspector, disabled by other code, scene unload, etc. Previously only OnDestroy did
    /// this cleanup, which doesn't fire on a simple disable - so a renderer hidden right
    /// before the component got disabled would stay hidden forever with nothing left to
    /// undo it. Doesn't touch `controls` (Input System) here since those are only ever
    /// Enable()'d once in Start(), not re-initialized in a matching OnEnable.
    /// </summary>
    private void OnDisable()
    {
        RestoreAllTransparentRenderers();

        if (dragonHidden)
        {
            SetDragonRenderersEnabled(true);
            dragonHidden = false;
        }
    }

    private void OnDestroy()
    {
        RestoreAllTransparentRenderers();

        if (dragonHidden)
        {
            SetDragonRenderersEnabled(true);
            dragonHidden = false;
        }
        
        if (controls != null)
        {
            controls.Camera.MouseZoom.performed -= HandleMouseScroll;
            controls.Disable();
        }
    }

    private void HandleMouseScroll(InputAction.CallbackContext context)
    {
        scrollDelta = context.ReadValue<Vector2>();
    }

    void Update()
    {
        bool rightClickOverride = CameraLocked && AllowRotationWhileLockedIfRightClickHeld &&
                                   Mouse.current != null && Mouse.current.rightButton.isPressed;

        if (CameraLocked && !rightClickOverride)
        {
            scrollDelta = Vector2.zero;
            if (inputAxis != null) inputAxis.enabled = false;
            return;
        }

        if (inputAxis != null) inputAxis.enabled = true;

        // FIX: clamp deltaTime for all smoothing math below. Any external
        // hitch (heavy synchronous work elsewhere in the scene, GC pause,
        // asset streaming, etc.) can cause Time.deltaTime to spike for a
        // single frame; multiplying that spike into Mathf.Lerp's interpolant
        // pushes values almost instantly to their target, reading as a
        // camera "snap". Capping it means the worst case is one slightly
        // slower-than-usual smoothing step instead of a visible jump.
        float dt = Mathf.Min(Time.deltaTime, 0.05f); // never treat a frame as slower than 20 fps for smoothing purposes

        // Zoom intent
        // FIX: base this on currentZoom (the player's intended distance), not on
        // orbital.Radius. orbital.Radius gets overwritten every frame with
        // collisionZoom (the collision-adjusted value), so computing the new
        // target from it created a feedback loop: after any wall-pull-in, the
        // next scroll would zoom from the shrunk distance instead of from where
        // the player actually had the camera, causing snapping/jumping.
        if (scrollDelta.y != 0f)
        {
            if (!ScrollSuppressed)
            {
                targetZoom = Mathf.Clamp(
                    currentZoom - scrollDelta.y * zoomSpeed,
                    minDistance, maxDistance);
            }
            scrollDelta = Vector2.zero; // clear regardless, so a suppressed scroll doesn't leak through and zoom once suppression lifts
        }

        currentZoom = Mathf.Lerp(currentZoom, targetZoom, dt * zoomLerpSpeed);

        // Handle both systems
        HandleCameraPullAndTransparency(dt);

        // Apply the final radius
        orbital.Radius = collisionZoom;

        // Pitch limits
        if (orbital != null)
        {
            orbital.VerticalAxis.Range = new Vector2(minPitchAngle, maxPitchAngle);
            orbital.VerticalAxis.Wrap = false;
        }
    }

    private void HandleCameraPullAndTransparency(float dt)
    {
        Transform follow = cam.Follow;
        if (follow == null)
        {
            // If this ever fires, cam.Follow is unset/lost, which means camera-pull,
            // dragon-visibility and transparency ALL silently stop running - including
            // whatever last set a renderer disabled, with nothing left to undo it.
            if (enableDebugLogs)
                Debug.LogWarning("[ThirdPersonCameraController] cam.Follow is null - camera pull/dragon-visibility/transparency are all skipped this frame.");
            return;
        }

        // Handle camera pull based on trigger collisions
        float desiredRadius = ResolveCollisionWithTrigger(currentZoom);
        
        float lerpSpeed = desiredRadius < collisionZoom
            ? collisionPullInSpeed
            : collisionPullOutSpeed;
        
        collisionZoom = Mathf.Lerp(collisionZoom, desiredRadius, dt * lerpSpeed);

        // Hide the dragon while the camera is pinned against something solid
        // (floor, wall, etc.), or while it's simply too close to the dragon itself,
        // so it doesn't clip through / fill the view in tight spaces.
        UpdateDragonVisibility(follow);

        // Ease the camera's pitch back to match the dragon's own current pitch while flying,
        // once the player stops actively steering it.
        HandleFlightRecentering(follow, dt);

        // Keep the dragon framed in the lower-center of the screen rather than dead-center,
        // with a taller offset while flying so the camera sits a bit higher relative to it in
        // the air. Runs continuously (not gated on isFlying alone) so it eases smoothly across
        // the takeoff/landing transition instead of snapping.
        {
            float desiredOffsetY = isFlying ? flyingTargetOffsetY : groundedTargetOffsetY;
            Vector3 targetOffset = orbital.TargetOffset;
            targetOffset.y = Mathf.Lerp(targetOffset.y, desiredOffsetY, dt * targetOffsetLerpSpeed);
            orbital.TargetOffset = targetOffset;
        }

        // Handle transparency for all other layers using Raycast
        HandleTransparencyForOtherLayers(dt);
    }

    /// <summary>
    /// Uses trigger collision detection to determine safe camera distance
    /// </summary>
    private float ResolveCollisionWithTrigger(float desiredRadius)
    {
        if (isCollidingWithPullObject && collisionDistance > 0)
        {
            // Calculate safe distance from player
            Transform follow = cam.Follow;
            if (follow != null)
            {
                float distanceFromCameraToPlayer = Vector3.Distance(transform.position, follow.position);
                float safeDistance = distanceFromCameraToPlayer - collisionDistance - collisionBuffer;
                return Mathf.Clamp(safeDistance, minDistance, desiredRadius);
            }
        }
        
        return desiredRadius;
    }

    /// <summary>
    /// Handles transparency for objects NOT handled by the collision box
    /// </summary>
    private void HandleTransparencyForOtherLayers(float dt)
    {
        Transform follow = cam.Follow;
        if (follow == null) return;

        Vector3 cameraPosition = cam.transform.position;
        Vector3 playerPosition = follow.position;
        Vector3 direction = (playerPosition - cameraPosition).normalized;
        float distanceToPlayer = Vector3.Distance(cameraPosition, playerPosition);
        
        // Raycast from camera to player for transparency.
        // FIX: this now respects transparentMask, which was previously declared but
        // never applied - the raycast hit every layer regardless of the mask setting.
        // Put the floor/wall layer(s) OUTSIDE this mask (and inside the collision box's
        // Include Layers instead - see ShouldObjectCauseCameraPull below) so they never
        // fade and are handled purely by the camera-pull collision system instead.
        // Ignoring triggers keeps the camera's own trigger box out of its own raycast.
        RaycastHit[] hits = Physics.RaycastAll(cameraPosition, direction, distanceToPlayer,
            transparentMask, QueryTriggerInteraction.Ignore);
        
        HashSet<Renderer> objectsToFade = new HashSet<Renderer>();
        
        foreach (RaycastHit hit in hits)
        {
            // Skip the player and their children
            if (hit.collider.transform == follow || hit.collider.transform.IsChildOf(follow))
                continue;
            
            // Skip objects that would be handled by the collision box (MainInteraction)
            // We can detect this by checking if the object's layer is in the camera's collider include layers
            if (ShouldObjectCauseCameraPull(hit.collider))
                continue;
            
            Renderer renderer = hit.collider.GetComponent<Renderer>();
            if (renderer != null && renderer.enabled)
            {
                objectsToFade.Add(renderer);
                
                if (!activeFades.Contains(renderer))
                {
                    StartFade(renderer);
                }
            }
        }
        
        // Stop fading objects that are no longer in the way
        List<Renderer> toStop = new List<Renderer>();
        foreach (Renderer renderer in activeFades)
        {
            if (!objectsToFade.Contains(renderer))
            {
                toStop.Add(renderer);
            }
        }
        
        foreach (Renderer renderer in toStop)
        {
            StopFade(renderer);
        }
        
        // Update alpha values
        UpdateAlphas(dt);
    }

    /// <summary>
    /// While flying, once the player has stopped all flight input (mouse look AND movement/
    /// ascend/descend keys - see chat history for why movement alone had to gate this too, not
    /// just mouse stillness) for recenterDelay seconds, smoothly eases the camera's pitch back to
    /// match the dragon's own current pitch. Measures the actual live camera angle vs. the
    /// dragon's actual angle and nudges the orbital axis by the DIFFERENCE, rather than assigning
    /// an absolute number - this sidesteps needing to know Cinemachine's exact convention for what
    /// VerticalAxis.Value's zero-point means, which turned out not to be a safe assumption for
    /// this rig. Reads from outputCamera (the real rendered camera) rather than this virtual
    /// camera's own transform, for the same Decollider-correction reason as the dragon-hide
    /// distance check uses it.
    /// </summary>
    private void HandleFlightRecentering(Transform follow, float dt)
    {
        if (orbital == null || follow == null) return;

        Transform recenterSource = recenterReferenceTransform != null ? recenterReferenceTransform : follow;

        bool lookInputActive = Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.01f;
        bool movementInputActive = Keyboard.current != null && (
            Keyboard.current.wKey.isPressed || Keyboard.current.aKey.isPressed ||
            Keyboard.current.sKey.isPressed || Keyboard.current.dKey.isPressed ||
            Keyboard.current.spaceKey.isPressed ||
            Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);

        timeSinceFlightInput = (lookInputActive || movementInputActive) ? 0f : timeSinceFlightInput + dt;

        if (!isFlying || timeSinceFlightInput < recenterDelay)
            return;

        // Direct absolute assignment, not the error-correction/differential version this had
        // briefly - that approach re-adds orbital.VerticalAxis.Value + pitchError every single
        // frame, and if the "current" reading lags by even one frame relative to what was just
        // set (very possible - Cinemachine's own pipeline runs after this script's Update), the
        // correction can overshoot and compound frame over frame instead of converging, which is
        // exactly what pinning at the Range's max looked like. This was never actually confirmed
        // broken in its simpler form - only horizontal (Center-based) was - so there was no real
        // reason for vertical to carry this extra complexity/risk in the first place. Horizontal
        // recentering below uses the same direct-assignment approach for the same reason.

        if (recenterVerticalWhileFlying)
        {
            float targetPitch = Mathf.Asin(Mathf.Clamp(recenterSource.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (invertVerticalRecenterSign) targetPitch = -targetPitch;
            targetPitch += verticalRecenterCenterOffset;
            targetPitch = Mathf.Clamp(targetPitch, minPitchAngle, maxPitchAngle);

            orbital.VerticalAxis.Value = Mathf.SmoothDampAngle(
                orbital.VerticalAxis.Value, targetPitch, ref verticalRecenterVelocity, recenterSmoothTime);
        }

        if (recenterHorizontalWhileFlying)
        {
            // Yaw (heading) from the same reference transform's forward vector, flattened onto
            // the horizontal plane - atan2(x, z) matches Unity's convention where +Z is 0 degrees.
            float targetYaw = Mathf.Atan2(recenterSource.forward.x, recenterSource.forward.z) * Mathf.Rad2Deg;
            if (invertHorizontalRecenterSign) targetYaw = -targetYaw;

            // HorizontalAxis is Wrap=true over its full -180..180 range (see chat history for why
            // that range specifically matters), and SmoothDampAngle is already wrap-aware - it
            // takes the shortest path around the circle rather than the long way through 180/-180,
            // so no extra wrapping logic is needed here.
            orbital.HorizontalAxis.Value = Mathf.SmoothDampAngle(
                orbital.HorizontalAxis.Value, targetYaw, ref horizontalRecenterVelocity, recenterSmoothTime);
        }
    }

    /// <summary>
    /// Hides the dragon whenever the camera gets closer than dragonHideDistance to the
    /// player - e.g. zoomed in tight in a small space. Shows it again once the camera has
    /// backed out past dragonHideDistance + dragonShowDistanceBuffer, which is added as
    /// hysteresis so it doesn't flicker while hovering right at the threshold.
    /// NOTE: this is distance-only for now - the wall/floor-pinned hiding this used to
    /// also do (via isCollidingWithPullObject) is parked, not removed; see the commented
    /// block below if you want to bring it back later.
    /// </summary>
    private void UpdateDragonVisibility(Transform follow)
    {
        if (dragonRenderers == null || dragonRenderers.Length == 0) return;

        // Use the actual rendered camera's position, not this virtual camera's own transform -
        // Decollider's wall-pushback correction moves the real on-screen camera without writing
        // that correction back to this CinemachineCamera's transform, so measuring from `this`
        // made the distance check blind to anything closeness caused by wall pushback.
        Transform distanceSource = outputCamera != null ? outputCamera.transform : transform;

        float distanceToPlayer = follow != null
            ? Vector3.Distance(distanceSource.position, follow.position)
            : float.MaxValue;

        bool shouldHide = distanceToPlayer < dragonHideDistance;
        bool shouldShowAgain = distanceToPlayer > dragonHideDistance + dragonShowDistanceBuffer;

        // Continuous visibility into the live number, independent of whether a hide/show
        // transition actually fires - without this, a "why doesn't it ever hide" report
        // gives zero data to look at, since the transition logs below only print on change.
        if (enableDebugLogs && Time.frameCount % 30 == 0)
            Debug.Log($"[DragonVisibility] distanceToPlayer={distanceToPlayer:F2} hideDistance={dragonHideDistance:F2} dragonHidden={dragonHidden}");

        // Parked for now - OR this into shouldHide (and gate shouldShowAgain on
        // !isCollidingWithPullObject, same as before) to bring back hide-while-pinned:
        // bool shouldHide = isCollidingWithPullObject || distanceToPlayer < dragonHideDistance;
        // bool shouldShowAgain = !isCollidingWithPullObject &&
        //     distanceToPlayer > dragonHideDistance + dragonShowDistanceBuffer;

        if (!dragonHidden && shouldHide)
        {
            SetDragonRenderersEnabled(false);
            dragonHidden = true;

            if (enableDebugLogs)
                Debug.Log($"Camera too close to dragon ({distanceToPlayer:F2}m) - hiding dragon");
        }
        else if (dragonHidden && shouldShowAgain)
        {
            SetDragonRenderersEnabled(true);
            dragonHidden = false;

            if (enableDebugLogs)
                Debug.Log("Camera backed away from dragon - showing dragon");
        }
    }

    private void SetDragonRenderersEnabled(bool enabled)
    {
        foreach (Renderer r in dragonRenderers)
        {
            if (r != null) r.enabled = enabled;
        }
    }

    /// <summary>
    /// Checks if an object should cause camera pull based on the collider's layer settings
    /// </summary>
    private bool ShouldObjectCauseCameraPull(Collider collider)
    {
        Collider cameraCollider = GetComponent<Collider>();
        if (cameraCollider == null) return false;
        
        // Get the layer of the hit object
        int objectLayer = collider.gameObject.layer;
        
        // Check if this layer would be detected by our trigger collider
        // This respects the collider's include/exclude layer settings
        // Fixed: IsTrigger is a property, not a method
        return cameraCollider.isTrigger && 
            (cameraCollider.includeLayers.value & (1 << objectLayer)) != 0 &&
            (cameraCollider.excludeLayers.value & (1 << objectLayer)) == 0;
    }

    // Draw gizmos for debug visualization
    void OnDrawGizmos()
    {
        if (!showDebugBox) return;
        
        Collider cameraCollider = GetComponent<Collider>();
        if (cameraCollider == null) return;
        
        // Draw the collider bounds
        Gizmos.color = Application.isPlaying && isCollidingWithPullObject ? Color.red : Color.green;
        
        if (cameraCollider is BoxCollider boxCollider)
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.TransformPoint(boxCollider.center), transform.rotation, boxCollider.size);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else if (cameraCollider is CapsuleCollider capsuleCollider)
        {
            // Draw capsule wireframe (simplified as a line)
            Vector3 top = transform.TransformPoint(capsuleCollider.center + Vector3.up * (capsuleCollider.height / 2 - capsuleCollider.radius));
            Vector3 bottom = transform.TransformPoint(capsuleCollider.center + Vector3.down * (capsuleCollider.height / 2 - capsuleCollider.radius));
            Gizmos.DrawLine(top, bottom);
            Gizmos.DrawWireSphere(top, capsuleCollider.radius);
            Gizmos.DrawWireSphere(bottom, capsuleCollider.radius);
        }
        
        // Draw line to colliding object if any
        if (Application.isPlaying && isCollidingWithPullObject && currentCollidingObject != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, currentCollidingObject.transform.position);
        }
    }

    // Transparency system methods (same as before)
    private void StartFade(Renderer renderer)
    {
        if (!propertyBlocks.ContainsKey(renderer))
        {
            propertyBlocks[renderer] = new MaterialPropertyBlock();
            
            Color originalColor = GetOriginalColor(renderer);
            originalColors[renderer] = originalColor;
            currentAlphas[renderer] = 1f;
            
            originalMaterials[renderer] = renderer.sharedMaterial;
            wasTransparent[renderer] = IsMaterialTransparent(renderer.sharedMaterial);
            
            if (!wasTransparent[renderer])
            {
                MakeMaterialTransparent(renderer);
            }
            
            if (enableDebugLogs)
                Debug.Log($"Start fading: {renderer.name}");
        }
        
        activeFades.Add(renderer);
    }

    private void StopFade(Renderer renderer)
    {
        if (propertyBlocks.ContainsKey(renderer))
        {
            SetRendererAlpha(renderer, 1f);
            
            if (!wasTransparent.ContainsKey(renderer) || !wasTransparent[renderer])
            {
                RestoreMaterialOpaque(renderer);
            }
            
            propertyBlocks.Remove(renderer);
            originalColors.Remove(renderer);
            currentAlphas.Remove(renderer);
            originalMaterials.Remove(renderer);
            wasTransparent.Remove(renderer);
        }
        activeFades.Remove(renderer);
    }

    private void UpdateAlphas(float dt)
    {
        foreach (Renderer renderer in activeFades)
        {
            if (renderer == null) continue;
            
            float current = currentAlphas[renderer];
            float newAlpha = Mathf.Lerp(current, targetAlpha, dt * fadeSpeed);
            currentAlphas[renderer] = newAlpha;
            SetRendererAlpha(renderer, newAlpha);
        }
    }

    private void MakeMaterialTransparent(Renderer renderer)
    {
        Material tempMaterial = new Material(renderer.sharedMaterial);
        
        if (tempMaterial.HasProperty(SurfaceProperty))
            tempMaterial.SetFloat(SurfaceProperty, 1f);
        
        if (tempMaterial.HasProperty(BlendProperty))
            tempMaterial.SetFloat(BlendProperty, 0f);
        
        if (tempMaterial.HasProperty(SrcBlendProperty))
            tempMaterial.SetFloat(SrcBlendProperty, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (tempMaterial.HasProperty(DstBlendProperty))
            tempMaterial.SetFloat(DstBlendProperty, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (tempMaterial.HasProperty(ZWriteProperty))
            tempMaterial.SetFloat(ZWriteProperty, 0f);
        
        tempMaterial.renderQueue = 3000;
        tempMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        tempMaterial.EnableKeyword("_ALPHABLEND_ON");
        tempMaterial.DisableKeyword("_ALPHATEST_ON");
        tempMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        
        renderer.material = tempMaterial;
    }

    private void RestoreMaterialOpaque(Renderer renderer)
    {
        if (originalMaterials.ContainsKey(renderer) && originalMaterials[renderer] != null)
        {
            renderer.sharedMaterial = originalMaterials[renderer];
        }
    }

    private bool IsMaterialTransparent(Material material)
    {
        if (material == null) return false;
        if (material.HasProperty(SurfaceProperty))
            return material.GetFloat(SurfaceProperty) == 1f;
        return material.renderQueue >= 3000;
    }

    private void SetRendererAlpha(Renderer renderer, float alpha)
    {
        MaterialPropertyBlock block;
        if (!propertyBlocks.ContainsKey(renderer))
        {
            block = new MaterialPropertyBlock();
            propertyBlocks[renderer] = block;
        }
        else
        {
            block = propertyBlocks[renderer];
        }
        
        renderer.GetPropertyBlock(block);
        
        if (HasMaterialProperty(renderer, BaseColorProperty))
        {
            Color color = originalColors.ContainsKey(renderer) ? originalColors[renderer] : Color.white;
            color.a = alpha;
            block.SetColor(BaseColorProperty, color);
        }
        else if (HasMaterialProperty(renderer, ColorProperty))
        {
            Color color = originalColors.ContainsKey(renderer) ? originalColors[renderer] : Color.white;
            color.a = alpha;
            block.SetColor(ColorProperty, color);
        }
        
        renderer.SetPropertyBlock(block);
    }
    
    private Color GetOriginalColor(Renderer renderer)
    {
        MaterialPropertyBlock tempBlock = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(tempBlock);
        
        Color color = tempBlock.GetColor(BaseColorProperty);
        if (color != Color.clear) return color;
        
        color = tempBlock.GetColor(ColorProperty);
        if (color != Color.clear) return color;
        
        if (renderer.sharedMaterial != null)
        {
            if (renderer.sharedMaterial.HasProperty(BaseColorProperty))
                return renderer.sharedMaterial.GetColor(BaseColorProperty);
            if (renderer.sharedMaterial.HasProperty(ColorProperty))
                return renderer.sharedMaterial.GetColor(ColorProperty);
        }
        
        return Color.white;
    }
    
    private bool HasMaterialProperty(Renderer renderer, int propertyId)
    {
        if (renderer.sharedMaterial != null)
            return renderer.sharedMaterial.HasProperty(propertyId);
        return false;
    }
    
    private void RestoreAllTransparentRenderers()
    {
        foreach (Renderer renderer in activeFades)
        {
            if (renderer != null)
            {
                SetRendererAlpha(renderer, 1f);
                if (originalMaterials.ContainsKey(renderer) && 
                    (!wasTransparent.ContainsKey(renderer) || !wasTransparent[renderer]))
                {
                    renderer.sharedMaterial = originalMaterials[renderer];
                }
            }
        }
        
        propertyBlocks.Clear();
        originalColors.Clear();
        currentAlphas.Clear();
        activeFades.Clear();
        originalMaterials.Clear();
        wasTransparent.Clear();
    }

    private void ConfigureAxes()
    {
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

        if (orbital != null)
        {
            orbital.VerticalAxis.Range = new Vector2(minPitchAngle, maxPitchAngle);
            orbital.VerticalAxis.Wrap = false;
        }
    }

    public void SetSensitivity(float horizontal, float vertical)
    {
        mouseSensitivityX = horizontal;
        mouseSensitivityY = vertical;
        ConfigureAxes();
    }

    public void setSensitivity(float newSpeed) => SetSensitivity(newSpeed, newSpeed);

    public static void setCameraZoomLimitOnFly(bool zoom)
    {
        // Instance can be null for one frame if this somehow fires before Start() has run -
        // extremely unlikely given the camera initializes well before the player can take off,
        // but a null-check here costs nothing and avoids a hard NullReferenceException either way.
        if (Instance == null) return;

        Instance.isFlying = zoom;
        Instance.minPitchAngle = zoom ? Instance.flyingMinPitch : Instance.groundedMinPitch;
        Instance.maxPitchAngle = zoom ? Instance.flyingMaxPitch : Instance.groundedMaxPitch;
    }
}
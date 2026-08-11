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
    [SerializeField] private static float minPitchAngle = -30f;
    [SerializeField] private static float maxPitchAngle = 70f;

    [Header("Object Transparency (for all other layers)")]
    [SerializeField] private LayerMask transparentMask = ~0;
    [SerializeField] private float fadeSpeed = 5f;
    [SerializeField] [Range(0f, 1f)] private float targetAlpha = 0.2f;
    
    [Header("Camera Pull Settings")]
    [SerializeField] private float collisionPullInSpeed = 20f;
    [SerializeField] private float collisionPullOutSpeed = 4f;
    [SerializeField] private float collisionBuffer = 0.3f;
    
    [Header("Collision Box Settings")]
    [SerializeField] private bool showDebugBox = true;

    [Header("Dragon Hide-on-Collision")]
    [Tooltip("Renderers to hide while the camera is blocked by a solid object (floor, wall, etc.), e.g. body, wings, horns.")]
    [SerializeField] private Renderer[] dragonRenderers;

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

    public static bool CameraLocked = false;

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
        controls = new PlayerControls();
        controls.Enable();
        controls.Camera.MouseZoom.performed += HandleMouseScroll;

        cam = GetComponent<CinemachineCamera>();
        orbital = cam.GetComponent<CinemachineOrbitalFollow>();
        inputAxis = cam.GetComponent<CinemachineInputAxisController>();

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
        if (CameraLocked)
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
            targetZoom = Mathf.Clamp(
                currentZoom - scrollDelta.y * zoomSpeed,
                minDistance, maxDistance);
            scrollDelta = Vector2.zero;
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
        if (follow == null) return;

        // Handle camera pull based on trigger collisions
        float desiredRadius = ResolveCollisionWithTrigger(currentZoom);
        
        float lerpSpeed = desiredRadius < collisionZoom
            ? collisionPullInSpeed
            : collisionPullOutSpeed;
        
        collisionZoom = Mathf.Lerp(collisionZoom, desiredRadius, dt * lerpSpeed);

        // Hide the dragon while the camera is pinned against something solid
        // (floor, wall, etc.) so it doesn't clip through the dragon's model.
        UpdateDragonVisibility();

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
    /// Hides the dragon while the camera is actively being blocked by something solid
    /// (floor, wall, or any other object on the collision-pull layer) - i.e. the exact
    /// moment the camera would otherwise clip into the dragon because it's pinned
    /// against a surface. Shows it again as soon as that collision ends. Driven off
    /// isCollidingWithPullObject rather than raw distance, so it only fires for real
    /// solid-object blocking, not just "camera happens to be near the dragon".
    /// </summary>
    private void UpdateDragonVisibility()
    {
        if (dragonRenderers == null || dragonRenderers.Length == 0) return;

        if (!dragonHidden && isCollidingWithPullObject)
        {
            SetDragonRenderersEnabled(false);
            dragonHidden = true;

            if (enableDebugLogs)
                Debug.Log($"Camera pinned against {currentCollidingObject?.name} - hiding dragon");
        }
        else if (dragonHidden && !isCollidingWithPullObject)
        {
            SetDragonRenderersEnabled(true);
            dragonHidden = false;

            if (enableDebugLogs)
                Debug.Log("Camera cleared the obstruction - showing dragon");
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
        minPitchAngle = zoom ? -70f : -40f;
        maxPitchAngle = zoom ? 70f : 40f;
    }
}
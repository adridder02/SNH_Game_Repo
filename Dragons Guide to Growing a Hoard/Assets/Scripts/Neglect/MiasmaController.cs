using UnityEngine;
using System.Collections.Generic;

public class MiasmaController : MonoBehaviour
{
    public enum MiasmaIntensity { Easy, Mild, Intense }

    [Header("Miasma Growth")]
    [Tooltip("Whether the miasma is currently growing/shrinking. Toggled by flipSize().")]
    [SerializeField] private bool incSize = false;

    [Tooltip("Start growing immediately on Start, without waiting for something to call flipSize(). " +
             "IMPORTANT: if something else (e.g. MainUIController.autoStartMiasmaGrowth) also calls " +
             "flipSize()/SetGrowing() on Start, only enable ONE of them - flipSize() is a toggle, so " +
             "two callers turning growth on/off in sequence will cancel each other out.")]
    public bool growOnStart = true;

    [Tooltip("Units of scale gained per second. Negative values shrink the miasma.")]
    public float rateOfMiasma = 1f;

    [Tooltip("Scale the miasma starts at.")]
    public float startSize = 2f;

    [Tooltip("Smallest scale the miasma can shrink to. Keep this above 0.")]
    public float minSize = 1f;

    [Tooltip("Largest scale the miasma can grow to.")]
    public float maxSize = 250f;


    [Header("Intensity Settings")]
    [Tooltip("Current intensity level")]
    public MiasmaIntensity currentIntensity = MiasmaIntensity.Easy;

    [Tooltip("Time in contact with plants to increase intensity (seconds)")]
    public float timeToIncreaseIntensity = 120f;

    [Tooltip("Easy intensity debuff values (applied every minute)")]
    public float easyLightPenalty = 0.01f;
    public int easySoilPenalty = 1;
    public float easyWaterDrainMultiplier = .02f;

    [Tooltip("Mild intensity debuff values")]
    public float mildLightPenalty = 0.02f;
    public int mildSoilPenalty = 1;
    public float mildWaterDrainMultiplier = .05f;

    [Tooltip("Intense intensity debuff values")]
    public float intenseLightPenalty = 0.3f;
    public int intenseSoilPenalty = 1;
    public float intenseWaterDrainMultiplier = .05f;


    [Header("Timing")]
    [Tooltip("How often to apply debuff ticks (seconds)")]
    public float debuffTickInterval = 60f;


    [Header("Visuals")]
    public Color easyColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    public Color mildColor = new Color(0.7f, 0.3f, 0.7f, 0.4f);
    public Color intenseColor = new Color(0.9f, 0.1f, 0.9f, 0.5f);


    [Header("Fog Particle System")]
    [Tooltip("Optional particle system used for a fog/mist visual. Drag your fog Particle System GameObject here. " +
             "It should NOT be a child of the miasma sphere. The script makes it follow this object's position.")]
    public ParticleSystem fogParticles;

    [Tooltip("If the fog system IS parented under a scaling object, detach it automatically at Start.")]
    public bool autoUnparentFog = true;

    [Tooltip("World-space offset applied to the fog system relative to this object's position.")]
    public Vector3 fogPositionOffset = Vector3.zero;

    [Tooltip("Controls how far particles are distributed from the centre of the miasma. " +
             "0.5 means the particle cloud radius is half the miasma radius.")]
    public float fogRadiusMultiplier = 0.5f;

    [Tooltip("Size of individual fog particles. Particles stay approximately this size while " +
             "the overall gas cloud expands.")]
    public float fogParticleSize = 2f;

    [Tooltip("Fog particle colors per intensity. Keep alpha low so it reads as mist rather than a solid blob.")]
    public Color easyFogColor = new Color(0.5f, 0.5f, 0.5f, 0.15f);
    public Color mildFogColor = new Color(0.7f, 0.3f, 0.7f, 0.25f);
    public Color intenseFogColor = new Color(0.9f, 0.1f, 0.9f, 0.35f);

    [Tooltip("Particles emitted per second, per intensity level.")]
    public float easyFogEmissionRate = 30f;
    public float mildFogEmissionRate = 60f;
    public float intenseFogEmissionRate = 100f;

    [Tooltip("How much a fog particle's size grows per unit of emission-sphere radius. This is the " +
             "actual fix for density: the OLD approach kept particle size fixed and only expanded the " +
             "emission volume, so a big miasma spread the same handful of tiny particles across a much " +
             "bigger space and looked empty instead of foggy — the opposite of what you want in a large " +
             "room. Scaling size with radius means fewer, bigger, heavily-overlapping soft particles, " +
             "which reads as thick fog and stays cheap since particle COUNT doesn't need to grow.")]
    public float fogSizeGrowthFactor = 0.35f;

    [Tooltip("Upper bound on scaled particle size (see fogSizeGrowthFactor), so particles don't turn " +
             "into single giant visible blobs/sprites at very large miasma sizes.")]
    public float fogMaxParticleSize = 20f;

    [Tooltip("Hard cap on simultaneous fog particles (ParticleSystem.MainModule.maxParticles). " +
             "Density now comes mainly from particle SIZE (fogSizeGrowthFactor), not particle count, " +
             "so this can stay low without the cloud looking sparse as it spreads.")]
    public int fogMaxParticles = 300;


    [Header("Performance")]
    [Tooltip("How often (seconds) to run the Physics.OverlapSphere plant-contact check. This used to " +
             "run every single Update() frame with radius = currentSize, which can reach maxSize (250) " +
             "— an uncapped 250-unit overlap query 60x/sec is expensive and is very likely the real " +
             "source of the reported lag, separate from the fog particles. Contact/debuff timing still " +
             "advances every frame via Time.deltaTime in UpdateIntensity()/ApplyDebuffsToPlants(); only " +
             "the discovery of which plants are currently inside the miasma is throttled.")]
    public float plantCheckInterval = 0.25f;
    private float plantCheckTimer = 0f;


    [Header("Sphere Visibility")]
    [Tooltip("The tint sphere uses Render Face = Back so the inside surface tints the screen while " +
             "you're inside it. The tradeoff: a convex sphere's back face is still whatever's on the " +
             "FAR side of it from the camera, so a camera standing outside sees straight through the " +
             "near (culled) side to the far back face and gets a big flat tinted panel hanging in space " +
             "— that's the pink wall in your screenshot. Fix: only enable the renderer while the camera " +
             "is actually inside the sphere.")]
    [SerializeField] private bool hideSphereWhenCameraOutside = true;


    [Header("Fog Emission (glow)")]
    [Tooltip("Multiplies fogColor (per intensity) to produce the HDR emission color sent to the fog " +
             "material each frame — push above 1 to actually glow once Bloom is set up. REQUIRES: the " +
             "fog material's own 'Emission' checkbox must be enabled in the Inspector first (and the " +
             "shader must support it — the legacy Mobile/Particles shaders don't, use a URP Particles " +
             "shader instead, e.g. Universal Render Pipeline/Particles/Unlit with Surface Type = " +
             "Transparent). This script only overrides the emission COLOR at runtime via a " +
             "MaterialPropertyBlock — it can't turn the feature on for you.")]
    public float fogEmissionIntensity = 3f;

    [Tooltip("If off, emission color is left alone (whatever's baked into the material). Turn on once " +
             "the fog material has Emission enabled and you want it to shift with intensity the same " +
             "way fogColor does.")]
    public bool driveFogEmissionFromScript = true;


    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;


    // Runtime variables
    private float contactTimer = 0f;
    private HashSet<PlantState> plantsInMiasma = new HashSet<PlantState>();
    private Dictionary<PlantState, float> lastDebuffTime = new Dictionary<PlantState, float>();

    private Renderer sphereRenderer;
    private float currentSize = 1f;
    private Camera mainCamera;
    private ParticleSystemRenderer fogRenderer;
    private MaterialPropertyBlock fogPropBlock;


    // Cached fog particle modules
    private ParticleSystem.ShapeModule fogShape;
    private ParticleSystem.MainModule fogMain;
    private ParticleSystem.EmissionModule fogEmission;
    private Transform fogTransform;
    private bool hasFogParticles = false;


    /// <summary>
    /// Current miasma sphere size.
    /// </summary>
    public float CurrentSize => currentSize;


    /// <summary>
    /// Upper bound currentSize will grow to.
    /// </summary>
    public float MaxSize => maxSize;


    /// <summary>
    /// Miasma fill as a 0-1 value.
    /// </summary>
    public float NormalisedSize =>
        maxSize <= 0f ? 0f : Mathf.Clamp01(currentSize / maxSize);


    void Awake()
    {
        // Set the starting scale.
        ApplySize(startSize);
    }


    void Start()
    {
        if (growOnStart)
            incSize = true;


        // Get renderer for visual feedback.
        sphereRenderer = GetComponent<Renderer>();

        if (sphereRenderer == null)
            sphereRenderer = GetComponentInChildren<Renderer>();


        // Disable shadows so light passes through.
        if (sphereRenderer != null)
        {
            sphereRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            sphereRenderer.receiveShadows = false;
        }


        // Cache the camera once so we're not doing a Camera.main lookup every frame.
        mainCamera = Camera.main;


        SetupFogParticles();

        UpdateVisuals();


        if (showDebugLogs)
        {
            Debug.Log(
                $"[MiasmaController] Initialized at size {currentSize} " +
                $"(max {maxSize}), Intensity: {currentIntensity}, Growing: {incSize}"
            );
        }
    }


    void SetupFogParticles()
    {
        if (fogParticles == null)
            return;


        fogTransform = fogParticles.transform;


        // The particle system should not inherit the miasma's scale.
        if (autoUnparentFog && fogTransform.parent != null)
        {
            fogTransform.SetParent(null, true);

            if (showDebugLogs)
            {
                Debug.Log(
                    "[MiasmaController] Fog particle system detached " +
                    "from its parent to avoid inheriting miasma scale."
                );
            }
        }


        // Keep particle system scale at 1.
        fogTransform.localScale = Vector3.one;


        // Cache particle modules.
        fogShape = fogParticles.shape;
        fogMain = fogParticles.main;
        fogEmission = fogParticles.emission;

        hasFogParticles = true;


        // Particle values are interpreted locally.
        fogMain.scalingMode = ParticleSystemScalingMode.Local;


        // World simulation makes the fog drift independently.
        fogMain.simulationSpace =
            ParticleSystemSimulationSpace.World;


        // Make sure the required modules are enabled.
        fogEmission.enabled = true;
        fogShape.enabled = true;


        // Use a sphere as the emission volume.
        fogShape.shapeType =
            ParticleSystemShapeType.Sphere;


        // Individual particles remain a consistent size.
        fogMain.startSize = fogParticleSize;

        // Give the system a fixed particle budget rather than whatever the Inspector
        // default happens to be, so emission rate tuning can't silently blow past it.
        fogMain.maxParticles = fogMaxParticles;


        // Shadows from a dense particle cloud are a common invisible lag source — a few
        // hundred overlapping semi-transparent quads each casting/receiving shadows adds a
        // lot of overdraw for very little visual payoff on something that's meant to read
        // as soft mist. Same treatment sphereRenderer already gets above.
        //
        // Cached as a field (not a local) so UpdateFogVisuals() can reuse it every frame to
        // drive the emission color, instead of calling GetComponent again each frame.
        fogRenderer = fogParticles.GetComponent<ParticleSystemRenderer>();
        fogPropBlock = new MaterialPropertyBlock();

        if (fogRenderer != null)
        {
            fogRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fogRenderer.receiveShadows = false;
        }


        if (!fogParticles.isPlaying)
            fogParticles.Play();
    }


    void Update()
    {
        HandleGrowth();

        // Check for plants in miasma. Throttled — see plantCheckInterval tooltip; an
        // uncapped OverlapSphere at radius=currentSize every frame is expensive once the
        // miasma has grown large.
        plantCheckTimer += Time.deltaTime;
        if (plantCheckTimer >= plantCheckInterval)
        {
            plantCheckTimer = 0f;
            CheckForPlants();
        }

        // Update intensity based on contact.
        UpdateIntensity();

        // Apply debuffs to plants.
        ApplyDebuffsToPlants();

        // Hide the tint sphere when the camera isn't inside it (see hideSphereWhenCameraOutside tooltip).
        UpdateSphereVisibility();

        // Update visual appearance.
        UpdateVisuals();
    }


    void HandleGrowth()
    {
        if (!incSize)
        {
            // Keep currentSize synchronized with the actual transform.
            currentSize = transform.localScale.x;
            return;
        }


        // Frame-rate independent growth.
        ApplySize(
            currentSize +
            rateOfMiasma * Time.deltaTime
        );
    }


    /// <summary>
    /// Sets the miasma scale, clamped to [minSize, maxSize].
    /// </summary>
    void ApplySize(float newSize)
    {
        float safeMin = Mathf.Max(0.01f, minSize);

        currentSize = Mathf.Clamp(
            newSize,
            safeMin,
            maxSize
        );


        transform.localScale = new Vector3(
            currentSize,
            currentSize,
            currentSize
        );
    }


    void LateUpdate()
    {
        // Because the fog is unparented, move it manually.
        if (hasFogParticles && fogTransform != null)
        {
            fogTransform.position =
                transform.position +
                fogPositionOffset;
        }
    }


    void CheckForPlants()
    {
        // Find all plants inside the miasma sphere.
        Collider[] hits =
            Physics.OverlapSphere(
                transform.position,
                currentSize
            );


        HashSet<PlantState> currentPlants =
            new HashSet<PlantState>();


        foreach (Collider hit in hits)
        {
            PlantState plant =
                hit.GetComponent<PlantState>();


            if (plant == null)
            {
                plant =
                    hit.GetComponentInParent<PlantState>();
            }


            if (plant != null)
            {
                currentPlants.Add(plant);


                // New plant entered.
                if (!plantsInMiasma.Contains(plant))
                {
                    plantsInMiasma.Add(plant);

                    lastDebuffTime[plant] =
                        Time.time;


                    if (showDebugLogs)
                    {
                        Debug.Log(
                            $"[MiasmaController] " +
                            $"{plant.gameObject.name} entered miasma - " +
                            $"Intensity: {currentIntensity}"
                        );
                    }
                }
            }
        }


        // Remove plants that left.
        List<PlantState> toRemove =
            new List<PlantState>();


        foreach (var plant in plantsInMiasma)
        {
            if (plant == null ||
                !currentPlants.Contains(plant))
            {
                toRemove.Add(plant);


                if (showDebugLogs && plant != null)
                {
                    Debug.Log(
                        $"[MiasmaController] " +
                        $"{plant.gameObject.name} left miasma"
                    );
                }
            }
        }


        foreach (var plant in toRemove)
        {
            plantsInMiasma.Remove(plant);
            lastDebuffTime.Remove(plant);
        }
    }


    void UpdateIntensity()
    {
        if (plantsInMiasma.Count > 0)
        {
            // Increase contact timer.
            contactTimer += Time.deltaTime;


            if (contactTimer >= timeToIncreaseIntensity)
            {
                contactTimer = 0f;


                switch (currentIntensity)
                {
                    case MiasmaIntensity.Easy:

                        currentIntensity =
                            MiasmaIntensity.Mild;


                        if (showDebugLogs)
                        {
                            Debug.Log(
                                "<color=purple>" +
                                "[MiasmaController] " +
                                "Intensity increased to MILD!" +
                                "</color>"
                            );
                        }

                        break;


                    case MiasmaIntensity.Mild:

                        currentIntensity =
                            MiasmaIntensity.Intense;


                        if (showDebugLogs)
                        {
                            Debug.Log(
                                "<color=red>" +
                                "[MiasmaController] " +
                                "Intensity increased to INTENSE!" +
                                "</color>"
                            );
                        }

                        break;
                }
            }
        }
        else
        {
            // No plants in miasma - reset to Easy.
            if (currentIntensity != MiasmaIntensity.Easy)
            {
                currentIntensity =
                    MiasmaIntensity.Easy;

                contactTimer = 0f;


                if (showDebugLogs)
                {
                    Debug.Log(
                        "[MiasmaController] " +
                        "Intensity reset to EASY " +
                        "(no plants in area)"
                    );
                }
            }
        }
    }


    void ApplyDebuffsToPlants()
    {
        float currentTime = Time.time;


        foreach (var plant in plantsInMiasma)
        {
            if (plant == null)
                continue;


            if (lastDebuffTime.TryGetValue(
                plant,
                out float lastTime))
            {
                if (currentTime - lastTime >=
                    debuffTickInterval)
                {
                    ApplyDebuffToPlant(plant);

                    lastDebuffTime[plant] =
                        currentTime;
                }
            }
        }
    }


    void ApplyDebuffToPlant(PlantState plant)
    {
        float lightPenalty = 0f;
        int soilPenalty = 0;
        float waterDrainMultiplier = 1f;


        switch (currentIntensity)
        {
            case MiasmaIntensity.Easy:

                lightPenalty =
                    easyLightPenalty;

                soilPenalty =
                    easySoilPenalty;

                waterDrainMultiplier =
                    easyWaterDrainMultiplier;

                break;


            case MiasmaIntensity.Mild:

                lightPenalty =
                    mildLightPenalty;

                soilPenalty =
                    mildSoilPenalty;

                waterDrainMultiplier =
                    mildWaterDrainMultiplier;

                break;


            case MiasmaIntensity.Intense:

                lightPenalty =
                    intenseLightPenalty;

                soilPenalty =
                    intenseSoilPenalty;

                waterDrainMultiplier =
                    intenseWaterDrainMultiplier;

                break;
        }


        plant.ApplyMiasmaDebuff(
            lightPenalty,
            soilPenalty,
            waterDrainMultiplier
        );


        if (showDebugLogs)
        {
            Debug.Log(
                $"[MiasmaController] Debuff applied to " +
                $"{plant.gameObject.name} - " +
                $"Intensity: {currentIntensity}, " +
                $"Light: -{lightPenalty}, " +
                $"Soil: -{soilPenalty}, " +
                $"Water Drain: x{waterDrainMultiplier}"
            );
        }
    }


    void UpdateSphereVisibility()
    {
        if (!hideSphereWhenCameraOutside || sphereRenderer == null)
            return;

        // Camera can change (cutscenes, splitscreen, etc.) — re-grab if the cached one died.
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        float camDistance = Vector3.Distance(mainCamera.transform.position, transform.position);

        // BUGFIX: this used to compare camDistance against currentSize directly, on the
        // assumption that currentSize IS the sphere's world-space radius. That's only true if
        // the mesh's local bounds have a radius of exactly 1. Unity's built-in sphere primitive
        // is radius 0.5, so the REAL visible surface sits at roughly half of currentSize — the
        // gap between "assumed radius" and "actual radius" is small at low currentSize but
        // grows into a wide band as the miasma gets large, and inside that band the camera is
        // genuinely outside the rendered mesh while this check still thought it was "inside"
        // and left the renderer on — reproducing the exact pink-wall bug, but only once the
        // fog/sphere had grown big enough for the gap to matter. Compute the real world radius
        // from the renderer's own local bounds instead of assuming.
        float actualWorldRadius = sphereRenderer.localBounds.extents.x * transform.lossyScale.x;

        // Small safety margin so the renderer switches off slightly before the camera actually
        // crosses the surface, avoiding a possible one-frame flash right at the boundary.
        sphereRenderer.enabled = camDistance <= actualWorldRadius * 0.97f;
    }


    void UpdateVisuals()
    {
        if (sphereRenderer != null)
        {
            switch (currentIntensity)
            {
                case MiasmaIntensity.Easy:

                    sphereRenderer.material.color =
                        easyColor;

                    break;


                case MiasmaIntensity.Mild:

                    sphereRenderer.material.color =
                        mildColor;

                    break;


                case MiasmaIntensity.Intense:

                    sphereRenderer.material.color =
                        intenseColor;

                    break;
            }
        }


        if (hasFogParticles)
            UpdateFogVisuals();
    }


    void UpdateFogVisuals()
    {
        // =========================================================
        // MAIN GAS EXPANSION
        // =========================================================
        //
        // The particle system's sphere gets larger as the miasma
        // grows, so the fog keeps spreading to cover a growing room.
        //
        fogShape.radius = currentSize * fogRadiusMultiplier;


        // =========================================================
        // PARTICLE SIZE
        // =========================================================
        //
        // REVISED: particles now grow WITH the radius (they used to stay a
        // fixed size while only the emission volume expanded, which spread
        // the same handful of particles across an ever-bigger space and
        // made large miasmas look empty instead of foggy). Fewer, bigger,
        // overlapping soft particles keep the cloud reading as dense no
        // matter how large a room it needs to fill, without needing more
        // particles (so it stays cheap).
        //
        float scaledSize = fogParticleSize + fogShape.radius * fogSizeGrowthFactor;
        fogMain.startSize = Mathf.Min(scaledSize, fogMaxParticleSize);


        // =========================================================
        // INTENSITY
        // =========================================================

        Color fogColor;
        float emissionRate;


        switch (currentIntensity)
        {
            case MiasmaIntensity.Mild:

                fogColor =
                    mildFogColor;

                emissionRate =
                    mildFogEmissionRate;

                break;


            case MiasmaIntensity.Intense:

                fogColor =
                    intenseFogColor;

                emissionRate =
                    intenseFogEmissionRate;

                break;


            default:

                fogColor =
                    easyFogColor;

                emissionRate =
                    easyFogEmissionRate;

                break;
        }


        fogMain.startColor =
            fogColor;


        // Change how much fog is being emitted.
        ParticleSystem.MinMaxCurve rate =
            fogEmission.rateOverTime;


        rate.constant =
            emissionRate;


        fogEmission.rateOverTime =
            rate;


        // =========================================================
        // EMISSION (glow)
        // =========================================================
        //
        // Uses a MaterialPropertyBlock rather than touching fogRenderer.material directly —
        // that would silently create a per-instance material copy and break GPU
        // instancing/batching for the particle system. Requires the fog material's own
        // "Emission" checkbox to already be enabled in the Inspector (and a shader that
        // supports it, e.g. Universal Render Pipeline/Particles/Unlit) — this only overrides
        // the color value each frame, it can't turn the shader feature on.
        if (driveFogEmissionFromScript && fogRenderer != null && fogPropBlock != null)
        {
            Color emissiveColor = new Color(
                fogColor.r * fogEmissionIntensity,
                fogColor.g * fogEmissionIntensity,
                fogColor.b * fogEmissionIntensity,
                1f // alpha doesn't matter for emission — it isn't blended, it's added light.
            );

            fogRenderer.GetPropertyBlock(fogPropBlock);
            fogPropBlock.SetColor("_EmissionColor", emissiveColor);
            fogRenderer.SetPropertyBlock(fogPropBlock);
        }
    }


    // =============================================================
    // PUBLIC METHODS
    // =============================================================

    public void flipSize()
    {
        this.incSize =
            !incSize;


        if (showDebugLogs)
        {
            Debug.Log(
                $"[MiasmaController] Size growth: " +
                $"{(incSize ? "STARTED" : "STOPPED")}"
            );
        }
    }


    /// <summary>
    /// Explicitly start or stop growth.
    /// </summary>
    public void SetGrowing(bool growing)
    {
        incSize =
            growing;


        if (showDebugLogs)
        {
            Debug.Log(
                $"[MiasmaController] Size growth: " +
                $"{(incSize ? "STARTED" : "STOPPED")}"
            );
        }
    }


    public void SetGrowthRate(float newRate)
    {
        rateOfMiasma =
            newRate;


        if (showDebugLogs)
        {
            Debug.Log(
                $"[MiasmaController] " +
                $"Growth rate set to {newRate}"
            );
        }
    }


    public void ResetIntensity()
    {
        currentIntensity =
            MiasmaIntensity.Easy;

        contactTimer = 0f;


        if (showDebugLogs)
        {
            Debug.Log(
                "[MiasmaController] " +
                "Intensity reset to EASY"
            );
        }
    }


    public int GetPlantsInMiasmaCount()
    {
        return plantsInMiasma.Count;
    }


    void OnDrawGizmosSelected()
    {
        // Visualize the miasma radius in the editor.
        Gizmos.color =
            new Color(
                0.5f,
                0f,
                0.5f,
                0.3f
            );


        Gizmos.DrawWireSphere(
            transform.position,
            transform.localScale.x
        );
    }
}
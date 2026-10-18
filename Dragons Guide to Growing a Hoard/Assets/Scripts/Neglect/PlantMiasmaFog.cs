using UnityEngine;

// =============================================================
// PlantMiasmaFog.cs  (now PER POT, not per plant)
// -------------------------------------------------------------
// A small, subtle fog box sitting over a pot while the plant in it is under miasma influence -
// deliberately minor, NOT the big gas cloud MiasmaController uses for the miasma zone as a whole.
// Intensity (emission + opacity) follows the planted PlantState.MiasmaInfluence01 - the SAME value
// driving the overhead miasma bar - and the fog is fully stopped and cleared at 0.
//
// Because it lives on the POT, it covers every plant that ever goes in that pot (and permanent/static
// pots, and fixed-soil pools) with one setup per pot prefab instead of one per plant prefab.
//
// The fog box is sized to the pot's footprint (width x length) automatically; you only choose its
// height. It follows the pot if the pot is moved, and hides while the pot is picked up/inactive.
//
// SETUP:
//   1. Add this to each POT prefab/object (the one with PotContents), including permanent pots.
//      REMOVE it from plant prefabs - it's no longer a per-plant component.
//   2. Give it a fog particle system: either assign Fog Particles (a ParticleSystem object, ideally
//      kept in the scene/prefab as a child) OR assign Fog Prefab (a prefab with a ParticleSystem) and a
//      copy is spawned for you. Author it for the FULL-strength (1.0) look; this script only controls
//      its box shape, emission rate and alpha - it forces the shape to a Box.
//   3. Set Fog Height (and Base Height Offset if the fog should start above/below the pot's top).
// =============================================================
public class PlantMiasmaFog : MonoBehaviour
{
    public enum SizeSource { Renderers, Collider }

    [Tooltip("Auto-found on this object or a parent if left empty.")]
    [SerializeField] private PotContents pot;

    [Header("Fog Particles")]
    [Tooltip("A ParticleSystem authored for the FULL (1.0) influence look. It gets detached from the pot at " +
             "start (so pot rotation/scale can't distort it) and is moved/resized to match the pot. Leave " +
             "empty and assign Fog Prefab to spawn one instead.")]
    [SerializeField] private ParticleSystem fogParticles;
    [Tooltip("Spawned (one per pot) when Fog Particles is empty - lets every pot share one prefab asset.")]
    [SerializeField] private ParticleSystem fogPrefab;

    [Header("Box Size")]
    [Tooltip("What the fog's width/length is measured from. Renderers = the pot's visible meshes (ignores the " +
             "plant and fog); Collider = the pot's Collider bounds.")]
    [SerializeField] private SizeSource sizeSource = SizeSource.Renderers;
    [Tooltip("Height of the fog box, in world units. This is the Y value you set.")]
    [Min(0.01f)] [SerializeField] private float fogHeight = 1.2f;
    [Tooltip("Where the fog box's BOTTOM sits relative to the top of the pot. 0 = starts at the pot's rim, " +
             "negative = starts lower, positive = floats higher.")]
    [SerializeField] private float baseHeightOffset = 0f;
    [Tooltip("Extra margin added to the width and length (world units), e.g. 0.1 = a little wider than the pot.")]
    [SerializeField] private float footprintPadding = 0f;

    [Header("Intensity")]
    [Tooltip("Emission rate (particles/sec) at full (1.0) influence for a pot with the Reference Area below.")]
    [SerializeField] private float maxEmissionRate = 4f;
    [Tooltip("Scale the emission rate with the pot's footprint area so a big pot isn't sparser than a small one.")]
    [SerializeField] private bool scaleEmissionWithArea = true;
    [Tooltip("Footprint area (width x length, world units squared) that Max Emission Rate is authored for.")]
    [Min(0.01f)] [SerializeField] private float referenceArea = 1f;
    [Tooltip("How quickly the displayed intensity eases toward the real value, in seconds.")]
    [SerializeField] private float smoothTime = 1.5f;

    private Transform fogTransform;
    private ParticleSystem.EmissionModule emission;
    private ParticleSystem.MainModule main;
    private Color baseColor;
    private float smoothedInfluence;
    private float influenceVelocity;
    private bool initialized;
    private float currentArea = 1f;

    private Vector3 lastPos;
    private Quaternion lastRot;
    private float nextRefresh;

    private void Awake()
    {
        if (pot == null) pot = GetComponentInParent<PotContents>();
        if (pot == null)
        {
            Debug.LogWarning("[PlantMiasmaFog] No PotContents found on this object or its parents - this component " +
                             "now goes on the POT, not the plant.", this);
            enabled = false;
            return;
        }

        if (fogParticles == null && fogPrefab != null)
            fogParticles = Instantiate(fogPrefab);

        if (fogParticles == null)
        {
            Debug.LogWarning("[PlantMiasmaFog] Assign Fog Particles or Fog Prefab.", this);
            enabled = false;
            return;
        }

        fogTransform = fogParticles.transform;
        fogTransform.SetParent(null, true);    // detached: world-aligned box, unaffected by pot rotation/scale
        fogTransform.rotation = Quaternion.identity;
        fogTransform.localScale = Vector3.one;
        fogTransform.name = $"{name}_MiasmaFog";

        emission = fogParticles.emission;
        main = fogParticles.main;
        baseColor = main.startColor.color;     // preserves whatever colour/alpha was authored
        var shape = fogParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;
        shape.scale = Vector3.one;

        fogParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        initialized = true;
        RefreshShape();
    }

    private void OnEnable()
    {
        if (initialized) RefreshShape();
    }

    private void OnDisable()
    {
        if (!initialized || fogParticles == null) return;
        fogParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        smoothedInfluence = 0f;
        influenceVelocity = 0f;
    }

    private void OnDestroy()
    {
        if (fogTransform != null) Destroy(fogTransform.gameObject);
    }

    // ---------------------------------------------------------------
    // Box placement/size from the pot
    // ---------------------------------------------------------------
    private bool TryGetPotBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;

        if (sizeSource == SizeSource.Renderers)
        {
            foreach (Renderer r in pot.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (r.GetComponentInParent<PlantState>() != null) continue; // the plant isn't part of the pot
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
        }

        if (!any)
        {
            Collider c = pot.GetComponent<Collider>();
            if (c != null) { bounds = c.bounds; any = true; }
        }
        return any;
    }

    private void RefreshShape()
    {
        if (!initialized || pot == null) return;
        if (!TryGetPotBounds(out Bounds b)) return;

        float w = Mathf.Max(0.05f, b.size.x + footprintPadding);
        float l = Mathf.Max(0.05f, b.size.z + footprintPadding);
        currentArea = w * l;

        var shape = fogParticles.shape;
        shape.scale = new Vector3(w, fogHeight, l);

        fogTransform.rotation = Quaternion.identity;
        fogTransform.position = new Vector3(b.center.x, b.max.y + baseHeightOffset + fogHeight * 0.5f, b.center.z);

        lastPos = pot.transform.position;
        lastRot = pot.transform.rotation;
    }

    private void LateUpdate()
    {
        if (!initialized) return;

        // Follow the pot if it moved (Move tool), and re-measure now and then in case soil/size changed.
        if (pot.transform.position != lastPos || pot.transform.rotation != lastRot || Time.time >= nextRefresh)
        {
            RefreshShape();
            nextRefresh = Time.time + 1f;
        }
    }

    private void Update()
    {
        if (!initialized || pot == null) return;

        PlantState plant = pot.HasPlant ? pot.Plant : null;
        float target = plant != null ? plant.MiasmaInfluence01 : 0f;
        smoothedInfluence = Mathf.SmoothDamp(smoothedInfluence, target, ref influenceVelocity, smoothTime);

        bool shouldShow = smoothedInfluence > 0.01f;

        if (shouldShow && !fogParticles.isPlaying)
        {
            fogParticles.Play();
        }
        else if (!shouldShow && fogParticles.isPlaying)
        {
            fogParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return;
        }

        if (!shouldShow) return;

        float areaFactor = scaleEmissionWithArea ? currentArea / Mathf.Max(0.01f, referenceArea) : 1f;
        ParticleSystem.MinMaxCurve rate = emission.rateOverTime;
        rate.constant = maxEmissionRate * areaFactor * smoothedInfluence;
        emission.rateOverTime = rate;

        Color c = baseColor;
        c.a = baseColor.a * smoothedInfluence;
        main.startColor = c;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        PotContents p = pot != null ? pot : GetComponentInParent<PotContents>();
        if (p == null) return;
        Collider c = p.GetComponent<Collider>();
        if (c == null) return;
        Bounds b = c.bounds;
        Gizmos.color = new Color(0.8f, 0.3f, 0.9f, 0.5f);
        Gizmos.DrawWireCube(new Vector3(b.center.x, b.max.y + baseHeightOffset + fogHeight * 0.5f, b.center.z),
                            new Vector3(b.size.x + footprintPadding, fogHeight, b.size.z + footprintPadding));
    }
#endif
}
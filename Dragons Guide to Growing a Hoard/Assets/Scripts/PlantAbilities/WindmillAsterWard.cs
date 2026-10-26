using System.Collections.Generic;
using UnityEngine;

// =============================================================
// WindmillAsterWard.cs
// -------------------------------------------------------------
// Attach alongside PlantState (and LightSensor) on the Windmill
// Aster prefab. Only active while ITS OWN LightSensor reads direct
// sunlight (>= sunlightThreshold) — "This effect can only be active
// if put in direct sunlight." While active, every plant within
// wardRadius is registered as miasma-immune via PlantState's
// reference-counted immunity sources; the moment sunlight drops
// below threshold (shade, night, moved indoors) every warded
// neighbour is released again.
// The ward is a CONE: it only covers plants within wardRadius AND within
// coneAngle (total, degrees) of the direction the plant faces - so rotating
// the pot (C in placement mode) aims it. facingYawOffset corrects it if the
// model's "front" isn't its transform's forward (Z) axis.
// =============================================================
[RequireComponent(typeof(PlantState))]
public class WindmillAsterWard : MonoBehaviour
{
    [Range(0f, 1f)]
    [Tooltip("Own LightSensor.NormalisedIntensity must be at or above this to count as 'direct sunlight'.")]
    public float sunlightThreshold = 0.6f;

    public float wardRadius = 4f;

    [Range(10f, 360f)]
    [Tooltip("Total width of the cone in degrees (360 = full circle, like before).")]
    public float coneAngle = 120f;

    [Tooltip("Extra yaw added to the plant's facing (degrees). Use if the cone points sideways/backwards relative to the plant's front.")]
    public float facingYawOffset = 0f;

    public float rescanInterval = 1f;
    public LayerMask plantLayerMask = ~0;

    [Header("Range indicator (2D circle on the ground)")]
    [SerializeField] private bool showRangeCircle = true;
    [Tooltip("Colour and strength of the circle. Alpha is the rim's opacity.")]
    [SerializeField] private Color rangeColor = new Color(1f, 0.85f, 0.35f, 0.45f);
    [Tooltip("Lift above the plant's origin. Lower it (even negative) if the circle floats above the floor.")]
    [SerializeField] private float rangeGroundOffset = 0.03f;

    private RangeCircle rangeCircle;
    private LightSensor lightSensor;
    private readonly HashSet<PlantState> currentlyWarded = new HashSet<PlantState>();
    private float rescanTimer;

    private void Awake()
    {
        lightSensor = GetComponent<LightSensor>() ?? GetComponentInChildren<LightSensor>();

        if (showRangeCircle)
            rangeCircle = RangeCircle.Create(transform, rangeColor, wardRadius, rangeGroundOffset, coneAngle, facingYawOffset);
    }

    private void Update()
    {
        bool inDirectSunlight = lightSensor != null && lightSensor.NormalisedIntensity >= sunlightThreshold;

        if (rangeCircle != null)
        {
            rangeCircle.SetRadius(wardRadius);
            rangeCircle.SetAngle(coneAngle);
            rangeCircle.SetYawOffset(facingYawOffset);
            rangeCircle.SetVisible(inDirectSunlight); // the circle only shows while the ward is actually working
        }

        if (!inDirectSunlight)
        {
            ReleaseAll();
            return;
        }

        rescanTimer += GameClock.DeltaTime;
        if (rescanTimer < rescanInterval) return;
        rescanTimer = 0f;

        var found = new HashSet<PlantState>();
        foreach (Collider col in Physics.OverlapSphere(transform.position, wardRadius, plantLayerMask))
        {
            PlantState plant = col.GetComponent<PlantState>() ?? col.GetComponentInParent<PlantState>();
            if (plant != null && InCone(plant.transform.position)) found.Add(plant);
        }

        foreach (PlantState plant in currentlyWarded)
            if (plant != null && !found.Contains(plant))
                plant.RemoveMiasmaImmunitySource(this);

        foreach (PlantState plant in found)
            plant.AddMiasmaImmunitySource(this);

        currentlyWarded.Clear();
        currentlyWarded.UnionWith(found);
    }

    private Vector3 FacingFlat()
    {
        Vector3 f = Quaternion.Euler(0f, facingYawOffset, 0f) * transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }

    private bool InCone(Vector3 worldPos)
    {
        if (coneAngle >= 359.9f) return true;
        Vector3 to = worldPos - transform.position; to.y = 0f;
        if (to.sqrMagnitude < 0.25f) return true; // the Windmill itself / right beside it
        return Vector3.Angle(FacingFlat(), to) <= coneAngle * 0.5f;
    }

    private void ReleaseAll()
    {
        if (currentlyWarded.Count == 0) return;
        foreach (PlantState plant in currentlyWarded)
            if (plant != null) plant.RemoveMiasmaImmunitySource(this);
        currentlyWarded.Clear();
    }

    private void OnDisable()
    {
        ReleaseAll();
        if (rangeCircle != null) rangeCircle.SetVisible(false);
    }

    private void OnDestroy()
    {
        ReleaseAll();
        if (rangeCircle != null) Destroy(rangeCircle.gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        bool active = lightSensor != null && lightSensor.NormalisedIntensity >= sunlightThreshold;
        Gizmos.color = active ? new Color(0.6f, 0.9f, 1f, 0.5f) : new Color(0.5f, 0.5f, 0.5f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, wardRadius);
        Vector3 f = FacingFlat();
        foreach (float sgn in new[] { -1f, 1f })
            Gizmos.DrawLine(transform.position, transform.position + Quaternion.Euler(0f, sgn * coneAngle * 0.5f, 0f) * f * wardRadius);
    }
}

using UnityEngine;

// =============================================================
// PlantGrowthSize.cs
// -------------------------------------------------------------
// Gives a plant a SIZE that grows as it grows: scales it from Small Scale up to its authored size as
// PlantProgress fills (0 -> 1), and exposes Growth01 so abilities can scale with it
// (WhumprilLauncher: bigger = launches higher, ClovenwickWallMount: bigger = holds more weight).
//
// SETUP: put on the plant prefab root next to PlantProgress.
//   - Scale Target: what gets scaled (defaults to this object, so any colliders under it grow too).
//   - Small Scale: size at 0 progress as a fraction of the authored scale (0.4 = 40%). The scale you
//     authored on the prefab is the FULLY GROWN size.
//   - With no PlantProgress (e.g. a hand-placed plant) Growth01 is 1 = fully grown.
// =============================================================
public class PlantGrowthSize : MonoBehaviour
{
    [Tooltip("Auto-found on this object or a parent if left empty.")]
    [SerializeField] private PlantProgress progress;

    [Tooltip("The transform that gets scaled. Leave empty to scale this object (so colliders on it or under it " +
             "grow with the plant).")]
    [SerializeField] private Transform scaleTarget;

    [Tooltip("Size at 0 progress, as a fraction of the authored (fully grown) scale.")]
    [Range(0.05f, 1f)] [SerializeField] private float smallScale = 0.4f;

    [Tooltip("Maps progress (x, 0-1) to growth (y, 0-1). Linear by default.")]
    [SerializeField] private AnimationCurve growthCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Tooltip("How quickly the shown size eases toward the target (higher = snappier). 0 = instant.")]
    [SerializeField] private float scaleSmoothing = 4f;

    private Vector3 fullLocalScale = Vector3.one;
    private float shownGrowth;

    /// <summary>0 = just planted (smallest), 1 = fully grown. 1 when there's no PlantProgress at all.</summary>
    public float Growth01 => progress != null ? Mathf.Clamp01(growthCurve.Evaluate(progress.Progress01)) : 1f;

    /// <summary>The size actually being shown right now (eased), 0-1. Use this for effects that should match what the player sees.</summary>
    public float ShownGrowth01 => shownGrowth;

    /// <summary>
    /// Starts this plant at a given size (0-1, the same scale as Growth01) - used when a plant is picked up
    /// or re-planted at a saved size. Works by moving the plant's PlantProgress to the point where the
    /// growth curve gives that size, so it carries on growing from there. The visible size snaps straight
    /// to it instead of easing up from the smallest scale.
    /// </summary>
    public void SetGrowth01(float growth01)
    {
        if (progress == null) progress = GetComponentInParent<PlantProgress>();
        if (progress == null) return;

        float target = Mathf.Clamp01(growth01);

        // Invert the growth curve (assumed to rise from 0 to 1) by bisection, so a custom curve still
        // lands on the size that was asked for.
        float lo = 0f, hi = 1f;
        for (int i = 0; i < 24; i++)
        {
            float mid = (lo + hi) * 0.5f;
            if (growthCurve.Evaluate(mid) < target) lo = mid; else hi = mid;
        }

        progress.SetStartingProgress((lo + hi) * 0.5f);
        shownGrowth = Growth01;
        Apply();
    }

    private void Awake()
    {
        if (progress == null) progress = GetComponentInParent<PlantProgress>();
        if (scaleTarget == null) scaleTarget = transform;
        fullLocalScale = scaleTarget.localScale;
        shownGrowth = Growth01;
        Apply();
    }

    private void Update()
    {
        float target = Growth01;
        shownGrowth = scaleSmoothing <= 0f
            ? target
            : Mathf.MoveTowards(shownGrowth, target, scaleSmoothing * GameClock.DeltaTime);
        Apply();
    }

    private void Apply()
    {
        if (scaleTarget == null) return;
        scaleTarget.localScale = fullLocalScale * Mathf.Lerp(smallScale, 1f, shownGrowth);
    }
}

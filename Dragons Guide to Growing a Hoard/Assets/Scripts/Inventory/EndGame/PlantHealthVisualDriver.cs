using UnityEngine;

// =============================================================
// PlantHealthVisualDriver.cs
// -------------------------------------------------------------
// Drives a plant's visual "how alive does this look" state from its real
// health value (PlantState.HealthNormalized01 — 1 = fully healthy, 0 = fully
// dead, confirmed straight from PlantState.cs).
//
// Two mutually-exclusive modes, auto-detected per-plant so ONE script works
// across all 15 species regardless of which kind a given prefab is:
//
//   A) ANIMATED  — this prefab has the 36-second scrub clip. Assign
//      `animator` + `clipStateName` in the Inspector. 0s = healthy,
//      36s (clipLength) = dead, scrubbed via Animator.Play(state, 0,
//      normalizedTime) with speed frozen at 0 — this is a direct seek, not
//      a played-forward/backward animation, so revival scrubs smoothly
//      back toward 0s exactly like death scrubs toward 36s.
//
//   B) MATERIAL LERP — no animator assigned (texture-only species). Drives
//      the shader's exposed 0-1 blend property (see the Lerp shader: 0 =
//      healthy sample, 1 = dead sample) across every renderer in
//      `materialRenderers`, via MaterialPropertyBlock — same pattern as
//      DisintegrateEffect/PlantMiasmaFog, so no per-instance material copies.
//
// Either way the raw health value is smoothed first (SmoothDamp) so a
// sudden score change (soil swap, a watering tick) doesn't visibly snap the
// plant between poses/textures — it eases toward the new state instead.
//
// Attach this to the same GameObject as PlantState (or a child — it will
// find PlantState on itself or a parent).
// =============================================================
[RequireComponent(typeof(PlantState))]
public class PlantHealthVisualDriver : MonoBehaviour
{
    // ---------------------------------------------------------------
    // MODE A — Animated scrub (36s clip: 0s healthy -> 36s dead)
    // ---------------------------------------------------------------
    [Header("Mode A - Animated Scrub (leave Animator empty to use Mode B instead)")]
    [Tooltip("Assign only on species that actually have the health-cycle animation. Leave null " +
             "for texture-only plants and this component automatically falls back to Mode B.")]
    public Animator animator;

    [Tooltip("Name of the Animator state/clip to scrub (must be on layer 0).")]
    public string clipStateName = "PlantHealthCycle";

    [Tooltip("Length of the clip in seconds. 0s = healthy, this value = fully dead.")]
    public float clipLength = 36f;

    // ---------------------------------------------------------------
    // MODE B — Material Lerp (texture-only plants)
    // ---------------------------------------------------------------
    [Header("Mode B - Material Lerp (used when Animator above is left empty)")]
    [Tooltip("Every renderer that should have the healthy/dead blend applied. Usually just the " +
             "plant's mesh renderer(s) — leave empty and this auto-collects all renderers in " +
             "children the first time it runs.")]
    public Renderer[] materialRenderers;

    [Tooltip("The shader's exposed Vector1 blend property name. 0 = healthy sample, 1 = dead sample " +
             "(matches the Lerp shader node structure: Lerp(A=healthy, B=dead, T=this property)).")]
    public string blendPropertyName = "_DeathBlend";

    // ---------------------------------------------------------------
    // SMOOTHING
    // ---------------------------------------------------------------
    [Header("Smoothing")]
    [Tooltip("How long (seconds) it takes the visual to ease toward a new health value. 0 = snap instantly.")]
    public float smoothTime = 1.5f;

    // ---------------------------------------------------------------
    private PlantState plantState;
    private MaterialPropertyBlock mpb;
    private int blendPropertyId;

    private float displayedHealth01 = 1f;   // smoothed, what's actually shown right now
    private float smoothVelocity = 0f;      // SmoothDamp's internal velocity state

    private bool useAnimatedMode;
    private int clipStateHash;

    private void Awake()
    {
        plantState = GetComponent<PlantState>();
        if (plantState == null) plantState = GetComponentInParent<PlantState>();

        useAnimatedMode = animator != null;

        if (useAnimatedMode)
        {
            clipStateHash = Animator.StringToHash(clipStateName);
            // Freeze normal playback entirely — this component drives time directly every frame.
            animator.speed = 0f;
        }
        else
        {
            if (materialRenderers == null || materialRenderers.Length == 0)
                materialRenderers = GetComponentsInChildren<Renderer>();

            mpb = new MaterialPropertyBlock();
            blendPropertyId = Shader.PropertyToID(blendPropertyName);
        }

        // Start already at the plant's real current health so nothing eases in from a wrong
        // pose on spawn (relevant for replanted/harvest-node plants that come in already damaged
        // via PlantCondition — see PlantState.ApplyCondition).
        if (plantState != null)
            displayedHealth01 = plantState.HealthNormalized01;

        ApplyImmediate(displayedHealth01);
    }

    private void Update()
    {
        if (plantState == null) return;

        // A permanently-dead plant never recovers — lock the visual target at fully dead
        // regardless of any residual live score (mirrors PlantState.CalculateState()'s own
        // isPermanentlyDead short-circuit).
        float targetHealth01 = plantState.IsPermanentlyDead ? 0f : plantState.HealthNormalized01;

        displayedHealth01 = smoothTime > 0f
            ? Mathf.SmoothDamp(displayedHealth01, targetHealth01, ref smoothVelocity, smoothTime)
            : targetHealth01;

        Apply(displayedHealth01);
    }

    private void Apply(float health01)
    {
        if (useAnimatedMode)
        {
            float normalizedTime = Mathf.Clamp01(1f - health01); // 0 health -> end of clip (dead)
            animator.Play(clipStateHash, 0, normalizedTime);
            // speed stays 0 (set in Awake) — Play() here is a pure seek, not a resumed playback.
        }
        else
        {
            float blend = Mathf.Clamp01(1f - health01); // 0 = healthy, 1 = dead
            for (int i = 0; i < materialRenderers.Length; i++)
            {
                Renderer r = materialRenderers[i];
                if (r == null) continue;

                r.GetPropertyBlock(mpb);
                mpb.SetFloat(blendPropertyId, blend);
                r.SetPropertyBlock(mpb);
            }
        }
    }

    // Same as Apply() but skips smoothing entirely — used once on spawn so the plant doesn't
    // visibly ease in from the wrong pose for the first second or two.
    private void ApplyImmediate(float health01)
    {
        if (useAnimatedMode && animator != null)
        {
            animator.Update(0f); // forces the Animator to initialise before the first Play() seek
        }
        Apply(health01);
    }
}

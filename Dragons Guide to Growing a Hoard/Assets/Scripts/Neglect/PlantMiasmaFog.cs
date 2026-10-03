using UnityEngine;

// =============================================================
// PlantMiasmaFog.cs
// -------------------------------------------------------------
// A small, subtle particle fog around a plant while it's under miasma influence — deliberately
// minor, NOT the big dramatic gas-cloud look MiasmaController itself uses for the miasma zone as
// a whole. Intensity (emission rate + opacity) scales directly with PlantState.MiasmaInfluence01
// — the SAME value already driving the overhead miasma bar, so this always visually agrees with
// what that bar shows. Fully off (stopped and cleared, not just invisible) once influence drops
// back to 0 — "sits around the plant until it's cleared away."
//
// SETUP:
//   1. Attach to the plant prefab, alongside PlantState (auto-found via GetComponent if left empty).
//   2. Assign fogParticles — a small ParticleSystem child, pre-configured with your "minor fog"
//      look (soft, low-opacity, small radius) for what it should look like at FULL influence
//      (1.0). This script scales DOWN from that baseline as influence drops — you only need to
//      author the "maximum" look once, not separate low/high versions.
// =============================================================
[RequireComponent(typeof(PlantState))]
public class PlantMiasmaFog : MonoBehaviour
{
    [Tooltip("Auto-found via GetComponent if left empty.")]
    [SerializeField] private PlantState plantState;

    [Tooltip("The small fog particle system, pre-configured for what it should look like at FULL " +
             "(1.0) miasma influence — this script scales its emission rate and startColor alpha " +
             "down from that baseline as influence drops, and stops it outright at 0.")]
    [SerializeField] private ParticleSystem fogParticles;

    [Tooltip("Emission rate (particles/sec) at full (1.0) miasma influence. Deliberately modest — " +
             "this is meant to read as a light haze, not the miasma gas cloud itself.")]
    [SerializeField] private float maxEmissionRate = 4f;

    [Tooltip("How quickly the displayed intensity eases toward the real value, in seconds. Smooths " +
             "out MiasmaInfluence01 fluctuating frame to frame, rather than the fog visibly " +
             "flickering/popping in step with it.")]
    [SerializeField] private float smoothTime = 1.5f;

    private ParticleSystem.EmissionModule emission;
    private ParticleSystem.MainModule main;
    private Color baseColor;
    private float smoothedInfluence;
    private float influenceVelocity;
    private bool initialized;

    private void Awake()
    {
        if (plantState == null)
            plantState = GetComponent<PlantState>();

        if (fogParticles != null)
        {
            emission = fogParticles.emission;
            main = fogParticles.main;
            baseColor = main.startColor.color; // preserves whatever color/alpha was authored in the Editor
            initialized = true;

            // Starts fully stopped — Update() below only Plays it once influence actually rises
            // above 0, so a freshly-planted (unaffected) plant never shows so much as a single
            // particle rather than needing the emission rate to happen to reach 0 first.
            fogParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void Update()
    {
        if (!initialized || plantState == null) return;

        float target = plantState.MiasmaInfluence01;
        smoothedInfluence = Mathf.SmoothDamp(smoothedInfluence, target, ref influenceVelocity, smoothTime);

        bool shouldShow = smoothedInfluence > 0.01f;

        if (shouldShow && !fogParticles.isPlaying)
        {
            fogParticles.Play();
        }
        else if (!shouldShow && fogParticles.isPlaying)
        {
            // Fully cleared away, not just paused/faded — matches "sits around the plant until
            // it's cleared away" rather than leaving stray particles lingering after influence hits 0.
            fogParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return;
        }

        if (!shouldShow) return;

        ParticleSystem.MinMaxCurve rate = emission.rateOverTime;
        rate.constant = maxEmissionRate * smoothedInfluence;
        emission.rateOverTime = rate;

        Color c = baseColor;
        c.a = baseColor.a * smoothedInfluence;
        main.startColor = c;
    }
}

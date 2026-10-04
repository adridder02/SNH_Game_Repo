using UnityEngine;

// =============================================================
// RoomAmbientFogController.cs
// -------------------------------------------------------------
// Passive, ambient "box" fog for a room — quietly sits there, no fog at low miasma, some at
// medium, thickish at high. One of these per room ("a fair few rooms") is the intended setup —
// this is a self-contained, reusable component, not a single manager juggling every room, so it
// stays "synced and dynamic" per room automatically: each instance just reads ITS OWN room's live
// MiasmaController.CurrentSize/MaxSize every frame (same source of truth the miasma bar, the
// screen overlay, and PlantMiasmaFog all already read from), so it can never drift out of sync
// with what that room's miasma is actually doing, and updates continuously as it grows/shrinks —
// nothing here is a one-time snapshot.
//
// CONTINUOUS, not three discrete snap-states — "none / some / thick" are anchor points along one
// smooth curve (Mathf.InverseLerp between lowThreshold and highThreshold) rather than the fog
// popping between three fixed looks, which reads as a much quieter, more passive effect and
// avoids a jarring flip right at a threshold boundary.
//
// SETUP (per room):
//   1. Build a big, soft, room-filling "box" fog ParticleSystem sized/shaped to that specific
//      room (this script only ever controls intensity — emission rate + opacity — never shape or
//      size, so author the box shape/scale by hand per room same as you would for any other
//      static set-dressing). Pre-configure it for what it should look like at the THICKEST (1.0)
//      end of the curve; this script scales DOWN from that baseline.
//   2. Attach this component in that room, alongside (or near) that room's ZoneHealth — if
//      roomMiasma is left empty, it auto-pulls from a ZoneHealth on the same GameObject
//      (ZoneHealth.miasma already points at the right MiasmaController for that room).
//   3. Assign fogParticles.
// =============================================================
public class RoomAmbientFogController : MonoBehaviour
{
    [Tooltip("This room's MiasmaController. Auto-pulled from a ZoneHealth on this same GameObject " +
             "(ZoneHealth.miasma) if left empty — the usual setup, since a room's ZoneHealth already " +
             "links to its own MiasmaController.")]
    [SerializeField] private MiasmaController roomMiasma;

    [Tooltip("The room-filling ambient fog particle system, pre-configured for what it should look " +
             "like at the THICKEST (1.0) end of the curve — this script scales emission rate and " +
             "startColor alpha down from that baseline, all the way to fully stopped at 0.")]
    [SerializeField] private ParticleSystem fogParticles;

    [Header("Curve (fraction of this room's MiasmaController.MaxSize)")]
    [Tooltip("At or below this fraction, fog is fully off (0 intensity) — 'none' at low miasma.")]
    [Range(0f, 1f)] [SerializeField] private float lowThreshold = 0.15f;
    [Tooltip("At or above this fraction, fog is at full (1.0) intensity — 'thickish' at high miasma. " +
             "Between lowThreshold and this, intensity ramps smoothly — e.g. sitting right in the " +
             "middle of the two reads as the 'some fog at medium' case, without a hard snap.")]
    [Range(0f, 1f)] [SerializeField] private float highThreshold = 0.85f;

    [Tooltip("Emission rate (particles/sec) at full (1.0) intensity.")]
    [SerializeField] private float maxEmissionRate = 20f;

    [Tooltip("How quickly the displayed intensity eases toward the real value, in seconds. The " +
             "miasma itself already grows/shrinks gradually, so this is mostly just a light polish " +
             "against per-frame jitter rather than doing heavy lifting.")]
    [SerializeField] private float smoothTime = 2f;

    private ParticleSystem.EmissionModule emission;
    private ParticleSystem.MainModule main;
    private Color baseColor;
    private float smoothedIntensity;
    private float intensityVelocity;
    private bool initialized;

    private void Awake()
    {
        if (roomMiasma == null)
        {
            ZoneHealth zone = GetComponent<ZoneHealth>();
            if (zone != null) roomMiasma = zone.miasma;
        }

        if (fogParticles != null)
        {
            emission = fogParticles.emission;
            main = fogParticles.main;
            baseColor = main.startColor.color; // preserves whatever color/alpha was authored in the Editor
            initialized = true;

            // Starts fully stopped — Update() below only Plays it once intensity actually rises
            // above 0, so a room with low/no miasma never shows so much as a single particle.
            fogParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void Update()
    {
        if (!initialized || roomMiasma == null || roomMiasma.MaxSize <= 0f) return;

        float fraction = roomMiasma.CurrentSize / roomMiasma.MaxSize;
        float target = Mathf.InverseLerp(lowThreshold, highThreshold, fraction);

        smoothedIntensity = Mathf.SmoothDamp(smoothedIntensity, target, ref intensityVelocity, smoothTime);

        bool shouldShow = smoothedIntensity > 0.01f;

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

        ParticleSystem.MinMaxCurve rate = emission.rateOverTime;
        rate.constant = maxEmissionRate * smoothedIntensity;
        emission.rateOverTime = rate;

        Color c = baseColor;
        c.a = baseColor.a * smoothedIntensity;
        main.startColor = c;
    }
}

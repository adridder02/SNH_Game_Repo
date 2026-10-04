using UnityEngine;

// =============================================================
// ProximityTutorialTrigger.cs
// -------------------------------------------------------------
// Fires a tutorial external trigger the first time the player gets within
// `radius` of `target` (or of this GameObject itself, if target is left
// blank — the common case: just drop this component directly on/near the
// thing you want the player to walk up to, e.g. a placement grid).
//
// Stays completely inert until Arm() is called, so it can't fire from
// proximity before the relevant tutorial step is even showing — wire
// Arm() from that step's onStepShown (see TutorialStepData), and Disarm()
// from its onStepHidden if you want it to stop checking whenever the
// tutorial moves on some other way before the player ever gets close.
// Disarms itself automatically the instant it fires, so it only ever
// notifies once per arm.
//
// SETUP:
//   1. Put this on (or near) the thing the player needs to walk up to.
//   2. Leave `target` blank to use this object's own position, or assign
//      a specific Transform (e.g. a GreenhouseSurface) if the trigger
//      object isn't at the right spot itself.
//   3. Set `radius` and `externalTriggerId` (must match a TutorialStep's
//      External Trigger Id).
//   4. On the relevant TutorialStep, wire onStepShown -> this.Arm().
// =============================================================
public class ProximityTutorialTrigger : MonoBehaviour
{
    [Tooltip("Auto-found via the scene's PlayerController if left empty.")]
    [SerializeField] private Transform player;

    [Tooltip("The point to measure distance to. Leave blank to use this GameObject's own position — " +
             "the common case when this component sits directly on (or right next to) the thing the " +
             "player needs to walk up to.")]
    [SerializeField] private Transform target;

    [Tooltip("How close (world units) the player needs to get before this fires.")]
    [SerializeField] private float radius = 3f;

    [Tooltip("Fired on TutorialSequenceController.NotifyExternalTrigger the moment the player gets " +
             "within radius, but ONLY while armed — must match a TutorialStep's External Trigger Id " +
             "to actually advance anything.")]
    [SerializeField] private string externalTriggerId;

    private bool armed = false;

    private void Awake()
    {
        if (player == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }

        if (target == null)
            target = transform;
    }

    /// <summary>Starts checking proximity. Wire from a TutorialStep's onStepShown. No-op if already
    /// armed, or if it already fired once and hasn't been re-armed since.</summary>
    public void Arm() => armed = true;

    /// <summary>Stops checking without firing — wire from a TutorialStep's onStepHidden if the
    /// tutorial can move on some other way before the player ever gets close, so this doesn't keep
    /// checking (and potentially fire late) after that step is no longer relevant.</summary>
    public void Disarm() => armed = false;

    private void Update()
    {
        if (!armed || player == null || target == null) return;

        if (Vector3.Distance(player.position, target.position) <= radius)
        {
            armed = false; // fire once per Arm() call
            if (!string.IsNullOrEmpty(externalTriggerId))
                TutorialSequenceController.Instance?.NotifyExternalTrigger(externalTriggerId);
        }
    }
}

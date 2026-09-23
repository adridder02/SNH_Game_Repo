using UnityEngine;

// =============================================================
// MiasmaScreenEffectController.cs
// -------------------------------------------------------------
// Drives the three-image miasma warning overlay on the main HUD, plus the flight restrictions
// that come with it — all scoped to ONE specific zone (the center room with the World Tree) and
// ONLY while the player is actually standing/flying in that exact room right now
// (PlayerZoneTracker.CurrentZone). Leaving the room hides all three images and clears both
// restrictions immediately, regardless of how bad that room's miasma still is — these are about
// being IN the room, not a global miasma-severity indicator.
//
// THREE STAGES, by centerZoneMiasma.CurrentSize / MaxSize:
//   Early  (< middleThreshold)                  -> overlayImage1
//   Middle (middleThreshold .. finalThreshold)   -> overlayImage2, PlayerController.ForceGradualDescend = true
//   Final  (>= finalThreshold, "at its worst")   -> overlayImage3, PlayerController.FlightBlocked = true
// Both restrictions clear themselves automatically the moment the stage drops back down (or the
// player leaves the room) — see PlayerController's own ForceGradualDescend/FlightBlocked.
//
// SETUP:
//   1. Assign centerZoneMiasma — the center room's MiasmaController.
//   2. Assign centerZone — that SAME room's ZoneHealth (ZoneHealth.miasma already links the two
//      on that component, so these two fields should point at one matching pair).
//   3. playerZoneTracker / playerController auto-find via FindObjectOfType if left empty.
//   4. Build three full-screen Image overlays on the HUD canvas, start them inactive in the
//      scene, and assign overlayImage1/2/3.
// =============================================================
public class MiasmaScreenEffectController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The center room's (World Tree room) MiasmaController.")]
    [SerializeField] private MiasmaController centerZoneMiasma;

    [Tooltip("That SAME room's ZoneHealth — used to check whether the player is actually standing " +
             "in this specific room right now (PlayerZoneTracker.CurrentZone == this). " +
             "ZoneHealth.miasma already links the two on that component; these should be the same pairing.")]
    [SerializeField] private ZoneHealth centerZone;

    [Tooltip("Auto-found via FindObjectOfType if left empty.")]
    [SerializeField] private PlayerZoneTracker playerZoneTracker;

    [Tooltip("Auto-found via FindObjectOfType if left empty.")]
    [SerializeField] private PlayerController playerController;

    [Header("Overlay Images")]
    [Tooltip("Shown during the Early stage — start inactive in the scene.")]
    [SerializeField] private GameObject overlayImage1;
    [Tooltip("Shown during the Middle stage, alongside PlayerController.ForceGradualDescend.")]
    [SerializeField] private GameObject overlayImage2;
    [Tooltip("Shown during the Final stage, alongside PlayerController.FlightBlocked.")]
    [SerializeField] private GameObject overlayImage3;

    [Header("Thresholds (fraction of this zone's MiasmaController.MaxSize)")]
    [Range(0f, 1f)] [SerializeField] private float middleThreshold = 0.33f;
    [Range(0f, 1f)] [SerializeField] private float finalThreshold = 0.66f;

    private enum Stage { None, Early, Middle, Final }
    private Stage currentStage = Stage.None;

    private void Awake()
    {
        if (playerZoneTracker == null)
            playerZoneTracker = FindObjectOfType<PlayerZoneTracker>();
        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>();
    }

    private void Update()
    {
        bool inCenterZone = centerZone != null && playerZoneTracker != null &&
                             playerZoneTracker.CurrentZone == centerZone;

        Stage newStage = Stage.None;

        if (inCenterZone && centerZoneMiasma != null && centerZoneMiasma.MaxSize > 0f)
        {
            float fraction = centerZoneMiasma.CurrentSize / centerZoneMiasma.MaxSize;

            newStage = fraction >= finalThreshold ? Stage.Final
                : fraction >= middleThreshold ? Stage.Middle
                : Stage.Early;
        }

        if (newStage != currentStage)
            ApplyStage(newStage);
    }

    private void ApplyStage(Stage stage)
    {
        currentStage = stage;

        if (overlayImage1 != null) overlayImage1.SetActive(stage == Stage.Early);
        if (overlayImage2 != null) overlayImage2.SetActive(stage == Stage.Middle);
        if (overlayImage3 != null) overlayImage3.SetActive(stage == Stage.Final);

        if (playerController != null)
        {
            playerController.ForceGradualDescend = stage == Stage.Middle;
            playerController.FlightBlocked = stage == Stage.Final;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// =============================================================
// MiasmaScreenEffectController.cs
// -------------------------------------------------------------
// Drives the three-image miasma warning overlay on the main HUD, plus the flight restrictions
// that come with it — all scoped to ONE specific zone (the center room with the World Tree) and
// ONLY while the player is actually standing/flying in that exact room right now
// (PlayerZoneTracker.CurrentZone). Leaving the room hides everything and clears both restrictions
// immediately, regardless of how bad that room's miasma still is — these are about being IN the
// room, not a global miasma-severity indicator.
//
// THREE STAGES, by centerZoneMiasma.CurrentSize / MaxSize:
//   Early  (< middleThreshold)                  -> overlayImage1
//   Middle (middleThreshold .. finalThreshold)   -> overlayImage2, PlayerController.ForceGradualDescend = true
//   Final  (>= finalThreshold, "at its worst")   -> overlayImage3, PlayerController.FlightBlocked = true
// Both restrictions clear themselves automatically the moment the stage drops back down (or the
// player leaves the room) — see PlayerController's own ForceGradualDescend/FlightBlocked.
//
// TWO STATUS INDICATORS, separate from the three stage overlays above — these track the
// PlayerController flags THEMSELVES rather than the room's stage, so they're specifically "is
// this actually affecting the dragon right now" rather than "how bad is this room":
//   dragDownIndicator — shown exactly while PlayerController.ForceGradualDescend is true.
//   noFlyIndicator    — shown exactly while PlayerController.FlightBlocked is true.
// In practice these currently line up with Middle/Final respectively (that's what sets those
// flags in the first place), but are driven independently in case that ever changes.
//
// All three stage overlays fade in/out (FadingOverlay) rather than popping instantly, so the
// room's stage changing doesn't feel jarring. The two status indicators below pop instantly on
// purpose — no fade on those.
//
// SETUP:
//   1. Assign centerZoneMiasma — the center room's MiasmaController.
//   2. Assign centerZone — that SAME room's ZoneHealth (ZoneHealth.miasma already links the two
//      on that component, so these two fields should point at one matching pair).
//   3. playerZoneTracker / playerController auto-find via FindObjectOfType if left empty.
//   3b. OPTIONAL info popups: build a small panel (text + whatever art you like) for each status
//      indicator, start it inactive, and assign dragDownPopup / noFlyPopup. It pops up for
//      popupDuration seconds when that indicator appears, and stays up until the player clicks
//      somewhere else if they click the indicator themselves. The indicator's Image needs
//      Raycast Target ticked so it can be clicked.
//   4. Build five UI elements on the HUD canvas (three stage overlays + two status indicators),
//      each with a FadingOverlay component (auto-adds its own CanvasGroup), start them inactive
//      in the scene, and assign overlayImage1/2/3 + dragDownIndicator/noFlyIndicator.
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
    [Tooltip("Shown during the Early stage.")]
    [SerializeField] private FadingOverlay overlayImage1;
    [Tooltip("Shown during the Middle stage, alongside dragDownIndicator below.")]
    [SerializeField] private FadingOverlay overlayImage2;
    [Tooltip("Shown during the Final stage, alongside noFlyIndicator below.")]
    [SerializeField] private FadingOverlay overlayImage3;

    [Header("Status Indicators")]
    [Tooltip("Shown exactly while PlayerController.ForceGradualDescend is true — the dragon is " +
             "actively being pulled down by the miasma right now. Pops instantly (no fade), unlike " +
             "the overlay images above.")]
    [SerializeField] private GameObject dragDownIndicator;
    [Tooltip("Shown exactly while PlayerController.FlightBlocked is true — flight is blocked entirely. " +
             "Pops instantly (no fade), unlike the overlay images above.")]
    [SerializeField] private GameObject noFlyIndicator;

    [Header("Info Popups (optional)")]
    [Tooltip("Small info panel for the drag-down indicator. Start it INACTIVE. Pops up when the indicator " +
             "appears, or when the player clicks the indicator.")]
    [SerializeField] private GameObject dragDownPopup;
    [Tooltip("Small info panel for the no-fly indicator. Start it INACTIVE.")]
    [SerializeField] private GameObject noFlyPopup;
    [Tooltip("How many seconds the popup stays up when it appears on its own (the indicator just showed). " +
             "Real seconds - not affected by the game's time speed.")]
    [SerializeField] private float popupDuration = 5f;
    [Tooltip("Pop the info up by itself whenever an indicator appears. Off = only when the player clicks it.")]
    [SerializeField] private bool popUpWhenIndicatorAppears = true;

    [Header("Thresholds (fraction of this zone's MiasmaController.MaxSize)")]
    [Range(0f, 1f)] [SerializeField] private float middleThreshold = 0.33f;
    [Range(0f, 1f)] [SerializeField] private float finalThreshold = 0.66f;

    private enum Stage { None, Early, Middle, Final }
    private Stage currentStage = Stage.None;

    private enum PopupId { None, DragDown, NoFly }
    private PopupId openPopup = PopupId.None;
    private bool popupSticky;          // opened by a click: stays until the player clicks elsewhere
    private float popupTimer;
    private bool prevDragShown, prevNoFlyShown;
    private readonly List<RaycastResult> raycastHits = new List<RaycastResult>();

    private void Awake()
    {
        if (dragDownPopup != null) dragDownPopup.SetActive(false);
        if (noFlyPopup != null) noFlyPopup.SetActive(false);

        if (playerZoneTracker == null)
            playerZoneTracker = FindAnyObjectByType<PlayerZoneTracker>();
        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();
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

        UpdatePopups();
    }

    // ---------------------------------------------------------------
    // INFO POPUPS
    // ---------------------------------------------------------------
    private void UpdatePopups()
    {
        bool dragShown = dragDownIndicator != null && dragDownIndicator.activeInHierarchy;
        bool noFlyShown = noFlyIndicator != null && noFlyIndicator.activeInHierarchy;

        // The indicator just appeared - pop its info up for popupDuration seconds.
        if (popUpWhenIndicatorAppears)
        {
            if (dragShown && !prevDragShown) OpenPopup(PopupId.DragDown, false);
            if (noFlyShown && !prevNoFlyShown) OpenPopup(PopupId.NoFly, false);
        }

        // The indicator went away - its info has nothing to explain any more.
        if (!dragShown && openPopup == PopupId.DragDown) ClosePopup();
        if (!noFlyShown && openPopup == PopupId.NoFly) ClosePopup();

        prevDragShown = dragShown;
        prevNoFlyShown = noFlyShown;

        if (openPopup != PopupId.None && !popupSticky)
        {
            popupTimer -= Time.unscaledDeltaTime;
            if (popupTimer <= 0f) ClosePopup();
        }

        if (openPopup != PopupId.None || dragShown || noFlyShown)
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                HandleClick(Mouse.current.position.ReadValue());
        }
    }

    private void HandleClick(Vector2 screenPos)
    {
        if (EventSystem.current == null) return;

        PointerEventData ped = new PointerEventData(EventSystem.current) { position = screenPos };
        raycastHits.Clear();
        EventSystem.current.RaycastAll(ped, raycastHits);

        PopupId clickedIndicator = PopupId.None;
        bool clickedOpenPopup = false;

        foreach (RaycastResult hit in raycastHits)
        {
            GameObject go = hit.gameObject;
            if (go == null) continue;
            if (IsUnder(go, dragDownIndicator)) clickedIndicator = PopupId.DragDown;
            else if (IsUnder(go, noFlyIndicator)) clickedIndicator = PopupId.NoFly;
            if (openPopup == PopupId.DragDown && IsUnder(go, dragDownPopup)) clickedOpenPopup = true;
            if (openPopup == PopupId.NoFly && IsUnder(go, noFlyPopup)) clickedOpenPopup = true;
        }

        if (clickedIndicator != PopupId.None)
        {
            // Clicking an indicator opens its info and keeps it up; clicking it again closes it.
            if (openPopup == clickedIndicator && popupSticky) ClosePopup();
            else OpenPopup(clickedIndicator, true);
        }
        else if (!clickedOpenPopup && openPopup != PopupId.None)
        {
            ClosePopup(); // clicked back into the game / on something else
        }
    }

    private void OpenPopup(PopupId id, bool sticky)
    {
        GameObject popup = id == PopupId.DragDown ? dragDownPopup : noFlyPopup;
        if (popup == null) return;

        ClosePopup();
        openPopup = id;
        popupSticky = sticky;
        popupTimer = popupDuration;
        popup.SetActive(true);
    }

    private void ClosePopup()
    {
        if (dragDownPopup != null) dragDownPopup.SetActive(false);
        if (noFlyPopup != null) noFlyPopup.SetActive(false);
        openPopup = PopupId.None;
        popupSticky = false;
    }

    private static bool IsUnder(GameObject hit, GameObject root)
    {
        return root != null && hit.transform.IsChildOf(root.transform);
    }

    private void ApplyStage(Stage stage)
    {
        currentStage = stage;

        if (overlayImage1 != null) overlayImage1.SetVisible(stage == Stage.Early);
        if (overlayImage2 != null) overlayImage2.SetVisible(stage == Stage.Middle);
        if (overlayImage3 != null) overlayImage3.SetVisible(stage == Stage.Final);

        if (playerController != null)
        {
            playerController.ForceGradualDescend = stage == Stage.Middle;
            playerController.FlightBlocked = stage == Stage.Final;
        }

        // Driven from the same stage change here (rather than polled separately in Update) since
        // that's the only thing that ever changes these flags in the first place — see the class
        // comment for why they're still kept as their own concept instead of just reusing
        // overlayImage2/3 directly.
        if (dragDownIndicator != null) dragDownIndicator.SetActive(playerController != null && playerController.ForceGradualDescend);
        if (noFlyIndicator != null) noFlyIndicator.SetActive(playerController != null && playerController.FlightBlocked);
    }
}
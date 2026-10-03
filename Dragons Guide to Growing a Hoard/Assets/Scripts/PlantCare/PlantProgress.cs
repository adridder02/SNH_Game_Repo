using UnityEngine;

// =============================================================
// PlantProgress.cs
// -------------------------------------------------------------
// Attach to the same GameObject as PlantState (the plant prefab root).
//
// SEPARATE SYSTEM from PlantState's happiness/health tracking. PlantState
// decides whether a plant is Revived / Intermediate / Dead RIGHT NOW,
// based on its current soil/light/water score. PlantProgress instead
// tracks how that happiness has trended OVER TIME, and grows a 0-1
// "ripeness" meter:
//
//   • Sustained Revived      -> progress climbs toward 1 (Complete).
//   • Sustained Dead         -> progress drains toward 0, relatively fast.
//   • Sustained Intermediate -> progress ALSO drains toward 0, just slower
//                                than Dead (see intermediateDrainFraction).
//                                A plant that's merely "okay" for a long
//                                time still settles into the middle/low
//                                band instead of holding or growing.
//
// The instantaneous PlantStateEnum is smoothed (exponential moving
// average, time constant = smoothingSeconds) before it's allowed to
// push progress up or down. That's what keeps this from being overly
// sensitive — a single bad tick (e.g. water dipping for a few seconds)
// doesn't yank the bar around, only a sustained trend does.
//
// Once progress reaches 1.0 the plant is marked Complete and STAYS that
// way PERMANENTLY (progress simply stops updating) until it's harvested —
// see PotContents.HarvestPlant(), which destroys this component along
// with the rest of the plant GameObject.
//
// UI: PotMenuUIController reads Progress01 to drive the Main panel's
// progress bar (an ImageFillBar with a colour gradient — see
// ImageFillBar.cs) and reads IsComplete to swap the "Remove Plant"
// button over to "Harvest".
// =============================================================
public class PlantProgress : MonoBehaviour
{
    // ---------------------------------------------------------------
    // INSPECTOR — Growth / decay speed
    // ---------------------------------------------------------------
    [Header("Growth / Decay Speed")]
    [Tooltip("How many seconds of SUSTAINED Revived state it takes to fill the bar from empty to Complete.")]
    public float secondsToGrowFull = 120f;

    [Tooltip("How many seconds of SUSTAINED Dead state it takes to fully drain the bar from full to empty.")]
    public float secondsToDrainFromDead = 60f;

    [Range(0f, 1f)]
    [Tooltip("While Intermediate, progress drains at this fraction of the Dead drain speed — slower than " +
             "Dead, so time spent merely 'okay' still settles the plant into the middle/low band instead of " +
             "letting it hold steady or grow.")]
    public float intermediateDrainFraction = 0.35f;

    [Header("Smoothing")]
    [Tooltip("Time constant (seconds) used to smooth the happiness-state signal before it's allowed to move " +
             "progress. Higher = progress only reacts to trends sustained over a longer window and ignores " +
             "brief blips. Lower = more responsive/twitchy. This is what keeps the system from being overly " +
             "sensitive to momentary state flicker.")]
    public float smoothingSeconds = 5f;

    // ---------------------------------------------------------------
    // REFERENCES
    // ---------------------------------------------------------------
    [Header("References")]
    [Tooltip("Leave empty to auto-find PlantState on this GameObject.")]
    public PlantState plantState;

    // ---------------------------------------------------------------
    // RUNTIME
    // ---------------------------------------------------------------
    [Header("Runtime (read-only)")]
    [Range(0f, 1f)]
    [SerializeField] private float progress = 0f;
    [SerializeField] private bool isComplete = false;
    [SerializeField] private float smoothedRatePerSecond = 0f;

    // ---------------------------------------------------------------
    // PUBLIC ACCESSORS
    // ---------------------------------------------------------------
    /// <summary>0-1 growth/ripeness meter. Feed this straight into ImageFillBar.SetNormalized().</summary>
    public float Progress01 => progress;

    /// <summary>True once the plant has been sustained-Revived long enough to fully ripen. Locked permanently until harvest.</summary>
    public bool IsComplete => isComplete;

    // ---------------------------------------------------------------
    private void Awake()
    {
        if (plantState == null)
            plantState = GetComponent<PlantState>();

        if (plantState == null)
            Debug.LogWarning("[PlantProgress] No PlantState found on this GameObject — progress will never move.", this);
    }

    // ---------------------------------------------------------------
    private void Update()
    {
        if (isComplete || plantState == null) return;

        float instantRate = InstantRateFor(plantState.CurrentState);

        // Exponential smoothing: a brief state flicker barely nudges smoothedRatePerSecond,
        // only a trend sustained across ~smoothingSeconds actually moves it — and therefore
        // actually moves progress.
        float tau = Mathf.Max(0.01f, smoothingSeconds);
        float alpha = 1f - Mathf.Exp(-Time.deltaTime / tau);
        smoothedRatePerSecond = Mathf.Lerp(smoothedRatePerSecond, instantRate, alpha);

        progress = Mathf.Clamp01(progress + smoothedRatePerSecond * Time.deltaTime);

        if (progress >= 1f)
        {
            progress = 1f;
            isComplete = true;
            Debug.Log($"[PlantProgress] '{gameObject.name}' is fully grown — ready to harvest.");

            ReportGoldMilestone();
        }
    }

    // ---------------------------------------------------------------
    // JOURNAL — reports this species' gold/fully-ripened milestone the moment IsComplete first
    // flips true (i.e. before the plant is even harvested — see PlantJournalManager.MarkCompleted).
    // Only reached once per plant instance, since Update() bails out early at the top once
    // isComplete is true, so this can't double-fire for the same plant.
    // ---------------------------------------------------------------
    private void ReportGoldMilestone()
    {
        if (plantState == null) return;

        PlantSpeciesData species = plantState.journalSpecies;
        if (species == null)
        {
            Debug.LogWarning($"[PlantProgress] '{gameObject.name}' reached full growth but its PlantState " +
                              "has no journalSpecies assigned — the journal's gold milestone can't be recorded for it.", this);
            return;
        }

        if (PlantJournalManager.Instance != null)
        {
            bool wasNew = PlantJournalManager.Instance.MarkCompleted(species);
            Debug.Log($"[PlantProgress] Reported '{species.displayName}' as completed to the journal " +
                      $"(newly completed: {wasNew}).", this);
        }
        else
        {
            Debug.LogWarning($"[PlantProgress] '{gameObject.name}' reached full growth but " +
                              "PlantJournalManager.Instance is null — is a PlantJournalManager present " +
                              "and active somewhere in the scene? The gold milestone was NOT recorded.", this);
        }
    }

    // ---------------------------------------------------------------
    // Instantaneous (pre-smoothing) rate for each PlantState — progress
    // units per second. Positive grows toward Complete, negative drains
    // toward empty.
    // ---------------------------------------------------------------
    private float InstantRateFor(PlantStateEnum state)
    {
        float growRate = 1f / Mathf.Max(0.01f, secondsToGrowFull);
        float deadDrainRate = 1f / Mathf.Max(0.01f, secondsToDrainFromDead);

        switch (state)
        {
            case PlantStateEnum.Revived:
                return growRate;
            case PlantStateEnum.Intermediate:
                return -deadDrainRate * intermediateDrainFraction;
            default: // Dead
                return -deadDrainRate;
        }
    }

    /// <summary>
    /// Resets progress back to empty and clears Complete. Not called anywhere yet — here in case a future
    /// feature (e.g. replanting into the same slot, or a "prune and regrow" action) needs to restart growth.
    /// </summary>
    public void ResetProgress()
    {
        progress = 0f;
        isComplete = false;
        smoothedRatePerSecond = 0f;
    }

    /// <summary>
    /// Debug/testing helper — jumps straight to full progress and reports the gold milestone, the same
    /// way naturally reaching 1.0 in Update() would. Use this instead of just toggling the isComplete
    /// checkbox in the Inspector: that checkbox alone doesn't call ReportGoldMilestone(), so the journal
    /// would never hear about it. Right-click the component header (or the ⋮ menu) in Play Mode to fire it.
    /// </summary>
    [ContextMenu("Debug: Force Complete")]
    public void DebugForceComplete()
    {
        if (isComplete)
        {
            Debug.Log($"[PlantProgress] '{gameObject.name}' is already complete — nothing to force.", this);
            return;
        }

        progress = 1f;
        isComplete = true;
        Debug.Log($"[PlantProgress] '{gameObject.name}' force-completed for testing.", this);

        ReportGoldMilestone();
    }
}
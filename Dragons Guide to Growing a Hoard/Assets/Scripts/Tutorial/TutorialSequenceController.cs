using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =============================================================
// TutorialSequenceController.cs
// -------------------------------------------------------------
// Plays a hand-ordered list of TutorialStep entries one at a time —
// Portable steps show via TutorialPromptUI (bubble + outline
// pointing at a target), BottomBar steps show via
// TutorialBottomPopupUI (the static strip). Whichever widget isn't
// the current step's type stays hidden.
//
// ADVANCING TO THE NEXT STEP happens any combination of:
//   1. advanceOnClick — the player clicks the outlined target
//      (Portable) or the bar itself (BottomBar).
//   2. autoAdvanceAfterSeconds — times out on its own.
//   3. linkedMission/linkedTaskId (set on the step itself, in
//      TutorialStepData) — this controller listens to
//      MissionProgressManager.OnProgressChanged and auto-advances the
//      instant that specific task is marked complete, no click
//      needed. Gameplay code doesn't need to know the tutorial exists
//      at all — it just keeps calling CompleteTask(...) like it
//      already does for the Guide page, and both the corner banner
//      and this sequence react to the same event.
//   4. Gameplay code can still call CompleteCurrentStep() directly if
//      you want a step to advance off something that isn't a mission
//      task at all.
//
// Steps with no linkedMission set aren't touched by any of this —
// they're a separate, simpler sequence of your own choosing (this is
// what plays the raw "Use WASD to move" tips that have nothing to do
// with mission tasks).
//
// SETUP:
//   1. Put this on a persistent tutorial-UI object in the scene.
//   2. Assign promptUI / bottomPopupUI (the two view widgets).
//   3. Build the `steps` list in the Inspector, in playback order —
//      set each entry's type, message, and (for Portable) target +
//      offsets. That's the whole authoring surface; nothing else in
//      this file needs touching per-step.
//   4. Leave autoStart on to begin at step 0 on Start(), or drive it
//      manually (e.g. after a cutscene ends) with BeginSequence().
// =============================================================
public class TutorialSequenceController : MonoBehaviour
{
    [SerializeField] private TutorialPromptUI promptUI;
    [SerializeField] private TutorialBottomPopupUI bottomPopupUI;
    [Tooltip("Auto-found via MissionProgressManager.Instance if left empty. Only needed for steps that use " +
             "linkedMission/linkedTaskId — leave both this and those blank if your tutorial never ties into missions.")]
    [SerializeField] private MissionProgressManager progressManager;

    [Tooltip("Steps in playback order. Set each one's type/message/target here — this is the only place " +
             "you should need to author the tutorial flow.")]
    [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

    [SerializeField] private bool autoStart = true;

    /// <summary>Fired once, after the last step in the list advances.</summary>
    public event Action OnSequenceComplete;

    private int currentIndex = -1;
    private Coroutine autoAdvanceRoutine;

    public int CurrentIndex => currentIndex;
    public TutorialStep CurrentStep => (currentIndex >= 0 && currentIndex < steps.Count) ? steps[currentIndex] : null;

    void Awake()
    {
        if (progressManager == null)
            progressManager = MissionProgressManager.Instance != null
                ? MissionProgressManager.Instance
                : FindObjectOfType<MissionProgressManager>();

        if (promptUI != null) promptUI.OnAdvanceRequested += HandleAdvanceRequested;
        if (bottomPopupUI != null) bottomPopupUI.OnAdvanceRequested += HandleAdvanceRequested;
    }

    void OnEnable()
    {
        if (progressManager != null)
            progressManager.OnProgressChanged += HandleMissionProgressChanged;
    }

    void OnDisable()
    {
        if (progressManager != null)
            progressManager.OnProgressChanged -= HandleMissionProgressChanged;
    }

    void OnDestroy()
    {
        if (promptUI != null) promptUI.OnAdvanceRequested -= HandleAdvanceRequested;
        if (bottomPopupUI != null) bottomPopupUI.OnAdvanceRequested -= HandleAdvanceRequested;
    }

    void Start()
    {
        if (autoStart) BeginSequence();
    }

    // ------------------------------------------------------------
    // PUBLIC CONTROL
    // ------------------------------------------------------------
    public void BeginSequence()
    {
        currentIndex = -1;
        AdvanceToNextStep();
    }

    /// <summary>Call from gameplay code to finish whatever step is currently showing and move on,
    /// regardless of that step's advanceOnClick setting.</summary>
    public void CompleteCurrentStep()
    {
        if (currentIndex < 0 || currentIndex >= steps.Count) return; // sequence not running
        AdvanceToNextStep();
    }

    /// <summary>Jumps straight to a specific step, e.g. to resume a tutorial mid-way after a save load.</summary>
    public void SkipToStep(int index)
    {
        if (index < 0 || index >= steps.Count) return;
        currentIndex = index - 1;
        AdvanceToNextStep();
    }

    public void StopSequence()
    {
        StopAutoAdvanceTimer();
        promptUI?.Hide();
        bottomPopupUI?.Hide();
        currentIndex = -1;
    }

    // ------------------------------------------------------------
    // TIME-CRUNCH SHORTCUT — right-click this component's header in
    // the Inspector and pick this to drop the full hardcoded tutorial
    // script (see TutorialContent.cs) straight into `steps`, instead
    // of typing every row by hand. Still need to drag a target
    // Transform onto each Portable entry afterward.
    // ------------------------------------------------------------
    [ContextMenu("Load Hardcoded Tutorial Script (Plant Basics + Water Plant)")]
    private void LoadHardcodedTutorialScript()
    {
        steps = TutorialContent.BuildDefaultSteps();
        Debug.Log($"[TutorialSequenceController] Loaded {steps.Count} hardcoded steps. " +
                  "Now assign each Portable step's target Transform in the Inspector.");
    }

    // ------------------------------------------------------------
    // INTERNAL
    // ------------------------------------------------------------
    private void HandleAdvanceRequested() => AdvanceToNextStep();

    /// <summary>Fires on every mission progress change, not just ones relevant to the current step — cheap
    /// enough to just re-check the current step's link each time rather than filtering by mission/task first.</summary>
    private void HandleMissionProgressChanged() => CheckLinkedTaskComplete();

    /// <summary>If the current step is linked to a mission task and that task is already complete, advances
    /// immediately. Safe to call redundantly — does nothing when there's no link or the task isn't done yet.</summary>
    private void CheckLinkedTaskComplete()
    {
        TutorialStep step = CurrentStep;
        if (step == null || step.linkedMission == null || string.IsNullOrEmpty(step.linkedTaskId)) return;
        if (progressManager == null) return;

        if (progressManager.IsTaskComplete(step.linkedMission.ResolvedId, step.linkedTaskId))
            AdvanceToNextStep();
    }

    private void AdvanceToNextStep()
    {
        StopAutoAdvanceTimer();
        promptUI?.Hide();
        bottomPopupUI?.Hide();

        currentIndex++;

        if (currentIndex >= steps.Count)
        {
            OnSequenceComplete?.Invoke();
            return;
        }

        TutorialStep step = steps[currentIndex];
        if (step == null)
        {
            Debug.LogWarning($"[TutorialSequenceController] Step {currentIndex} is null — skipping.");
            AdvanceToNextStep();
            return;
        }

        switch (step.type)
        {
            case TutorialPromptType.Portable:
                if (promptUI == null)
                {
                    Debug.LogWarning("[TutorialSequenceController] Portable step but no promptUI assigned — skipping.");
                    AdvanceToNextStep();
                    return;
                }
                promptUI.Show(step);
                break;

            case TutorialPromptType.BottomBar:
                if (bottomPopupUI == null)
                {
                    Debug.LogWarning("[TutorialSequenceController] BottomBar step but no bottomPopupUI assigned — skipping.");
                    AdvanceToNextStep();
                    return;
                }
                bottomPopupUI.Show(step);
                break;
        }

        if (step.autoAdvanceAfterSeconds > 0f)
            autoAdvanceRoutine = StartCoroutine(AutoAdvanceAfter(step.autoAdvanceAfterSeconds));

        // Covers the case where the linked task was already completed before this step ever got shown
        // (e.g. the player did the thing slightly ahead of the tutorial catching up).
        CheckLinkedTaskComplete();
    }

    private IEnumerator AutoAdvanceAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        AdvanceToNextStep();
    }

    private void StopAutoAdvanceTimer()
    {
        if (autoAdvanceRoutine != null)
        {
            StopCoroutine(autoAdvanceRoutine);
            autoAdvanceRoutine = null;
        }
    }
}
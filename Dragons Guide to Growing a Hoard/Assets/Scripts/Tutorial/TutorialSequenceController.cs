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

    [Tooltip("Optional. Only used by the 'Load Hardcoded Tutorial Script' context menu action below — if " +
             "assigned, the six movement-tip BottomBar steps it generates are auto-linked to this mission's " +
             "tasks (WASD move / jump / double-space fly / tilt up / tilt down / land), so they advance the " +
             "instant PlayerController reports the action instead of waiting for a click. Leave blank to get " +
             "the old click-only behavior. This must be the SAME MissionData asset assigned to " +
             "PlayerController's 'Movement Mission' field, or nothing will auto-advance.")]
    [SerializeField] private MissionData movementMissionForHardcodedScript;

    [Tooltip("Optional. Only used by the 'Load Hardcoded Tutorial Script' context menu action below — if " +
             "assigned, the six find-node/pick-up/place-pot/water-plant/find-water/refill steps it generates " +
             "are auto-linked to this mission's tasks. This mission's task list must be ordered find_node, " +
             "plant_pickup, place_pot, water_plant, find_water, water_refill — CompleteOrderedTask only lets " +
             "a task complete when it's next in that order, so a mismatched order means a step here waits " +
             "forever. Must be the SAME MissionData asset assigned to CollectablePlant / " +
             "HarvestNodeContainer / PlacementSystem / PotInteraction / PlayerWaterSource.")]
    [SerializeField] private MissionData harvestMissionForHardcodedScript;

    /// <summary>Fired once, after the last step in the list advances.</summary>
    public event Action OnSequenceComplete;

    /// <summary>Auto-found via FindObjectOfType if a gameplay script needs to call NotifyExternalTrigger
    /// and doesn't already have a reference — set in Awake, same pattern as MissionProgressManager.Instance.</summary>
    public static TutorialSequenceController Instance { get; private set; }

    private int currentIndex = -1;
    private Coroutine autoAdvanceRoutine;

    public int CurrentIndex => currentIndex;
    public TutorialStep CurrentStep => (currentIndex >= 0 && currentIndex < steps.Count) ? steps[currentIndex] : null;

    void Awake()
    {
        if (Instance == null)
            Instance = this;

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

    /// <summary>Call this from GameInputModeManager whenever a menu (or placement mode) opens/closes —
    /// hides the bottom bar the instant a menu covers the screen, and brings it back afterward if the
    /// current step is still a BottomBar one. Doesn't touch Portable prompts or auto-advance timers,
    /// just the bottom strip's visibility.</summary>
    public void SetMenuOpen(bool open)
    {
        if (bottomPopupUI == null) return;

        if (open)
        {
            bottomPopupUI.Hide();
            return;
        }

        TutorialStep step = CurrentStep;
        if (step != null && step.type == TutorialPromptType.BottomBar)
            bottomPopupUI.Show(step);
    }

    /// <summary>Call this from any gameplay script when a real action happens that isn't tracked as a
    /// mission task (e.g. InventoryUIController calling NotifyExternalTrigger("inventory_opened") when
    /// the player actually presses [I]). Only advances if the CURRENT step's externalTriggerId matches —
    /// safe to call any time an action happens, even if no step (or a different one) is showing.</summary>
    public void NotifyExternalTrigger(string triggerId)
    {
        if (string.IsNullOrEmpty(triggerId)) return;

        TutorialStep step = CurrentStep;
        if (step != null && step.externalTriggerId == triggerId)
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
        steps = TutorialContent.BuildDefaultSteps(movementMissionForHardcodedScript, harvestMissionForHardcodedScript);
        Debug.Log($"[TutorialSequenceController] Loaded {steps.Count} hardcoded steps. " +
                  "Now assign each Portable step's target Transform in the Inspector." +
                  (movementMissionForHardcodedScript != null
                      ? " Movement steps are linked to " + movementMissionForHardcodedScript.name + "."
                      : " Movement Mission wasn't assigned, so those steps are click-only.") +
                  (harvestMissionForHardcodedScript != null
                      ? " Harvest/pot/water steps are linked to " + harvestMissionForHardcodedScript.name + "."
                      : " Harvest Mission wasn't assigned, so those steps are click-only."));
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

            case TutorialPromptType.Gate:
                // Deliberately shows nothing — promptUI/bottomPopupUI were already both Hide()'d above.
                // This step just sits here until CheckLinkedTaskComplete() below (or a future
                // OnProgressChanged tick) finds its linked task done and advances past it.
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
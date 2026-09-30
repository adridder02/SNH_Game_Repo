using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
    [Tooltip("The paginated image/title/description panel with Previous/Next buttons, used by Sidebar-" +
             "type steps. Leave blank if you don't use that type.")]
    [SerializeField] private TutorialSidebarUI sidebarUI;
    [Tooltip("Auto-found via MissionProgressManager.Instance if left empty. Only needed for steps that use " +
             "linkedMission/linkedTaskId — leave both this and those blank if your tutorial never ties into missions.")]
    [SerializeField] private MissionProgressManager progressManager;

    [Tooltip("Steps in playback order. Set each one's type/message/target here — this is the only place " +
             "you should need to author the tutorial flow.")]
    [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

    [SerializeField] private bool autoStart = true;

    [Header("Scene-Transition Gate (optional)")]
    [Tooltip("Exact scene name that, once loaded, fires sceneLoadTriggerId below via NotifyExternalTrigger " +
             "— the natural fit for a Gate step sitting between two halves of the tutorial (tutorial scene " +
             "-> main scene). Leave blank if nothing in this sequence needs this. Note this GameObject " +
             "needs to survive the tutorial-scene -> main-scene load for a Gate step to wait across that " +
             "transition at all — see the DontDestroyOnLoad call in Awake below. If your two halves instead " +
             "use separate UI references per scene (a different promptUI/portablePrompt set once you're in " +
             "the main scene), leave this whole section blank and drive that Gate step's advance some other " +
             "way (e.g. the main scene's own bootstrap script calling CompleteCurrentStep or " +
             "NotifyExternalTrigger once its own UI is ready).")]
    [SerializeField] private string sceneLoadTriggerSceneName;
    [Tooltip("The id fired when sceneLoadTriggerSceneName above finishes loading — must match the waiting " +
             "Gate step's External Trigger Id exactly.")]
    [SerializeField] private string sceneLoadTriggerId = "entered_main_scene";

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

    /// <summary>Tracks whether SetMenuUIMode() is currently active on the Sidebar type's behalf, so
    /// entering/leaving a run of Sidebar pages locks/unlocks the camera exactly once each way — see
    /// SyncSidebarCameraLock — instead of toggling on every single page turn within the same run.</summary>
    private bool sidebarCameraLockActive = false;

    public int CurrentIndex => currentIndex;
    public TutorialStep CurrentStep => (currentIndex >= 0 && currentIndex < steps.Count) ? steps[currentIndex] : null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // A second instance showed up (e.g. one already persisted from the tutorial scene, and the
            // main scene's own copy just loaded alongside it) — the persisted one is the one actually
            // running the sequence, so this newcomer has nothing to do.
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Needed for sceneLoadTriggerSceneName below to ever fire — without this, the whole object
        // (and its subscription to SceneManager.sceneLoaded just below) is destroyed the instant the
        // tutorial scene unloads, and a Gate step waiting on the main scene loading would wait forever.
        // Harmless to leave on even if you don't use the scene-transition gate at all.
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoadedForTutorial;

        if (progressManager == null)
            progressManager = MissionProgressManager.Instance != null
                ? MissionProgressManager.Instance
                : FindAnyObjectByType<MissionProgressManager>();

        if (promptUI != null) promptUI.OnAdvanceRequested += HandleAdvanceRequested;
        if (bottomPopupUI != null) bottomPopupUI.OnAdvanceRequested += HandleAdvanceRequested;
        if (sidebarUI != null)
        {
            sidebarUI.OnNextRequested += HandleAdvanceRequested;
            sidebarUI.OnPreviousRequested += GoToPreviousStep;
        }
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
        SceneManager.sceneLoaded -= OnSceneLoadedForTutorial;

        if (promptUI != null) promptUI.OnAdvanceRequested -= HandleAdvanceRequested;
        if (bottomPopupUI != null) bottomPopupUI.OnAdvanceRequested -= HandleAdvanceRequested;
        if (sidebarUI != null)
        {
            sidebarUI.OnNextRequested -= HandleAdvanceRequested;
            sidebarUI.OnPreviousRequested -= GoToPreviousStep;
        }
    }

    /// <summary>Fires sceneLoadTriggerId the moment sceneLoadTriggerSceneName finishes loading — the
    /// mechanism behind the scene-transition Gate step described in the header above. No-op if either
    /// field is blank, or if the scene that loaded isn't the one being waited on.</summary>
    private void OnSceneLoadedForTutorial(Scene scene, LoadSceneMode mode)
    {
        if (string.IsNullOrEmpty(sceneLoadTriggerSceneName) || string.IsNullOrEmpty(sceneLoadTriggerId))
            return;

        if (scene.name == sceneLoadTriggerSceneName)
            NotifyExternalTrigger(sceneLoadTriggerId);
    }

    void Start()
    {
        if (autoStart) BeginSequence();
    }

    void Update()
    {
        if (currentIndex < 0 || currentIndex >= steps.Count) return; // sequence not running
        if (Keyboard.current == null || !Keyboard.current.spaceKey.wasPressedThisFrame) return;

        TutorialStep step = CurrentStep;
        if (step == null) return;

        if (step.type == TutorialPromptType.Sidebar)
        {
            // Same as clicking Next — Next stays active even on the last page of a run (it just reads
            // "Complete" there, see TutorialSidebarUI), so this always has something to do while a
            // Sidebar step is current.
            AdvanceToNextStep();
            return;
        }

        // Any other step opts in individually via advanceOnSpacebar (e.g. a Portable prompt pointing
        // at the pot menu's health bar, which you'd rather not make the player click on directly).
        if (step.advanceOnSpacebar)
            AdvanceToNextStep();
    }

    /// <summary>True while the CURRENT step will react to the spacebar itself — a Sidebar page (Next/
    /// Complete) or any other step with advanceOnSpacebar on. Gameplay code that also binds Space (e.g.
    /// PlayerController's Jump) should check this and skip its own action so a single press doesn't
    /// both dismiss/advance the tutorial step AND do the gameplay thing at the same time.</summary>
    public bool IsConsumingSpacebar
    {
        get
        {
            TutorialStep step = CurrentStep;
            if (step == null) return false;
            return step.type == TutorialPromptType.Sidebar || step.advanceOnSpacebar;
        }
    }

    /// <summary>Whether the step at (index + direction) is itself a Sidebar step — used to decide
    /// whether THIS Sidebar step's Previous/Next button should be visible at all (direction -1/+1
    /// respectively). Adjacency is checked by TYPE, not just list bounds, so a Sidebar run correctly
    /// hides Previous/Next at either end even when other, non-Sidebar steps happen to sit right before
    /// or after it in the master `steps` list.</summary>
    private bool HasAdjacentSidebarStep(int index, int direction)
    {
        int neighborIndex = index + direction;
        if (neighborIndex < 0 || neighborIndex >= steps.Count) return false;

        TutorialStep neighbor = steps[neighborIndex];
        return neighbor != null && neighbor.type == TutorialPromptType.Sidebar;
    }

    /// <summary>Locks/unlocks the camera for a Sidebar step exactly like every other full-screen menu
    /// (Inventory/Journal/Pot Menu/Exit Menu all do this themselves via GameInputModeManager) — called
    /// whenever the CURRENT step changes, in both directions. Only actually calls SetMenuUIMode/
    /// SetGameplayMode on an actual ENTER/EXIT of a Sidebar run, not on every page turn within one (page
    /// turns stay Sidebar->Sidebar, so sidebarCameraLockActive is already true and this no-ops).</summary>
    private void SyncSidebarCameraLock(TutorialPromptType? currentType)
    {
        bool shouldLock = currentType == TutorialPromptType.Sidebar;
        if (shouldLock == sidebarCameraLockActive) return;

        sidebarCameraLockActive = shouldLock;
        if (shouldLock)
            GameInputModeManager.Instance?.SetMenuUIMode();
        else
            GameInputModeManager.Instance?.SetGameplayMode();
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

    /// <summary>True only if the step CURRENTLY SHOWING is linked to this exact mission task — i.e. the
    /// tutorial sequence has actually reached this point, not just that the task happens to be next in
    /// the mission's own ordering. CheckLinkedTaskComplete's own comment describes the normal, intended
    /// behavior for most linked steps: if the player does the real action slightly ahead of the tutorial
    /// UI catching up, the step just gets silently skipped the instant it becomes current, since the
    /// task is already done. Some actions (e.g. placing a pot) shouldn't get that pass — the player
    /// doing it early shouldn't bank the task at all, so the full prompt still shows later, exactly as
    /// if it hadn't happened yet. Gate a CompleteTask/CompleteOrderedTask call at the gameplay call site
    /// on this (only complete the task if this returns true, or if TutorialSequenceController.Instance
    /// is null) to get that stricter behavior for that one action specifically.</summary>
    public bool IsCurrentLinkedTask(MissionData mission, string taskId)
    {
        TutorialStep step = CurrentStep;
        return step != null && step.linkedMission == mission && step.linkedTaskId == taskId;
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
        sidebarUI?.Hide();
        SyncSidebarCameraLock(null); // force-unlock if a Sidebar run was cut short mid-sequence
        currentIndex = -1;
    }

    /// <summary>Steps backward one entry — what a Sidebar page's Previous button (or TutorialSidebarUI.
    /// OnPreviousRequested) calls. Unlike AdvanceToNextStep, this deliberately does NOT fire onStepShown/
    /// onStepHidden, touch any linked mission task, or restart an auto-advance timer — going back is just
    /// re-displaying an already-seen page, not a fresh state change. Does nothing on the first step.</summary>
    public void GoToPreviousStep()
    {
        if (currentIndex <= 0 || currentIndex >= steps.Count) return;

        StopAutoAdvanceTimer();
        promptUI?.Hide();
        bottomPopupUI?.Hide();
        sidebarUI?.Hide();

        currentIndex--;
        TutorialStep step = steps[currentIndex];
        if (step == null) return;

        SyncSidebarCameraLock(step.type);

        switch (step.type)
        {
            case TutorialPromptType.Portable:
                promptUI?.Show(step);
                break;
            case TutorialPromptType.BottomBar:
                bottomPopupUI?.Show(step);
                break;
            case TutorialPromptType.Sidebar:
                sidebarUI?.Show(step, HasAdjacentSidebarStep(currentIndex, -1), HasAdjacentSidebarStep(currentIndex, 1));
                break;
            case TutorialPromptType.Gate:
                break;
        }
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

        // Fire the step we're LEAVING's onStepHidden before switching away — same event regardless of
        // why we're leaving (click, timer, mission task, or external trigger), so anything turned on by
        // that step's onStepShown (a DirectionalIndicator target, say) has one reliable place to turn
        // back off. No-op on the very first call (CurrentStep is null before the sequence has begun).
        CurrentStep?.onStepHidden?.Invoke();

        promptUI?.Hide();
        bottomPopupUI?.Hide();
        sidebarUI?.Hide();

        currentIndex++;

        if (currentIndex >= steps.Count)
        {
            SyncSidebarCameraLock(null); // release the lock if the sequence ends mid-Sidebar-run
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

        SyncSidebarCameraLock(step.type);

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

            case TutorialPromptType.Sidebar:
                if (sidebarUI == null)
                {
                    Debug.LogWarning("[TutorialSequenceController] Sidebar step but no sidebarUI assigned — skipping.");
                    AdvanceToNextStep();
                    return;
                }
                sidebarUI.Show(step, HasAdjacentSidebarStep(currentIndex, -1), HasAdjacentSidebarStep(currentIndex, 1));
                break;

            case TutorialPromptType.Gate:
                // Deliberately shows nothing — promptUI/bottomPopupUI/sidebarUI were already all
                // Hide()'d above. This step just sits here until CheckLinkedTaskComplete() below (or a
                // future OnProgressChanged tick) finds its linked task done and advances past it.
                break;
        }

        // Fires for every type, including Gate — "shown" here means "became the current step", not
        // literally visible on screen. Side effects (revealing a HUD icon, arming a proximity check,
        // activating a DirectionalIndicator target) should happen the instant a step becomes current
        // regardless of whether it has its own visible UI.
        step.onStepShown?.Invoke();

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
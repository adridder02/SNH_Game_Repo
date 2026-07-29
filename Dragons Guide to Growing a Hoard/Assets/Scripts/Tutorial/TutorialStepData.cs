using UnityEngine;

// =============================================================
// TutorialStepData.cs
// -------------------------------------------------------------
// One entry in TutorialSequenceController's ordered `steps` list.
// This is the whole authoring surface for the tutorial flow — set
// the type, message, and (for Portable) portablePrompt here, in the
// order you want them to play. Nothing else needs touching per-step.
// =============================================================
public enum TutorialPromptType
{
    /// <summary>An individually hand-built prompt (outline + bubble + text — see TutorialPromptBox.cs), shown via TutorialPromptUI.</summary>
    Portable,
    /// <summary>Static strip along the bottom of the screen, no target — for movement/system tips. Shown via TutorialBottomPopupUI.</summary>
    BottomBar,
    /// <summary>Shows nothing at all. Exists purely to hold the sequence here — via linkedMission/linkedTaskId — until some
    /// task completes, before the NEXT step (a real Portable/BottomBar one) is allowed to appear. Use this when you want a
    /// step's bubble to stay hidden until an earlier task is done, rather than popping up the instant the previous step
    /// finishes. message/portablePrompt are ignored for this type.</summary>
    Gate
}

[System.Serializable]
public class TutorialStep
{
    [Tooltip("Which widget shows this step.")]
    public TutorialPromptType type = TutorialPromptType.Portable;

    [TextArea(1, 4)]
    [Tooltip("Required for BottomBar. Optional for Portable — only needed if you want to override the " +
             "text already typed into that prompt's own TutorialPromptBox; leave blank to just use " +
             "whatever's authored there.")]
    public string message;

    [Header("Portable only")]
    [Tooltip("The pre-built prompt for this step — its own outline box, bubble, and text, already " +
             "positioned and sized exactly where you want it in the Canvas (see TutorialPromptBox.cs). " +
             "Build one of these per Portable step and drag it in here. Leave blank for BottomBar steps.")]
    public TutorialPromptBox portablePrompt;

    [Header("Advance")]
    [Tooltip("Clicking the prompt (Portable) or the bar itself (BottomBar) closes this step and moves " +
             "to the next, in addition to any of the options below.")]
    public bool advanceOnClick = true;
    [Tooltip("0 = no auto-advance. Otherwise the step advances on its own after this many seconds, in addition to any click.")]
    public float autoAdvanceAfterSeconds = 0f;

    [Header("Auto-complete from a mission task (optional)")]
    [Tooltip("If assigned, this step advances automatically the moment MissionProgressManager reports the task " +
             "below as done — no click needed. This is what lets a step only advance once the player has " +
             "actually done the thing (picked up the plant, watered it, etc.), the same completion source of " +
             "truth the Guide page's checklist reads from. Leave blank for steps that aren't tied to a mission " +
             "task (movement tips, purely UI-pointer steps, etc.) — advanceOnClick / autoAdvanceAfterSeconds " +
             "still work fine on their own.")]
    public MissionData linkedMission;
    [Tooltip("Must match the task's Task Id — or its description, if Task Id was left blank on that " +
             "MissionData asset — i.e. whatever MissionTaskEntry.ResolvedId returns for the task you want this " +
             "step tied to. Typo-proofing this against the actual asset is on the TODO list; for now just copy " +
             "the text exactly as it appears on the MissionData asset.")]
    public string linkedTaskId;

    [Header("Auto-complete from a non-mission trigger (optional)")]
    [Tooltip("For real actions that aren't tracked as a mission task at all (e.g. just pressing [I] to open " +
             "the inventory) — set this to any id string of your choosing, then have that gameplay script call " +
             "TutorialSequenceController.Instance.NotifyExternalTrigger(sameId) when it happens. If this step " +
             "is the one currently showing, it advances immediately, same as a click. Leave blank if unused.")]
    public string externalTriggerId;
}
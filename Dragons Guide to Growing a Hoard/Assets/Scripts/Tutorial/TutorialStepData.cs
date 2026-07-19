using UnityEngine;

// =============================================================
// TutorialStepData.cs
// -------------------------------------------------------------
// One entry in TutorialSequenceController's ordered `steps` list.
// This is the whole authoring surface for the tutorial flow — set
// the type, message, and (for Portable) target/offsets here, in the
// order you want them to play. Nothing else needs touching per-step.
// =============================================================
public enum TutorialPromptType
{
    /// <summary>Bubble + outline pointing at a target (world object or UI element). Shown via TutorialPromptUI.</summary>
    Portable,
    /// <summary>Static strip along the bottom of the screen, no target — for movement/system tips. Shown via TutorialBottomPopupUI.</summary>
    BottomBar
}

[System.Serializable]
public class TutorialStep
{
    [Tooltip("Which widget shows this step.")]
    public TutorialPromptType type = TutorialPromptType.Portable;

    [TextArea(1, 4)]
    public string message;

    [Header("Portable only")]
    [Tooltip("What the outline box should surround / the bubble should point at. Leave blank for BottomBar steps.")]
    public Transform target;
    [Tooltip("Tick this if target is a 3D-world object (a plant, a pot, a water tile) — its screen position " +
             "is recalculated every frame via the main camera so the outline tracks it as the player moves. " +
             "Leave off for a UI element's RectTransform, whose screen position is already stable.")]
    public bool targetIsWorldSpace = true;
    [Tooltip("Bubble's offset from the target's projected screen position, in canvas pixels.")]
    public Vector2 bubbleOffset = new Vector2(0f, 160f);
    [Tooltip("Size of the outline box drawn around the target, in canvas pixels.")]
    public Vector2 outlineSize = new Vector2(300f, 220f);

    [Header("Advance")]
    [Tooltip("Clicking the outlined target (Portable) or the bar itself (BottomBar) closes this step and moves " +
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
}
using System.Collections.Generic;
using UnityEngine;

// =============================================================
// MissionData.cs
// -------------------------------------------------------------
// One asset per mission — a header/title plus an ordered list of
// tasks. This is the data-driven replacement for the hardcoded
// listOfInstructions[] string arrays (and the Toggle1..6-per-task
// wiring) that used to live directly inside Tutorial.cs / Tutorial_1.cs
// / Tutorial_2.cs.
//
// SETUP:
//   Right-click in the Project window ->
//   Create > Guide > Mission Data
//   Fill in missionTitle and add one MissionTaskEntry per task, in
//   the order gameplay scripts will complete them. Then either:
//     - assign it to a Tutorial_1 (or similar) script's `mission`
//       field so gameplay events call into MissionProgressManager, and/or
//     - add it to a MissionDatabase asset so it shows up as a
//       button on the Guide page's left-hand list.
// =============================================================
[CreateAssetMenu(fileName = "Mission_", menuName = "Guide/Mission Data")]
public class MissionData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique key used for save data / completion tracking. Leave blank to just use " +
             "the asset's file name. IMPORTANT: once players have save data, don't rename this (or " +
             "the asset, if left blank) — completion will look reset.")]
    [SerializeField] private string missionId;

    [Tooltip("Header text shown on the Guide page's left-side button and at the top of the right-side panel.")]
    public string missionTitle = "Mission";

    [Tooltip("Tasks in the order they should be completed/displayed. Each task's checkmark is driven by " +
             "MissionProgressManager, keyed off this mission's id + the task's own id.")]
    public List<MissionTaskEntry> tasks = new List<MissionTaskEntry>();

    /// <summary>The key actually used for save/progress data — falls back to the asset name if missionId is blank.</summary>
    public string ResolvedId => string.IsNullOrEmpty(missionId) ? name : missionId;
}

[System.Serializable]
public class MissionTaskEntry
{
    [Tooltip("Stable id for completion tracking. Leave blank to just use the description text itself " +
             "(fine to start with, but renaming the description later will reset that task's completion).")]
    [SerializeField] private string taskId;

    [TextArea(1, 3)]
    [Tooltip("The task text shown on the Guide page's right-side panel next to its checkmark.")]
    public string description;

    /// <summary>The key actually used for save/progress data — falls back to the description if taskId is blank.</summary>
    public string ResolvedId => string.IsNullOrEmpty(taskId) ? description : taskId;
}

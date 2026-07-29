using TMPro;
using UnityEngine;

// =============================================================
// TutorialObjectiveUI.cs
// -------------------------------------------------------------
// The small always-on banner in the top-right corner (icon + one
// line of text, e.g. "Find a place to pot the plant") that shows the
// CURRENT task for a mission — the first task in mission.tasks that
// MissionProgressManager hasn't marked complete yet. As soon as that
// task completes it swaps to the next one on its own; once every
// task is done the whole banner hides itself.
//
// This is read-only chrome, same spirit as GuideTaskRowUI — it never
// writes to MissionProgressManager. Gameplay code still calls
// CompleteTask(...) directly (same as it already does for the Guide
// page); this just reacts to OnProgressChanged.
//
// SETUP:
//   1. Put this on the corner panel's root GameObject (the one with
//      the "!" icon and the text label in the mockup).
//   2. Assign bannerRoot (the whole panel, for show/hide), iconRoot,
//      and objectiveLabel.
//   3. Assign `mission` to whichever MissionData is currently active
//      in this scene/level — e.g. the same asset LevelLoader is
//      watching for that level. If the player can move between
//      missions without leaving the scene, call SetMission(...) from
//      whatever script advances them instead of hardcoding one here.
// =============================================================
public class TutorialObjectiveUI : MonoBehaviour
{
    [Tooltip("Auto-found via MissionProgressManager.Instance if left empty.")]
    [SerializeField] private MissionProgressManager progressManager;
    [Tooltip("Mission whose next incomplete task this banner tracks. Swap at runtime with SetMission(...).")]
    [SerializeField] private MissionData mission;

    [Tooltip("Optional. If assigned, this banner stays hidden entirely until this mission is fully " +
             "complete — e.g. assign the movement mission here so the harvest checklist doesn't pop up " +
             "in the corner while the player's still in the middle of the WASD/jump/fly tutorial. Leave " +
             "blank to show this banner immediately, same as before.")]
    [SerializeField] private MissionData gateOnMissionComplete;

    [Tooltip("The whole banner — hidden entirely when there's no incomplete task to show. Falls back to " +
             "this GameObject if left blank.")]
    [SerializeField] private GameObject bannerRoot;
    [SerializeField] private GameObject iconRoot;
    [SerializeField] private TMP_Text objectiveLabel;

    void Awake()
    {
        if (progressManager == null)
            progressManager = MissionProgressManager.Instance != null
                ? MissionProgressManager.Instance
                : FindObjectOfType<MissionProgressManager>();
    }

    void OnEnable()
    {
        if (progressManager != null)
            progressManager.OnProgressChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (progressManager != null)
            progressManager.OnProgressChanged -= Refresh;
    }

    /// <summary>Point this banner at a different mission — e.g. call right before/after loading the next level.</summary>
    public void SetMission(MissionData newMission)
    {
        mission = newMission;
        Refresh();
    }

    private void Refresh()
    {
        if (gateOnMissionComplete != null &&
            (progressManager == null || !progressManager.IsMissionComplete(gateOnMissionComplete)))
        {
            if (bannerRoot != null) bannerRoot.SetActive(false);
            else gameObject.SetActive(false);
            return;
        }

        MissionTaskEntry nextTask = GetNextIncompleteTask();
        bool show = nextTask != null;

        if (bannerRoot != null) bannerRoot.SetActive(show);
        else gameObject.SetActive(show); // fallback if no separate banner root was assigned

        if (!show) return;

        if (objectiveLabel != null) objectiveLabel.text = nextTask.description;
        if (iconRoot != null) iconRoot.SetActive(true);
    }

    private MissionTaskEntry GetNextIncompleteTask()
    {
        if (mission == null || progressManager == null) return null;

        foreach (MissionTaskEntry task in mission.tasks)
            if (!progressManager.IsTaskComplete(mission, task))
                return task;

        return null; // every task done
    }
}
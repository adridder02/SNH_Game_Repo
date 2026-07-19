using System.Collections.Generic;
using TMPro;
using UnityEngine;

// =============================================================
// GuideUIController.cs
// -------------------------------------------------------------
// Drives the Journal's "Guide" page. Two sides, both built the same
// way the Plants page builds its species grid from slotTemplate:
//
//   LEFT  — missionButtonTemplate is one real button already sitting
//           in missionListContainer. Its RectTransform is the
//           reference for every cloned button's size + starting
//           position; clicking a button shows that mission on the
//           right.
//   RIGHT — taskRowTemplate is one real checklist row already sitting
//           in taskListContainer, same deal. Populated whenever a
//           mission button is clicked.
//
// Neither template needs its size or position touched in code ever
// again — move/resize it once in the Editor and every clone follows.
//
// This controller only manages the Guide page's own two lists. Which
// page is currently visible (Plants/Progress/Guide/Settings) is
// still whatever owns that nav bar (e.g. JournalUIController) —
// just SetActive this page's GameObject the same way the others are
// toggled; OnEnable here repopulates the left list every time.
//
// SETUP:
//   1. Build the left button and right row once in the Editor,
//      each with their UI component (GuideMissionButtonUI /
//      GuideTaskRowUI) attached, and both parented under a
//      top-left-anchored (anchor/pivot 0,1) container.
//   2. Drag those two into missionButtonTemplate / taskRowTemplate.
//   3. Drag missionListContainer / taskListContainer (their parents).
//   4. Assign a MissionDatabase asset.
//   5. Assign checklistPanel (the whole right side) — it starts
//      inactive, same as the Plants page's detail panel, until a
//      mission is clicked.
// =============================================================
public class GuideUIController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private MissionDatabase database;
    [Tooltip("Auto-found via MissionProgressManager.Instance if left empty.")]
    [SerializeField] private MissionProgressManager progressManager;

    [Header("Left Side — Mission List")]
    [Tooltip("Parent container for the mission buttons. Anchor/pivot must be top-left (0,1).")]
    [SerializeField] private RectTransform missionListContainer;
    [Tooltip("An existing button already placed in missionListContainer, with a GuideMissionButtonUI " +
             "component. Hidden at startup and cloned once per mission — its RectTransform is also the " +
             "size/position reference every cloned button uses.")]
    [SerializeField] private GuideMissionButtonUI missionButtonTemplate;
    [SerializeField] private float buttonGapY = 8f;

    [Header("Right Side — Checklist")]
    [Tooltip("The whole right-page contents. Inactive by default — there's no separate empty state, " +
             "this is just hidden until a mission is clicked.")]
    [SerializeField] private GameObject checklistPanel;
    [SerializeField] private TMP_Text missionTitleText;
    [Tooltip("Shows 'Complete' or 'Incomplete' for whichever mission is open. The 'Status' label itself " +
             "is static text baked into the panel — only this value text needs a reference.")]
    [SerializeField] private TMP_Text statusValueText;
    [Tooltip("Parent container for the checklist rows. Anchor/pivot must be top-left (0,1).")]
    [SerializeField] private RectTransform taskListContainer;
    [Tooltip("An existing checklist row already placed in taskListContainer, with a GuideTaskRowUI " +
             "component. Hidden at startup and cloned once per task.")]
    [SerializeField] private GuideTaskRowUI taskRowTemplate;
    [SerializeField] private float taskRowGapY = 8f;

    private RectTransform missionButtonTemplateRect;
    private RectTransform taskRowTemplateRect;

    private readonly List<GuideMissionButtonUI> spawnedButtons = new List<GuideMissionButtonUI>();
    private readonly List<GuideTaskRowUI> spawnedRows = new List<GuideTaskRowUI>();

    private MissionData selectedMission;
    private GuideMissionButtonUI selectedButton;

    void Awake()
    {
        if (progressManager == null)
            progressManager = MissionProgressManager.Instance != null
                ? MissionProgressManager.Instance
                : FindObjectOfType<MissionProgressManager>();

        if (missionButtonTemplate != null)
        {
            missionButtonTemplateRect = missionButtonTemplate.GetComponent<RectTransform>();
            missionButtonTemplate.gameObject.SetActive(false); // hide the live scene object used as a clone source
        }

        if (taskRowTemplate != null)
        {
            taskRowTemplateRect = taskRowTemplate.GetComponent<RectTransform>();
            taskRowTemplate.gameObject.SetActive(false);
        }

        if (checklistPanel != null)
            checklistPanel.SetActive(false);
    }

    void OnEnable()
    {
        if (progressManager != null)
            progressManager.OnProgressChanged += HandleProgressChanged;

        PopulateMissionList();
    }

    void OnDisable()
    {
        if (progressManager != null)
            progressManager.OnProgressChanged -= HandleProgressChanged;
    }

    private void HandleProgressChanged()
    {
        // Refresh whichever mission is currently open on the right, and the
        // left list's complete-indicators, without requiring a re-click.
        PopulateMissionList();
        if (selectedMission != null)
            ShowMissionDetail(selectedMission);
    }

    // ------------------------------------------------------------
    // LEFT — mission list
    // ------------------------------------------------------------
    private void PopulateMissionList()
    {
        if (database == null || missionButtonTemplate == null)
        {
            Debug.LogWarning("[GuideUIController] Missing database or mission button template — check the Inspector.");
            return;
        }

        foreach (GuideMissionButtonUI old in spawnedButtons)
            if (old != null) Destroy(old.gameObject);
        spawnedButtons.Clear();

        for (int i = 0; i < database.missions.Count; i++)
        {
            MissionData mission = database.missions[i];
            if (mission == null) continue;

            GuideMissionButtonUI button = Instantiate(missionButtonTemplate, missionListContainer);
            button.gameObject.SetActive(true);

            RectTransform buttonRect = button.GetComponent<RectTransform>();
            buttonRect.anchoredPosition = GuideVerticalLayout.GetPosition(missionButtonTemplateRect, i, buttonGapY);

            bool isComplete = progressManager != null && progressManager.IsMissionComplete(mission);
            button.Initialize(mission, isComplete, m => OnMissionButtonClicked(m, button));

            // Re-apply the persistent highlight if this clone happens to be the mission
            // that's currently open on the right (e.g. after a progress-triggered refresh).
            if (selectedMission != null && mission.ResolvedId == selectedMission.ResolvedId)
            {
                button.SetSelected(true);
                selectedButton = button;
            }

            spawnedButtons.Add(button);
        }
    }

    // ------------------------------------------------------------
    // RIGHT — checklist for whichever mission was clicked
    // ------------------------------------------------------------
    private void OnMissionButtonClicked(MissionData mission, GuideMissionButtonUI button)
    {
        // Clear the previous button's highlight — stays applied otherwise, independent
        // of Unity's transient EventSystem Selected state, so it doesn't fall away just
        // because focus moves to a task row, a dropdown, or empty space.
        if (selectedButton != null)
            selectedButton.SetSelected(false);

        selectedButton = button;
        selectedButton?.SetSelected(true);

        ShowMissionDetail(mission);
    }

    public void ShowMissionDetail(MissionData mission)
    {
        if (mission == null) return;
        selectedMission = mission;

        if (checklistPanel != null) checklistPanel.SetActive(true);
        if (missionTitleText != null) missionTitleText.text = mission.missionTitle;

        if (statusValueText != null)
        {
            bool complete = progressManager != null && progressManager.IsMissionComplete(mission);
            statusValueText.text = complete ? "Complete" : "Incomplete";
        }

        foreach (GuideTaskRowUI old in spawnedRows)
            if (old != null) Destroy(old.gameObject);
        spawnedRows.Clear();

        if (taskRowTemplate == null) return;

        for (int i = 0; i < mission.tasks.Count; i++)
        {
            MissionTaskEntry task = mission.tasks[i];

            GuideTaskRowUI row = Instantiate(taskRowTemplate, taskListContainer);
            row.gameObject.SetActive(true);

            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchoredPosition = GuideVerticalLayout.GetPosition(taskRowTemplateRect, i, taskRowGapY);

            bool complete = progressManager != null && progressManager.IsTaskComplete(mission, task);
            row.SetState(task.description, complete);

            spawnedRows.Add(row);
        }
    }

    /// <summary>Hides the right-side checklist, e.g. when the Guide page itself closes.</summary>
    public void ClearSelection()
    {
        selectedMission = null;
        if (selectedButton != null)
            selectedButton.SetSelected(false);
        selectedButton = null;
        if (checklistPanel != null) checklistPanel.SetActive(false);
    }
}

// =============================================================
// GuideVerticalLayout
// -------------------------------------------------------------
// Same idea as the Plants page's ManualSlotLayout, but for a single
// top-to-bottom column instead of a wrapping grid — every item uses
// the template's own size, stepping down by that height + a gap per
// index. Template RectTransform must be anchored/pivoted top-left (0,1).
// =============================================================
public static class GuideVerticalLayout
{
    public static Vector2 GetPosition(RectTransform template, int index, float gapY)
    {
        if (template == null) return Vector2.zero;

        float rowHeight = template.rect.height + gapY;
        return template.anchoredPosition + new Vector2(0f, -rowHeight * index);
    }
}
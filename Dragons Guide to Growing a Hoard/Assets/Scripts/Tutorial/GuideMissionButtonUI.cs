using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =============================================================
// GuideMissionButtonUI.cs
// -------------------------------------------------------------
// One button in the Guide page's left-hand list. Put this on the
// single button you've already placed in the scene (the "first
// item" GuideUIController uses as its layout/style reference) —
// it gets hidden and cloned once per mission at runtime.
// =============================================================
[RequireComponent(typeof(RectTransform))]
public class GuideMissionButtonUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleLabel;

    [Tooltip("Text color while this mission's page is open — stays applied until a different mission " +
             "is clicked, independent of Unity's transient EventSystem 'Selected' state (same trick as " +
             "JournalUIController's top-level nav buttons).")]
    [SerializeField] private Color selectedTextColor = Color.white;

    [Tooltip("Optional. If assigned, shown/hidden based on whether every task in this mission is complete " +
             "— e.g. a small badge or dot next to the title. Leave blank if you don't want this.")]
    [SerializeField] private GameObject completeIndicator;

    private MissionData mission;
    private Color normalTextColor;
    private bool normalColorCached = false;

    public void Initialize(MissionData mission, bool isComplete, Action<MissionData> onClicked)
    {
        this.mission = mission;

        if (titleLabel != null)
        {
            if (!normalColorCached)
            {
                normalTextColor = titleLabel.color;
                normalColorCached = true;
            }
            titleLabel.text = mission.missionTitle;
            titleLabel.color = normalTextColor;
        }

        if (completeIndicator != null)
            completeIndicator.SetActive(isComplete);

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClicked?.Invoke(this.mission));
        }
    }

    /// <summary>Applies (or clears) the persistent "this mission's page is open" look. Stays applied
    /// until explicitly cleared — not tied to Unity's transient EventSystem Selected state.</summary>
    public void SetSelected(bool selected)
    {
        if (titleLabel == null) return;
        titleLabel.color = selected ? selectedTextColor : normalTextColor;
    }
}
using TMPro;
using UnityEngine;

// =============================================================
// GuideTaskRowUI.cs
// -------------------------------------------------------------
// One row in the Guide page's right-hand checklist. Put this on
// the single row you've already placed in the scene (the "first
// item" GuideUIController uses as its layout/style reference) —
// it gets hidden and cloned once per task at runtime.
//
// The checkmark is a plain GameObject (an Image, most likely) that
// gets SetActive(true/false) — this is intentionally NOT a Toggle.
// Nothing here is interactable; the player never clicks a checkbox,
// gameplay just reports completion through MissionProgressManager
// and this row reflects it.
// =============================================================
[RequireComponent(typeof(RectTransform))]
public class GuideTaskRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text descriptionLabel;
    [Tooltip("The checkmark graphic (an Image GameObject, not a Toggle). Shown when the task is complete, hidden otherwise.")]
    [SerializeField] private GameObject checkmark;

    public void SetState(string description, bool complete)
    {
        if (descriptionLabel != null)
            descriptionLabel.text = description;

        if (checkmark != null)
            checkmark.SetActive(complete);
    }
}

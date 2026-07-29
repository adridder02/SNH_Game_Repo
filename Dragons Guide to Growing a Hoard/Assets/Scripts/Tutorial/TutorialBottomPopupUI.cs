using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =============================================================
// TutorialBottomPopupUI.cs
// -------------------------------------------------------------
// The static strip along the bottom of the screen — used for
// system/movement tips ("Use WASD and your mouse to move",
// "Press the spacebar to jump", etc.) that don't point at anything,
// unlike the portable bubble+outline prompt. Same one-step-at-a-time
// relationship with TutorialSequenceController as TutorialPromptUI:
// this class only displays whatever step it's given and reports back
// when clicked.
//
// SETUP:
//   1. Build the bottom bar/panel once in the Editor.
//   2. Assign panelRoot (whatever should show/hide as a whole) and
//      messageLabel.
//   3. Assign dismissButton — can be a full-width invisible Button
//      over the whole bar so clicking anywhere dismisses it, or a
//      small "Got it" button in the corner, your call.
// =============================================================
public class TutorialBottomPopupUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text messageLabel;
    [Tooltip("Fires OnAdvanceRequested when clicked, if the current step's advanceOnClick is true.")]
    [SerializeField] private Button dismissButton;

    /// <summary>Fired when the player clicks/dismisses this popup while the current step allows it.</summary>
    public event Action OnAdvanceRequested;

    void Awake()
    {
        if (dismissButton != null)
            dismissButton.onClick.AddListener(() => OnAdvanceRequested?.Invoke());

        Hide();
    }

    public void Show(TutorialStep step)
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        if (messageLabel != null) messageLabel.text = step.message;
        if (dismissButton != null) dismissButton.interactable = step.advanceOnClick;
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }
}
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =============================================================
// TutorialPromptBox.cs
// -------------------------------------------------------------
// One individually-authored Portable prompt — its own outline box,
// bubble, and text, built and positioned by hand as a child of
// TutorialPromptUI. Nothing here gets moved, resized, or projected
// from a world-space target at runtime — you place it exactly where
// you want it in the Editor and it just shows/hides on cue.
//
// SETUP (build one of these per Portable TutorialStep):
//   1. Create an empty GameObject under TutorialPromptUI's Canvas,
//      sized/positioned wherever this prompt should appear.
//   2. Build your outline box + bubble + text as children of it,
//      however you like — this script doesn't care about their
//      layout, only about the two references below.
//   3. Add this component to the root of that GameObject.
//   4. Assign messageLabel (optional — only needed if you want a
//      step's `message` to be able to override the text you've
//      already typed in here) and clickCatcher (optional — only
//      needed if this prompt should be click-to-advance).
//   5. Drag this GameObject into the matching TutorialStep's
//      "Portable Prompt" field in TutorialSequenceController's
//      steps list.
//
// This starts hidden (Awake deactivates it) — TutorialPromptUI is
// what shows/hides it, based on which step is current.
// =============================================================
public class TutorialPromptBox : MonoBehaviour
{
    [Tooltip("Optional. If the TutorialStep pointing at this prompt has non-empty message text, it's " +
             "applied here on Show(). Leave unassigned (or leave the step's message blank) to just use " +
             "whatever text you've already typed directly into this prompt in the Editor.")]
    [SerializeField] private TMP_Text messageLabel;

    [Tooltip("Optional. Covers this prompt so clicking it can advance the tutorial when the step's " +
             "advanceOnClick is true. Leave blank if this prompt should never be click-advanced (e.g. " +
             "it's meant to auto-advance off a linked mission task instead).")]
    [SerializeField] private Button clickCatcher;

    /// <summary>Fired when the player clicks this prompt while its step allows it.</summary>
    public event Action OnAdvanceRequested;

    private void Awake()
    {
        if (clickCatcher != null)
            clickCatcher.onClick.AddListener(() => OnAdvanceRequested?.Invoke());

        gameObject.SetActive(false); // hidden until TutorialPromptUI shows it for its step
    }

    public void Show(string overrideMessage, bool advanceOnClick)
    {
        if (!string.IsNullOrEmpty(overrideMessage) && messageLabel != null)
            messageLabel.text = overrideMessage;

        if (clickCatcher != null)
            clickCatcher.interactable = advanceOnClick;

        gameObject.SetActive(true);
    }

    public void Hide() => gameObject.SetActive(false);
}

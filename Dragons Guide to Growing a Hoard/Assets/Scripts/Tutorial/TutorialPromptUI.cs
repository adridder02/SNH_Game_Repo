using System;
using UnityEngine;

// =============================================================
// TutorialPromptUI.cs
// -------------------------------------------------------------
// The parent for every individually-authored Portable prompt (see
// TutorialPromptBox.cs). TutorialSequenceController owns the ordered
// list of steps and just calls Show(step)/Hide() here; this class
// doesn't build, move, resize, or reposition anything itself
// anymore — each step already points at a fully hand-built prompt
// box, so this is just traffic control: show whichever box the
// current step points at, hide whichever one was showing before.
//
// SETUP:
//   1. Put this on the parent GameObject that holds every
//      TutorialPromptBox as a child (see TutorialPromptBox.cs for
//      how to build each one).
//   2. Leave this GameObject active in the scene — the individual
//      boxes start hidden on their own (each TutorialPromptBox
//      hides itself in Awake), nothing to configure here.
//   3. Nothing else to assign — TutorialStep.portablePrompt is
//      where each step's box actually gets wired up.
// =============================================================
public class TutorialPromptUI : MonoBehaviour
{
    /// <summary>Fired when the player clicks the current prompt while its step allows it.</summary>
    public event Action OnAdvanceRequested;

    private TutorialPromptBox activeBox;

    public void Show(TutorialStep step)
    {
        HideActive();

        if (step.portablePrompt == null)
        {
            Debug.LogWarning("[TutorialPromptUI] Portable step has no Portable Prompt assigned — " +
                              "nothing to show. Build a TutorialPromptBox for it and drag it into the " +
                              "step's Portable Prompt field.");
            return;
        }

        activeBox = step.portablePrompt;
        activeBox.OnAdvanceRequested += HandleBoxAdvanceRequested;
        activeBox.Show(step.message, step.advanceOnClick);
    }

    public void Hide() => HideActive();

    private void HideActive()
    {
        if (activeBox == null) return;
        activeBox.OnAdvanceRequested -= HandleBoxAdvanceRequested;
        activeBox.Hide();
        activeBox = null;
    }

    private void HandleBoxAdvanceRequested() => OnAdvanceRequested?.Invoke();
}
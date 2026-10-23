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

    [Header("Follow a runtime target (optional)")]
    [Tooltip("Leave blank for a normal hand-placed prompt. If set (e.g. 'journal_plant' or 'inventory_consumable'), " +
             "this prompt's root snaps its position and size onto that UI element every frame while showing - " +
             "for things that only exist at runtime so you can't place a box on them by hand. Anchor this " +
             "object to a single point (not stretched). Put your outline on the root and the bubble/text as " +
             "children offset where you want them; they move with it. See TutorialTargets.cs for the keys.")]
    [SerializeField] private string followTargetKey;
    [Tooltip("Extra size added around the target (px) so the outline sits just outside it.")]
    [SerializeField] private Vector2 followPadding = new Vector2(12f, 12f);

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
        FollowTarget();
    }

    private void LateUpdate()
    {
        if (!string.IsNullOrEmpty(followTargetKey)) FollowTarget();
    }

    private void FollowTarget()
    {
        if (string.IsNullOrEmpty(followTargetKey)) return;
        RectTransform target = TutorialTargets.Get(followTargetKey);
        RectTransform self = transform as RectTransform;
        RectTransform parent = self != null ? self.parent as RectTransform : null;
        if (target == null || self == null || parent == null) return;

        var c = new Vector3[4];
        target.GetWorldCorners(c);
        Vector3 center = (c[0] + c[2]) * 0.5f;

        // size measured in the parent's local space so canvas scaling doesn't matter
        Vector3 min = parent.InverseTransformPoint(c[0]);
        Vector3 max = parent.InverseTransformPoint(c[2]);
        self.sizeDelta = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y)) + followPadding;

        // move so the rect's CENTRE (not its pivot) sits on the target's centre
        Vector3 pivotToCentre = self.TransformPoint(self.rect.center) - self.position;
        self.position = center - pivotToCentre;
    }

    public void Hide() => gameObject.SetActive(false);
}
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =============================================================
// TutorialPromptUI.cs
// -------------------------------------------------------------
// The "portable" prompt widget — an outline box around whatever the
// current TutorialStep points at, plus a speech bubble with the
// step's message. TutorialSequenceController owns the ordered list
// of steps and just calls Show(step)/Hide() here; this class only
// knows how to display ONE step and report back when it's clicked.
//
// If a step's target is a world-space object (a plant, a pot, a
// water tile) tick that step's targetIsWorldSpace box — this widget
// re-projects it through the camera every frame so the outline
// tracks the object as the player moves/turns. UI targets (an
// inventory icon, a nav button) don't need that — their
// RectTransform position is already screen space and stable, so
// it's only computed once in Show().
//
// SETUP:
//   1. Build the bubble (panel + TMP_Text) and the outline (an Image
//      with a border/frame sprite) once as children of the same
//      overlay Canvas as the rest of your HUD — this script only
//      moves and resizes them at runtime, it never touches their
//      appearance. Both should use a top-left-independent anchor
//      (e.g. center, 0.5/0.5) since anchoredPosition here is computed
//      relative to the canvas via RectTransformUtility.
//   2. Assign bubbleRect / messageLabel / outlineRect below.
//   3. Assign outlineClickCatcher — a Button on the outline (or an
//      invisible Image with Raycast Target on, sized to match) so
//      steps with advanceOnClick = true can be clicked to advance.
//   4. Leave this GameObject active in the scene; Awake() hides the
//      bubble/outline until the first Show() call.
// =============================================================
[RequireComponent(typeof(RectTransform))]
public class TutorialPromptUI : MonoBehaviour
{
    [Header("Bubble")]
    [SerializeField] private RectTransform bubbleRect;
    [SerializeField] private TMP_Text messageLabel;

    [Header("Outline")]
    [SerializeField] private RectTransform outlineRect;
    [Tooltip("Button covering the outline box. Its onClick fires OnAdvanceRequested when the current step's advanceOnClick is true.")]
    [SerializeField] private Button outlineClickCatcher;

    [Header("Projection")]
    [Tooltip("Overlay canvas' RectTransform this widget's children live under — used to convert world-space " +
             "targets into anchored positions. Auto-found via GetComponentInParent<Canvas>() if left empty.")]
    [SerializeField] private RectTransform canvasRect;
    [Tooltip("Falls back to Camera.main if left empty.")]
    [SerializeField] private Camera worldCamera;

    /// <summary>Fired when the player clicks the outlined target while the current step allows it.</summary>
    public event Action OnAdvanceRequested;

    private TutorialStep activeStep;
    private bool isShowing = false;

    void Awake()
    {
        if (canvasRect == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvasRect = canvas.GetComponent<RectTransform>();
        }
        if (worldCamera == null) worldCamera = Camera.main;

        if (outlineClickCatcher != null)
            outlineClickCatcher.onClick.AddListener(() => OnAdvanceRequested?.Invoke());

        SetVisible(false);
    }

    public void Show(TutorialStep step)
    {
        activeStep = step;
        isShowing = true;
        SetVisible(true);

        if (messageLabel != null) messageLabel.text = step.message;
        if (outlineRect != null) outlineRect.sizeDelta = step.outlineSize;
        if (outlineClickCatcher != null) outlineClickCatcher.interactable = step.advanceOnClick;

        RepositionToTarget(); // first-frame placement, don't wait for the next LateUpdate
    }

    public void Hide()
    {
        isShowing = false;
        activeStep = null;
        SetVisible(false);
    }

    void LateUpdate()
    {
        // Only pay the projection cost every frame for steps that actually need it
        // (world-space targets moving relative to the camera). UI targets are placed once in Show().
        if (isShowing && activeStep != null && activeStep.targetIsWorldSpace)
            RepositionToTarget();
    }

    private void RepositionToTarget()
    {
        if (activeStep == null || activeStep.target == null || canvasRect == null) return;

        Vector2 screenPoint;

        if (activeStep.targetIsWorldSpace)
        {
            Camera cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null) return;
            screenPoint = cam.WorldToScreenPoint(activeStep.target.position);
        }
        else
        {
            screenPoint = RectTransformUtility.WorldToScreenPoint(null, activeStep.target.position);
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 localPoint);

        if (outlineRect != null)
            outlineRect.anchoredPosition = localPoint;

        if (bubbleRect != null)
            bubbleRect.anchoredPosition = localPoint + activeStep.bubbleOffset;
    }

    private void SetVisible(bool visible)
    {
        if (bubbleRect != null) bubbleRect.gameObject.SetActive(visible);
        if (outlineRect != null) outlineRect.gameObject.SetActive(visible);
    }
}

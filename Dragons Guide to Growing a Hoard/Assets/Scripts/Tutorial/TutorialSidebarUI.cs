using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =============================================================
// TutorialSidebarUI.cs
// -------------------------------------------------------------
// A paginated info panel — image, title, description, plus Previous/
// Next buttons — for Sidebar-type TutorialSteps. Unlike the Portable
// prompt (points at a specific on-screen target) or the BottomBar
// strip (a single line of text), this is for a short run of full
// pages the player can flip back and forth through (think a little
// intro booklet). It reuses exactly the same TutorialStep/
// TutorialSequenceController machinery as everything else — author
// each page as its own Sidebar step (sidebarTitle/message/
// sidebarImage) back to back in the same `steps` list, nothing extra
// to build per page beyond that.
//
// Previous/Next hide themselves automatically at either end of a run
// (TutorialSequenceController's Show() call tells this whether
// there's a page before/after this one) — nothing to wire per page.
// Space also advances Next, same as clicking it — see
// TutorialSequenceController.Update().
//
// SETUP:
//   1. Build the panel once in the Editor — an Image for the picture,
//      a TMP_Text for the title, another for the description, and a
//      Previous/Next Button pair.
//   2. Assign every field below.
//   3. Assign this component to TutorialSequenceController's new
//      `sidebarUI` field.
//   4. Leave this GameObject active in the scene; it hides itself in
//      Awake and only appears while a Sidebar step is current.
// =============================================================
public class TutorialSidebarUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image pictureImage;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text descriptionLabel;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [Tooltip("The Next button's own label — its text switches to completeLabel on the last page of a " +
             "run (see Show below). Leave unassigned if the button's label never needs to change.")]
    [SerializeField] private TMP_Text nextButtonLabel;
    [SerializeField] private string nextLabel = "Next";
    [SerializeField] private string completeLabel = "Complete";

    /// <summary>Fired when Next is clicked. TutorialSequenceController treats this exactly like
    /// clicking a Portable/BottomBar prompt — advances to the next step in `steps`.</summary>
    public event Action OnNextRequested;

    /// <summary>Fired when Previous is clicked. Only ever raised while the button is actually visible
    /// (hidden entirely on the first page of a run — see Show below).</summary>
    public event Action OnPreviousRequested;

    void Awake()
    {
        if (nextButton != null)
            nextButton.onClick.AddListener(() => OnNextRequested?.Invoke());
        if (previousButton != null)
            previousButton.onClick.AddListener(() => OnPreviousRequested?.Invoke());

        Hide();
    }

    /// <param name="hasPrevious">Whether a page exists before this one — hides Previous entirely when false.</param>
    /// <param name="hasNext">Whether another Sidebar page exists after this one. Next itself stays
    /// visible either way (see below) — this only decides its label.</param>
    public void Show(TutorialStep step, bool hasPrevious, bool hasNext)
    {
        if (step == null) return;

        if (panelRoot != null) panelRoot.SetActive(true);

        if (pictureImage != null)
        {
            pictureImage.sprite = step.sidebarImage;
            pictureImage.enabled = step.sidebarImage != null;
        }

        if (titleLabel != null) titleLabel.text = step.sidebarTitle;
        if (descriptionLabel != null) descriptionLabel.text = step.message;

        // Hidden, not just disabled — a step with no earlier page shouldn't show a dead Previous button.
        if (previousButton != null) previousButton.gameObject.SetActive(hasPrevious);

        // Next stays visible on every page, including the last — it's how the player finishes the
        // booklet, it just reads "Complete" there instead of "Next" (see nextButtonLabel).
        if (nextButton != null) nextButton.gameObject.SetActive(true);
        if (nextButtonLabel != null) nextButtonLabel.text = hasNext ? nextLabel : completeLabel;
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }
}
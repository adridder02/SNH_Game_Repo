using UnityEngine;
using UnityEngine.UI;

// =============================================================
// TitleScreenController.cs
// -------------------------------------------------------------
// Put this anywhere in the Title scene. Assign startButton (or call
// StartTutorial() directly from a Button's onClick if you'd rather wire
// it that way instead).
// =============================================================
public class TitleScreenController : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button exitButton;

    [Tooltip("Exact scene name as it appears in File > Build Settings.")]
    [SerializeField] private string tutorialSceneName = "Tutorial";

    [Header("Transition (optional)")]
    [Tooltip("Same fade-out Animator LevelLoader uses (needs a 'Start' trigger parameter). Leave " +
             "empty to load instantly with no fade.")]
    [SerializeField] private Animator transitionAnimator;
    [SerializeField] private float transitionTime = 1f;

    private void Awake()
    {
        startButton?.onClick.AddListener(StartTutorial);
        exitButton?.onClick.AddListener(QuitGame);
    }

    /// <summary>Public so it can also be wired directly to a Button's onClick in the Inspector
    /// instead of (or in addition to) assigning startButton above.</summary>
    public void StartTutorial()
    {
        SceneTransitionUtility.LoadScene(this, tutorialSceneName, transitionAnimator, transitionTime);
    }

    /// <summary>Public so it can also be wired directly to a Button's onClick instead of (or in
    /// addition to) assigning exitButton above. Application.Quit() only does anything in a real
    /// build — it's a no-op in the Editor, so use the Editor block below to actually stop Play
    /// mode when testing.</summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
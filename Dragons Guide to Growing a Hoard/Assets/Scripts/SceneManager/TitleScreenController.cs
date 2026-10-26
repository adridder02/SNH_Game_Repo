using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// =============================================================
// TitleScreenController.cs
// -------------------------------------------------------------
// Put this anywhere in the Title scene.
//
//   startButton     = NEW GAME (same field as before, so your existing wiring keeps working). If a save
//                     exists, asks "overwrite?" through the scene's ConfirmationDialog first, then wipes the
//                     save + progress and loads the tutorial scene.
//   continueButton  = CONTINUE. Loads the saved game. Disabled (or hidden) when there's no save.
//   saveInfoLabel   = optional TMP text next to Continue: "Saved 25 Oct 2026, 14:03".
//
// The scene names live on the SaveRegistry asset (Resources/SaveRegistry). If that asset doesn't exist
// yet, New Game falls back to the old behaviour (loads tutorialSceneName below, no wipe).
// =============================================================
public class TitleScreenController : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button exitButton;

    [Header("Settings (optional)")]
    [Tooltip("Opens the settings panel below.")]
    [SerializeField] private Button settingsButton;
    [Tooltip("A panel containing a SettingsPageUI. Starts hidden; Escape or the close button hides it.")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button closeSettingsButton;

    [Tooltip("Optional. Shows when the save was made, or is cleared when there isn't one.")]
    [SerializeField] private TMP_Text saveInfoLabel;
    // (Removed: "hide Continue when no save". Continue is now always shown and just disabled without a save.)

    [Tooltip("Yes/No popup for 'overwrite your save?'. Auto-found in the scene if left empty; without one " +
             "New Game just goes ahead.")]
    [SerializeField] private ConfirmationDialog confirmDialog;

    [Header("Hide while a popup is open")]
    [Tooltip("Everything on the title screen that should disappear while the settings panel or the " +
             "confirmation popup is showing: the logo, the signpost menu, decorations. Do NOT put the " +
             "settings panel or the confirmation dialog in here.")]
    [SerializeField] private GameObject[] hideWhilePopupOpen;

    [Tooltip("Only used as a fallback when there's no SaveRegistry asset. Exact scene name from Build Settings.")]
    [SerializeField] private string tutorialSceneName = "Tutorial";

    [Header("Transition (optional)")]
    [Tooltip("Same fade-out Animator LevelLoader uses (needs a 'Start' trigger parameter). Leave " +
             "empty to load instantly with no fade.")]
    [SerializeField] private Animator transitionAnimator;
    [SerializeField] private float transitionTime = 1f;

    private void Awake()
    {
        startButton?.onClick.AddListener(StartTutorial);
        continueButton?.onClick.AddListener(ContinueGame);
        exitButton?.onClick.AddListener(QuitGame);
        settingsButton?.onClick.AddListener(OpenSettings);
        closeSettingsButton?.onClick.AddListener(CloseSettings);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (confirmDialog == null) confirmDialog = FindAnyObjectByType<ConfirmationDialog>();
    }

    private void OnEnable()
    {
        SaveManager.SaveStateChanged += RefreshContinue;
        RefreshContinue();
    }

    private void OnDisable() => SaveManager.SaveStateChanged -= RefreshContinue;

    private bool popupWasOpen;

    private void Update()
    {
        bool popupOpen = (settingsPanel != null && settingsPanel.activeSelf) ||
                         (confirmDialog != null && confirmDialog.IsShowing);
        if (popupOpen != popupWasOpen)
        {
            popupWasOpen = popupOpen;
            foreach (GameObject g in hideWhilePopupOpen)
                if (g != null) g.SetActive(!popupOpen);
        }

        if (settingsPanel != null && settingsPanel.activeSelf &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseSettings();
    }

    public void OpenSettings() { if (settingsPanel != null) settingsPanel.SetActive(true); }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        SettingsManager.Save();
    }

    private void RefreshContinue()
    {
        bool has = SaveManager.HasSave;
        if (continueButton != null)
        {
            continueButton.interactable = has; // disabled (not hidden) when there's no save
            if (!continueButton.gameObject.activeSelf) continueButton.gameObject.SetActive(true);
        }
        if (saveInfoLabel != null) saveInfoLabel.text = has ? SaveManager.DescribeSave() : "";
    }

    /// <summary>NEW GAME. Public so it can also be wired straight to a Button's onClick.</summary>
    public void StartTutorial()
    {
        if (SaveManager.HasSave && confirmDialog != null)
        {
            confirmDialog.Show("Start a new game? Your current save will be overwritten.",
                               BeginNewGame, null, "Overwrite", "Cancel");
            return;
        }
        BeginNewGame();
    }

    private void BeginNewGame()
    {
        if (SaveRegistry.Instance != null)
            SaveManager.NewGame(this, transitionAnimator, transitionTime);
        else
            SceneTransitionUtility.LoadScene(this, tutorialSceneName, transitionAnimator, transitionTime);
    }

    /// <summary>CONTINUE. Public so it can also be wired straight to a Button's onClick.</summary>
    public void ContinueGame()
    {
        if (!SaveManager.Continue(this, transitionAnimator, transitionTime))
            RefreshContinue();
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

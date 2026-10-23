using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// =============================================================
// GameTimeController.cs
// -------------------------------------------------------------
// Lets the player speed up / slow down / reset the world simulation (plant growth + decay, water
// drain, miasma, ability timers - everything that reads GameClock). T or the HUD button opens a
// small menu; Faster / Slower step through the speeds below, Reset returns to 1x.
//
// SETUP:
//   1. Put this on any always-active object (e.g. the HUD canvas root).
//   2. menuRoot = the little panel (inactive by default). Wire speedUpButton / slowDownButton /
//      resetButton, and optionally speedLabel (TMP, shows e.g. "2x") and toggleButton (the HUD button
//      that opens/closes the menu - no OnClick wiring needed, this hooks it up).
// =============================================================
public class GameTimeController : MonoBehaviour
{
    [Header("Menu")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private Button toggleButton;
    [SerializeField] private Button speedUpButton;
    [SerializeField] private Button slowDownButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text speedLabel;

    [Header("Tutorial reveal")]
    [Tooltip("If on, the toggle button (and the T hotkey) stay hidden/disabled until RevealTimeUI() is called - wire " +
             "that to the time-menu tutorial step's onStepShown, same as the other HUD reveals. MainUIController." +
             "RevealAllTutorialGatedUI also calls it. Leave off to show the time button from the start.")]
    [SerializeField] private bool startsHidden = true;

    private bool revealed = true;

    /// <summary>Shows the time toggle button and enables the hotkey. Safe to call repeatedly.</summary>
    public void RevealTimeUI()
    {
        revealed = true;
        if (toggleButton != null) toggleButton.gameObject.SetActive(true);
    }

    [Header("Speeds")]
    [Tooltip("The speeds Faster/Slower step through, lowest to highest. Must contain 1.")]
    [SerializeField] private float[] speedSteps = { 0.25f, 0.5f, 1f, 2f, 4f, 8f };
    [Tooltip("Label format; {0} is the speed, e.g. '2x'.")]
    [SerializeField] private string labelFormat = "{0}x";

    [Header("Input")]
    [SerializeField] private bool enableHotkey = true;

    private int index;
    private bool menuOpen;

    public bool IsOpen => menuOpen;

    private void Awake()
    {
        index = DefaultIndex();
        toggleButton?.onClick.AddListener(ToggleMenu);
        revealed = !startsHidden;
        if (startsHidden && toggleButton != null) toggleButton.gameObject.SetActive(false);
        speedUpButton?.onClick.AddListener(SpeedUp);
        slowDownButton?.onClick.AddListener(SlowDown);
        resetButton?.onClick.AddListener(ResetSpeed);
        closeButton?.onClick.AddListener(CloseMenu);
        if (menuRoot != null) menuRoot.SetActive(false);
        Apply();
    }

    private void OnDestroy()
    {
        if (menuOpen)
        {
            MenuLayerManager.NotifyClosed(this);
            GameInputModeManager.Instance?.SetGameplayMode();
        }
        // Never leave the world running at a changed speed behind after this scene's controller is gone.
        GameClock.ResetScale();
    }

    private int DefaultIndex()
    {
        for (int i = 0; i < speedSteps.Length; i++)
            if (Mathf.Approximately(speedSteps[i], 1f)) return i;
        return 0;
    }

    private void Update()
    {
        if (!revealed || !enableHotkey || Keyboard.current == null || !Keyboard.current.tKey.wasPressedThisFrame) return;
        ToggleMenu();
    }

    private static bool CanOpen()
    {
        var tutorial = TutorialSequenceController.Instance;
        if (tutorial != null && !tutorial.IsActionAllowedForTrigger("time_menu_opened")) return false;

        // Only from normal gameplay - not while the Journal/Inventory/Pot menu is open or the player is
        // in a placement mode (opening on top of those would leave the input mode wrong on close).
        var input = GameInputModeManager.Instance;
        return input == null || input.CurrentMode == GameInputModeManager.InputMode.Gameplay;
    }

    public void ToggleMenu()
    {
        if (!revealed) return;
        if (menuOpen) { CloseMenu(); return; }
        if (!CanOpen()) return;
        OpenMenu();
    }

    public void OpenMenu()
    {
        if (menuOpen) return;
        MenuLayerManager.NotifyOpened(this, CloseMenu);
        menuOpen = true;
        if (menuRoot != null) menuRoot.SetActive(true);
        Apply();

        // Same takeover the Journal/Inventory do: free cursor, camera locked, movement still allowed.
        GameInputModeManager.Instance?.SetMenuUIMode();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ThirdPersonCameraController.CameraLocked = true;
        TutorialSequenceController.Instance?.NotifyExternalTrigger("time_menu_opened");
    }

    public void CloseMenu()
    {
        if (!menuOpen) return;
        menuOpen = false;
        MenuLayerManager.NotifyClosed(this);
        if (menuRoot != null) menuRoot.SetActive(false);
        GameInputModeManager.Instance?.SetGameplayMode();
        TutorialSequenceController.Instance?.NotifyExternalTrigger("time_menu_closed");
    }

    public void SpeedUp()
    {
        int before = index;
        index = Mathf.Min(index + 1, speedSteps.Length - 1);
        Apply();
        if (index != before) TutorialSequenceController.Instance?.NotifyExternalTrigger("sped_up_time");
    }

    public void SlowDown()
    {
        int before = index;
        index = Mathf.Max(index - 1, 0);
        Apply();
        if (index != before) TutorialSequenceController.Instance?.NotifyExternalTrigger("slowed_down_time");
    }

    public void ResetSpeed()
    {
        int before = index;
        index = DefaultIndex();
        Apply();
        if (index != before) TutorialSequenceController.Instance?.NotifyExternalTrigger("reset_time_speed");
    }

    private void Apply()
    {
        if (speedSteps == null || speedSteps.Length == 0) return;
        float s = speedSteps[Mathf.Clamp(index, 0, speedSteps.Length - 1)];
        GameClock.SetScale(s);

        if (speedLabel != null) speedLabel.text = string.Format(labelFormat, s);
        if (speedUpButton != null) speedUpButton.interactable = index < speedSteps.Length - 1;
        if (slowDownButton != null) slowDownButton.interactable = index > 0;
        if (resetButton != null) resetButton.interactable = !Mathf.Approximately(s, 1f);
    }
}
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
        speedUpButton?.onClick.AddListener(SpeedUp);
        slowDownButton?.onClick.AddListener(SlowDown);
        resetButton?.onClick.AddListener(ResetSpeed);
        closeButton?.onClick.AddListener(CloseMenu);
        if (menuRoot != null) menuRoot.SetActive(false);
        Apply();
    }

    private void OnDestroy()
    {
        if (menuOpen) MenuLayerManager.NotifyClosed(this);
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
        if (!enableHotkey || Keyboard.current == null || !Keyboard.current.tKey.wasPressedThisFrame) return;
        if (!menuOpen && !CanOpen()) return;
        ToggleMenu();
    }

    private static bool CanOpen()
    {
        var tutorial = TutorialSequenceController.Instance;
        return tutorial == null || !tutorial.IsInputLocked;
    }

    public void ToggleMenu()
    {
        if (menuOpen) CloseMenu(); else OpenMenu();
    }

    public void OpenMenu()
    {
        if (menuOpen) return;
        MenuLayerManager.NotifyOpened(this, CloseMenu);
        menuOpen = true;
        if (menuRoot != null) menuRoot.SetActive(true);
        Apply();
    }

    public void CloseMenu()
    {
        if (!menuOpen) return;
        menuOpen = false;
        MenuLayerManager.NotifyClosed(this);
        if (menuRoot != null) menuRoot.SetActive(false);
    }

    public void SpeedUp() { index = Mathf.Min(index + 1, speedSteps.Length - 1); Apply(); }
    public void SlowDown() { index = Mathf.Max(index - 1, 0); Apply(); }
    public void ResetSpeed() { index = DefaultIndex(); Apply(); }

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

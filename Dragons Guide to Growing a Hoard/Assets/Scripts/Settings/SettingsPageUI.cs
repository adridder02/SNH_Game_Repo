using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =============================================================
// SettingsPageUI.cs
// -------------------------------------------------------------
// Drives a settings page. Put it on the page's root object and drag in whichever widgets you have —
// EVERY field is optional, anything left empty is simply skipped, so you can build the page in pieces.
//
//   VIDEO     changes are staged: pick resolution / window mode / vsync / frame cap / quality, then press
//             Apply Video (Revert Video undoes them). Leave Apply Video empty to apply instantly.
//             Brightness is live.
//   AUDIO     sliders are live (0-100%).
//   CAMERA    sensitivity + invert Y are live.
//   GAMEPLAY  UI scale, autosave on/off and interval.
//
// Dropdowns are filled from code (resolutions the screen supports, quality levels, etc.), so just add
// empty TMP_Dropdowns. Value labels are optional TMP_Texts next to sliders.
// =============================================================
public class SettingsPageUI : MonoBehaviour
{
    [Header("Video")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown windowModeDropdown;
    [SerializeField] private Toggle vsyncToggle;
    [SerializeField] private TMP_Dropdown frameRateDropdown;
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private TMP_Text brightnessValue;
    [SerializeField] private Button applyVideoButton;
    [SerializeField] private Button revertVideoButton;

    [Header("Audio")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private TMP_Text masterValue;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private TMP_Text musicValue;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Text sfxValue;
    [SerializeField] private Slider ambienceSlider;
    [SerializeField] private TMP_Text ambienceValue;
    [SerializeField] private Toggle muteWhenUnfocusedToggle;

    [Header("Controls & Camera")]
    [SerializeField] private Slider cameraSensitivitySlider;
    [SerializeField] private TMP_Text cameraSensitivityValue;
    [SerializeField] private Toggle invertYToggle;

    [Header("Gameplay & Accessibility")]
    [SerializeField] private Slider uiScaleSlider;
    [SerializeField] private TMP_Text uiScaleValue;
    [SerializeField] private Toggle autosaveToggle;
    [SerializeField] private Slider autosaveMinutesSlider;
    [SerializeField] private TMP_Text autosaveMinutesValue;

    [Header("General")]
    [SerializeField] private Button resetDefaultsButton;

    private static readonly int[] FrameRates = { 30, 60, 90, 120, 144, 165, 240, 0 }; // 0 = unlimited
    private static readonly FullScreenMode[] WindowModes =
        { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };

    private List<Vector2Int> resolutions;
    private GameSettings pendingVideo;   // staged video choices
    private bool refreshing;             // true while we set widget values from code

    // ------------------------------------------------------------
    private void Awake()
    {
        resolutions = SettingsManager.GetResolutions();
        BuildDropdowns();
        WireCallbacks();
    }

    private void OnEnable() => RefreshFromSettings();

    // ------------------------------------------------------------
    private void BuildDropdowns()
    {
        if (resolutionDropdown != null)
        {
            resolutionDropdown.ClearOptions();
            List<string> names = new List<string>();
            foreach (Vector2Int r in resolutions) names.Add($"{r.x} x {r.y}");
            resolutionDropdown.AddOptions(names);
        }

        if (windowModeDropdown != null)
        {
            windowModeDropdown.ClearOptions();
            windowModeDropdown.AddOptions(new List<string> { "Fullscreen", "Borderless Window", "Windowed" });
        }

        if (frameRateDropdown != null)
        {
            frameRateDropdown.ClearOptions();
            List<string> names = new List<string>();
            foreach (int f in FrameRates) names.Add(f == 0 ? "Unlimited" : f + " FPS");
            frameRateDropdown.AddOptions(names);
        }

        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
        }

        ConfigureSlider(brightnessSlider, 0f, 1f, false);
        ConfigureSlider(masterSlider, 0f, 1f, false);
        ConfigureSlider(musicSlider, 0f, 1f, false);
        ConfigureSlider(sfxSlider, 0f, 1f, false);
        ConfigureSlider(ambienceSlider, 0f, 1f, false);
        ConfigureSlider(cameraSensitivitySlider, 0.25f, 2f, false);
        ConfigureSlider(uiScaleSlider, 0.8f, 1.4f, false);
        ConfigureSlider(autosaveMinutesSlider, 1f, 15f, true);
    }

    private static void ConfigureSlider(Slider s, float min, float max, bool whole)
    {
        if (s == null) return;
        s.minValue = min;
        s.maxValue = max;
        s.wholeNumbers = whole;
    }

    // ------------------------------------------------------------
    private void WireCallbacks()
    {
        // ---- video (staged) ----
        resolutionDropdown?.onValueChanged.AddListener(i =>
        {
            if (refreshing || i < 0 || i >= resolutions.Count) return;
            pendingVideo.resolutionWidth = resolutions[i].x;
            pendingVideo.resolutionHeight = resolutions[i].y;
            VideoEdited();
        });
        windowModeDropdown?.onValueChanged.AddListener(i =>
        {
            if (refreshing) return;
            pendingVideo.fullscreenMode = (int)WindowModes[Mathf.Clamp(i, 0, WindowModes.Length - 1)];
            VideoEdited();
        });
        vsyncToggle?.onValueChanged.AddListener(v => { if (refreshing) return; pendingVideo.vsync = v; VideoEdited(); });
        frameRateDropdown?.onValueChanged.AddListener(i =>
        {
            if (refreshing) return;
            pendingVideo.frameRateCap = FrameRates[Mathf.Clamp(i, 0, FrameRates.Length - 1)];
            VideoEdited();
        });
        qualityDropdown?.onValueChanged.AddListener(i => { if (refreshing) return; pendingVideo.qualityLevel = i; VideoEdited(); });

        applyVideoButton?.onClick.AddListener(ApplyVideo);
        revertVideoButton?.onClick.AddListener(RefreshFromSettings);

        // ---- live ----
        brightnessSlider?.onValueChanged.AddListener(v => { if (refreshing) return; SettingsManager.Current.brightness = v; Live(); UpdateLabels(); });

        masterSlider?.onValueChanged.AddListener(v => { if (refreshing) return; SettingsManager.Current.masterVolume = v; Live(); UpdateLabels(); });
        musicSlider?.onValueChanged.AddListener(v => { if (refreshing) return; SettingsManager.Current.musicVolume = v; Live(); UpdateLabels(); });
        sfxSlider?.onValueChanged.AddListener(v => { if (refreshing) return; SettingsManager.Current.sfxVolume = v; Live(); UpdateLabels(); });
        ambienceSlider?.onValueChanged.AddListener(v => { if (refreshing) return; SettingsManager.Current.ambienceVolume = v; Live(); UpdateLabels(); });
        muteWhenUnfocusedToggle?.onValueChanged.AddListener(v => { if (refreshing) return; SettingsManager.Current.muteWhenUnfocused = v; Live(); });

        cameraSensitivitySlider?.onValueChanged.AddListener(v => { if (refreshing) return; SettingsManager.Current.cameraSensitivity = v; Live(); UpdateLabels(); });
        invertYToggle?.onValueChanged.AddListener(v => { if (refreshing) return; SettingsManager.Current.invertY = v; Live(); });

        uiScaleSlider?.onValueChanged.AddListener(v => { if (refreshing) return; SettingsManager.Current.uiScale = v; Live(); UpdateLabels(); });
        autosaveToggle?.onValueChanged.AddListener(v => { if (refreshing) return; SettingsManager.Current.autosave = v; Live(); });
        autosaveMinutesSlider?.onValueChanged.AddListener(v => { if (refreshing) return; SettingsManager.Current.autosaveMinutes = Mathf.RoundToInt(v); Live(); UpdateLabels(); });

        resetDefaultsButton?.onClick.AddListener(() =>
        {
            SettingsManager.ResetToDefaults();
            RefreshFromSettings();
        });
    }

    private void Live() => SettingsManager.Apply();

    private void VideoEdited()
    {
        if (applyVideoButton == null) ApplyVideo(); // no Apply button -> instant
    }

    private void ApplyVideo()
    {
        GameSettings s = SettingsManager.Current;
        s.videoChosen = true;
        s.resolutionWidth = pendingVideo.resolutionWidth;
        s.resolutionHeight = pendingVideo.resolutionHeight;
        s.fullscreenMode = pendingVideo.fullscreenMode;
        s.vsync = pendingVideo.vsync;
        s.frameRateCap = pendingVideo.frameRateCap;
        s.qualityLevel = pendingVideo.qualityLevel;
        SettingsManager.Apply();
    }

    // ------------------------------------------------------------
    // Pull everything from the saved settings into the widgets (without firing callbacks).
    // ------------------------------------------------------------
    public void RefreshFromSettings()
    {
        if (resolutions == null) return; // Awake hasn't run yet
        refreshing = true;

        GameSettings s = SettingsManager.Current;
        pendingVideo = s.Clone();
        if (!s.videoChosen)
        {
            pendingVideo.resolutionWidth = Screen.width;
            pendingVideo.resolutionHeight = Screen.height;
            pendingVideo.fullscreenMode = (int)Screen.fullScreenMode;
            pendingVideo.vsync = QualitySettings.vSyncCount > 0;
            pendingVideo.frameRateCap = Application.targetFrameRate > 0 ? Application.targetFrameRate : 0;
        }
        if (pendingVideo.qualityLevel < 0) pendingVideo.qualityLevel = QualitySettings.GetQualityLevel();

        if (resolutionDropdown != null)
        {
            int idx = resolutions.FindIndex(r => r.x == pendingVideo.resolutionWidth && r.y == pendingVideo.resolutionHeight);
            resolutionDropdown.SetValueWithoutNotify(idx >= 0 ? idx : resolutions.Count - 1);
        }
        if (windowModeDropdown != null)
        {
            int idx = System.Array.IndexOf(WindowModes, (FullScreenMode)pendingVideo.fullscreenMode);
            windowModeDropdown.SetValueWithoutNotify(idx >= 0 ? idx : 1);
        }
        vsyncToggle?.SetIsOnWithoutNotify(pendingVideo.vsync);
        if (frameRateDropdown != null)
        {
            int idx = System.Array.IndexOf(FrameRates, pendingVideo.frameRateCap);
            frameRateDropdown.SetValueWithoutNotify(idx >= 0 ? idx : FrameRates.Length - 1);
        }
        qualityDropdown?.SetValueWithoutNotify(Mathf.Clamp(pendingVideo.qualityLevel, 0, Mathf.Max(0, QualitySettings.names.Length - 1)));
        brightnessSlider?.SetValueWithoutNotify(s.brightness);

        masterSlider?.SetValueWithoutNotify(s.masterVolume);
        musicSlider?.SetValueWithoutNotify(s.musicVolume);
        sfxSlider?.SetValueWithoutNotify(s.sfxVolume);
        ambienceSlider?.SetValueWithoutNotify(s.ambienceVolume);
        muteWhenUnfocusedToggle?.SetIsOnWithoutNotify(s.muteWhenUnfocused);

        cameraSensitivitySlider?.SetValueWithoutNotify(s.cameraSensitivity);
        invertYToggle?.SetIsOnWithoutNotify(s.invertY);

        uiScaleSlider?.SetValueWithoutNotify(s.uiScale);
        autosaveToggle?.SetIsOnWithoutNotify(s.autosave);
        autosaveMinutesSlider?.SetValueWithoutNotify(s.autosaveMinutes);

        refreshing = false;
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        GameSettings s = SettingsManager.Current;
        SetLabel(brightnessValue, Mathf.RoundToInt(s.brightness * 100f) + "%");
        SetLabel(masterValue, Mathf.RoundToInt(s.masterVolume * 100f) + "%");
        SetLabel(musicValue, Mathf.RoundToInt(s.musicVolume * 100f) + "%");
        SetLabel(sfxValue, Mathf.RoundToInt(s.sfxVolume * 100f) + "%");
        SetLabel(ambienceValue, Mathf.RoundToInt(s.ambienceVolume * 100f) + "%");
        SetLabel(cameraSensitivityValue, s.cameraSensitivity.ToString("0.00") + "x");
        SetLabel(uiScaleValue, Mathf.RoundToInt(s.uiScale * 100f) + "%");
        SetLabel(autosaveMinutesValue, s.autosaveMinutes + " min");
    }

    private static void SetLabel(TMP_Text t, string text)
    {
        if (t != null) t.text = text;
    }
}

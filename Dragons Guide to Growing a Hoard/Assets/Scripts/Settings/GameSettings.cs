using System;
using UnityEngine;

// =============================================================
// GameSettings.cs
// -------------------------------------------------------------
// Plain data for every player setting. Saved as one JSON blob in PlayerPrefs by SettingsManager —
// deliberately separate from the game SAVE, since settings belong to the player/machine, not to a
// playthrough (loading or starting a new game never changes them).
// =============================================================
[Serializable]
public class GameSettings
{
    // ---------------- Video ----------------
    [Tooltip("False until the player has applied video settings once — until then the game keeps whatever " +
             "resolution/window mode Unity launched with.")]
    public bool videoChosen = false;
    public int resolutionWidth = 1920;
    public int resolutionHeight = 1080;
    /// <summary>A UnityEngine.FullScreenMode value.</summary>
    public int fullscreenMode = (int)FullScreenMode.FullScreenWindow;
    public bool vsync = true;
    /// <summary>Frame cap used when vsync is off. 0 = unlimited.</summary>
    public int frameRateCap = 0;
    /// <summary>Index into QualitySettings.names. -1 = leave Unity's current quality level alone.</summary>
    public int qualityLevel = -1;
    /// <summary>0..1, 0.5 = neutral. Drives a global post-exposure offset.</summary>
    public float brightness = 0.5f;

    // ---------------- Audio (0..1) ----------------
    public float masterVolume = 1f;
    public float musicVolume = 0.8f;
    public float sfxVolume = 1f;
    public float ambienceVolume = 0.8f;
    public bool muteWhenUnfocused = true;

    // ---------------- Controls & camera ----------------
    /// <summary>Multiplier on the camera's own sensitivity (1 = as authored).</summary>
    public float cameraSensitivity = 1f;
    public bool invertY = false;

    // ---------------- Gameplay & accessibility ----------------
    /// <summary>Multiplier for UI size (see UIScaleApplier).</summary>
    public float uiScale = 1f;
    public bool autosave = true;
    public int autosaveMinutes = 5;

    public GameSettings Clone() => (GameSettings)MemberwiseClone();
}

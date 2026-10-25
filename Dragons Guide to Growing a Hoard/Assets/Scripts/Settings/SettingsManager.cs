using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// =============================================================
// SettingsManager.cs
// -------------------------------------------------------------
// Owns the player's settings: loads them at startup, applies them to Unity (screen, quality, audio,
// brightness) and tells the rest of the game when they change (SettingsManager.Changed).
//
// NO SCENE SETUP NEEDED — it creates itself before the first scene loads and survives scene changes.
//
// AUDIO: create an AudioMixer asset and put it at  Assets/.../Resources/MainAudioMixer.mixer  (any
// Resources folder, e.g. your Data/Resources one). Add groups Master > Music, SFX, Ambience, and
// EXPOSE each group's Volume parameter, renaming them exactly:
//        MasterVolume   MusicVolume   SfxVolume   AmbienceVolume
// Then set each AudioSource's "Output" to the matching group. With no mixer (or an unexposed Master),
// master volume falls back to AudioListener.volume so the Master slider still works.
//
// BRIGHTNESS: a hidden global URP Volume adds post-exposure. Needs Post Processing enabled on the
// camera (Camera > Rendering > Post Processing).
// =============================================================
public class SettingsManager : MonoBehaviour
{
    private const string SaveKey = "GameSettings_v1";
    private const string MixerResource = "MainAudioMixer";

    public const string MasterParam = "MasterVolume";
    public const string MusicParam = "MusicVolume";
    public const string SfxParam = "SfxVolume";
    public const string AmbienceParam = "AmbienceVolume";

    private static SettingsManager instance;
    private static GameSettings current;

    /// <summary>Fired after any settings change has been applied (use to refresh things like the camera).</summary>
    public static event Action Changed;

    public static GameSettings Current
    {
        get
        {
            if (current == null) current = LoadFromPrefs();
            return current;
        }
    }

    // Convenience reads for other scripts.
    public static float CameraSensitivity => Current.cameraSensitivity;
    public static bool InvertY => Current.invertY;
    public static float UIScale => Current.uiScale;
    public static bool AutosaveEnabled => Current.autosave;
    public static int AutosaveMinutes => Current.autosaveMinutes;

    private AudioMixer mixer;
    private VolumeProfile brightnessProfile;
    private ColorAdjustments brightnessAdjust;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        current = null;
        Changed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (instance != null) return;
        GameObject go = new GameObject("SettingsManager");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SettingsManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;

        mixer = Resources.Load<AudioMixer>(MixerResource);
        if (mixer == null)
            Debug.Log($"[SettingsManager] No AudioMixer found at Resources/{MixerResource} — only the Master slider will work (via AudioListener).");

        ApplyAll();
        Application.focusChanged += OnFocusChanged;
    }

    private void OnDestroy()
    {
        Application.focusChanged -= OnFocusChanged;
        if (instance == this) instance = null;
    }

    // ------------------------------------------------------------
    // PUBLIC API
    // ------------------------------------------------------------
    /// <summary>Applies the current settings, saves them and fires Changed. Call after editing Current.</summary>
    public static void Apply(bool save = true)
    {
        if (instance != null) instance.ApplyAll();
        if (save) Save();
        Changed?.Invoke();
    }

    public static void ResetToDefaults()
    {
        GameSettings fresh = new GameSettings();
        // Keep the screen the player is on instead of forcing 1080p.
        fresh.videoChosen = false;
        current = fresh;
        Apply();
    }

    public static void Save()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Current));
        PlayerPrefs.Save();
    }

    private static GameSettings LoadFromPrefs()
    {
        GameSettings s = null;
        if (PlayerPrefs.HasKey(SaveKey))
        {
            try { s = JsonUtility.FromJson<GameSettings>(PlayerPrefs.GetString(SaveKey)); }
            catch (Exception) { s = null; }
        }
        return s ?? new GameSettings();
    }

    /// <summary>Distinct resolutions the display supports (highest last), for the resolution dropdown.</summary>
    public static List<Vector2Int> GetResolutions()
    {
        List<Vector2Int> list = new List<Vector2Int>();
        foreach (Resolution r in Screen.resolutions)
        {
            Vector2Int v = new Vector2Int(r.width, r.height);
            if (!list.Contains(v)) list.Add(v);
        }
        if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
        return list;
    }

    // ------------------------------------------------------------
    // APPLY
    // ------------------------------------------------------------
    private void ApplyAll()
    {
        GameSettings s = Current;

        // ---- video ----
        if (s.qualityLevel >= 0 && QualitySettings.names.Length > 0)
            QualitySettings.SetQualityLevel(Mathf.Clamp(s.qualityLevel, 0, QualitySettings.names.Length - 1), true);

        QualitySettings.vSyncCount = s.vsync ? 1 : 0;
        Application.targetFrameRate = s.vsync ? -1 : (s.frameRateCap > 0 ? s.frameRateCap : -1);

        if (s.videoChosen)
            Screen.SetResolution(s.resolutionWidth, s.resolutionHeight, (FullScreenMode)s.fullscreenMode);

        // ---- audio ----
        bool masterOnMixer = SetMixerVolume(MasterParam, s.masterVolume);
        AudioListener.volume = masterOnMixer ? 1f : s.masterVolume;
        SetMixerVolume(MusicParam, s.musicVolume);
        SetMixerVolume(SfxParam, s.sfxVolume);
        SetMixerVolume(AmbienceParam, s.ambienceVolume);

        // ---- brightness ----
        ApplyBrightness(s.brightness);
    }

    private bool SetMixerVolume(string param, float linear01)
    {
        if (mixer == null) return false;
        float dB = linear01 <= 0.0001f ? -80f : Mathf.Log10(Mathf.Clamp01(linear01)) * 20f;
        return mixer.SetFloat(param, dB);
    }

    private void ApplyBrightness(float brightness01)
    {
        if (brightnessAdjust == null)
        {
            GameObject go = new GameObject("BrightnessVolume");
            go.transform.SetParent(transform);
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000f; // wins over scene volumes for this one override only
            brightnessProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            brightnessAdjust = brightnessProfile.Add<ColorAdjustments>(true);
            brightnessAdjust.postExposure.overrideState = true;
            volume.sharedProfile = brightnessProfile;
        }

        // 0.5 = neutral, 0 = -1.5 stops, 1 = +1.5 stops
        brightnessAdjust.postExposure.value = (brightness01 - 0.5f) * 3f;
    }

    private void OnFocusChanged(bool focused)
    {
        AudioListener.pause = !focused && Current.muteWhenUnfocused;
    }
}

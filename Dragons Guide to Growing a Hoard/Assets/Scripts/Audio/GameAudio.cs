using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// =============================================================
// GameAudio.cs
// -------------------------------------------------------------
// Static entry point for every sound:   GameAudio.Play(SoundId.TaskComplete);
// Reads the SoundBank at Resources/SoundBank, plays through the mixer group of the sound's category (so the
// Sfx / Music / Ambience sliders in Settings work), and quietly does nothing if a clip isn't assigned yet.
// =============================================================
public static class GameAudio
{
    private static SoundBank bank;
    private static bool bankSearched;
    private static AudioMixer mixer;
    private static bool mixerSearched;
    private static AudioSource sfxSource;
    private static readonly Dictionary<SoundId, float> lastPlayed = new Dictionary<SoundId, float>();

    /// <summary>Time (unscaled) of the most recent sound played through GameAudio.</summary>
    public static float LastPlayTime { get; private set; } = -10f;

    public static SoundBank Bank
    {
        get
        {
            if (!bankSearched)
            {
                bankSearched = true;
                bank = Resources.Load<SoundBank>("SoundBank");
                if (bank == null)
                    Debug.LogWarning("[GameAudio] No SoundBank found at Resources/SoundBank - sounds are off. " +
                                     "Use the menu Neglect > Audio > Create Sound Bank.");
            }
            return bank;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        bank = null; bankSearched = false; mixer = null; mixerSearched = false; sfxSource = null;
        lastPlayed.Clear(); LastPlayTime = -10f;
    }

    public static AudioClip GetClip(SoundId id) => Bank?.Get(id)?.clip;

    public static float GetVolume(SoundId id) => Bank?.Get(id)?.volume ?? 1f;

    public static AudioCategory GetCategory(SoundId id) => Bank?.Get(id)?.category ?? AudioCategory.Sfx;

    /// <summary>Plays a sound flat (2D) - UI and feedback sounds.</summary>
    public static void Play(SoundId id, float volumeScale = 1f)
    {
        SoundBank.Entry e = Bank?.Get(id);
        if (e == null || e.clip == null) return;

        // Same sound twice within a few frames (e.g. a button AND a menu both reacting) = play once.
        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(id, out float last) && now - last < 0.06f) return;
        lastPlayed[id] = now;
        LastPlayTime = now;

        EnsureSfxSource();
        sfxSource.PlayOneShot(e.clip, e.volume * volumeScale);
    }

    /// <summary>Plays a sound at a place in the world (3D).</summary>
    public static void PlayAt(SoundId id, Vector3 position, float volumeScale = 1f)
    {
        SoundBank.Entry e = Bank?.Get(id);
        if (e == null || e.clip == null) return;
        LastPlayTime = Time.unscaledTime;

        var go = new GameObject("Sfx_" + id);
        go.transform.position = position;
        AudioSource s = go.AddComponent<AudioSource>();
        s.clip = e.clip;
        s.volume = e.volume * volumeScale;
        s.spatialBlend = 1f;
        Route(s, e.category);
        s.Play();
        Object.Destroy(go, e.clip.length + 0.2f);
    }

    /// <summary>Plays any clip at a world position, routed through the mixer (for one-off clips set in the Inspector).</summary>
    public static void PlayClipAt(AudioClip clip, Vector3 position, float volume = 1f, AudioCategory category = AudioCategory.Sfx)
    {
        if (clip == null) return;
        LastPlayTime = Time.unscaledTime;
        var go = new GameObject("Sfx_" + clip.name);
        go.transform.position = position;
        AudioSource s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.volume = volume;
        s.spatialBlend = 1f;
        Route(s, category);
        s.Play();
        Object.Destroy(go, clip.length + 0.2f);
    }

    /// <summary>Sends an AudioSource to the mixer group for its category (no-op without a mixer).</summary>
    public static void Route(AudioSource source, AudioCategory category)
    {
        if (source == null) return;
        if (!mixerSearched)
        {
            mixerSearched = true;
            mixer = Resources.Load<AudioMixer>("MainAudioMixer");
        }
        if (mixer == null) return;

        string groupName = category == AudioCategory.Music ? "Music" : category == AudioCategory.Ambience ? "Ambience" : "Sfx";
        foreach (AudioMixerGroup g in mixer.FindMatchingGroups(groupName))
            if (g.name == groupName) { source.outputAudioMixerGroup = g; return; }
    }

    private static void EnsureSfxSource()
    {
        if (sfxSource != null) return;
        var go = new GameObject("GameAudio_Sfx");
        Object.DontDestroyOnLoad(go);
        sfxSource = go.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
        Route(sfxSource, AudioCategory.Sfx);
    }
}

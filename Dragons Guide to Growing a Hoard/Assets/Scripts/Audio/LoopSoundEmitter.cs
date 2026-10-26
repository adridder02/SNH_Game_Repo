using UnityEngine;

// =============================================================
// LoopSoundEmitter.cs
// -------------------------------------------------------------
// Drop this on ANY object to make it play a looping sound from the SoundBank while it's active:
//   - Waterbell prefab  -> Waterbell Active          - Sparkmint prefab -> Sparkmint Ambient
//   - Windmill prefab   -> Windmill                  - an empty object in the Tutorial scene -> Tutorial Wind (untick Spatial)
//   - an empty object near the world tree -> Tree Music 1 / Tree Music 2
// Spatial = the sound gets quieter with distance (Min/Max Distance). Untick it for music / scene-wide wind.
// =============================================================
public class LoopSoundEmitter : MonoBehaviour
{
    public SoundId sound = SoundId.Windmill;
    [Tooltip("Optional: use this clip instead of the one in the SoundBank.")]
    public AudioClip overrideClip;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("3D")]
    public bool spatial = true;
    public float minDistance = 2f;
    public float maxDistance = 20f;

    [Header("Fade")]
    public float fadeInSeconds = 1f;

    private AudioSource source;
    private float fade;

    void OnEnable()
    {
        AudioClip clip = overrideClip != null ? overrideClip : GameAudio.GetClip(sound);
        if (clip == null) return;

        if (source == null)
        {
            var child = new GameObject("Loop_" + sound);
            child.transform.SetParent(transform, false);
            source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            GameAudio.Route(source, GameAudio.GetCategory(sound));
        }

        source.clip = clip;
        fade = fadeInSeconds <= 0.01f ? 1f : 0f;
        source.volume = volume * GameAudio.GetVolume(sound) * fade;
        source.Play();
    }

    void Update()
    {
        if (source == null || !source.isPlaying || fade >= 1f) return;
        fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeInSeconds));
        source.volume = volume * GameAudio.GetVolume(sound) * fade;
    }

    void OnDisable()
    {
        if (source != null) source.Stop();
    }
}

using UnityEngine;

// =============================================================
// PlayerWaterAudio.cs
// -------------------------------------------------------------
// Put on the Player. Splash when the dragon enters a WaterVolume, a swimming loop while inside, and it fades
// out when they leave. (Footsteps are muted in the water by PlayerSounds.)
// =============================================================
public class PlayerWaterAudio : MonoBehaviour
{
    [Tooltip("The point checked against the water, raised above the feet so wading doesn't count as swimming.")]
    [SerializeField] private float checkHeight = 0.4f;
    [SerializeField] private float splashCooldown = 0.6f;
    [SerializeField] private float swimFadeSeconds = 0.4f;
    [Range(0f, 1f)] [SerializeField] private float swimVolume = 1f;

    private AudioSource swimSource;
    private bool wasInWater;
    private float lastSplash = -10f;
    private float fade;

    public bool InWater { get; private set; }

    void Update()
    {
        InWater = WaterVolume.Contains(transform.position + Vector3.up * checkHeight);

        if (InWater && !wasInWater && Time.time - lastSplash > splashCooldown)
        {
            lastSplash = Time.time;
            GameAudio.Play(SoundId.JumpIntoWater);
        }
        wasInWater = InWater;

        AudioClip clip = GameAudio.GetClip(SoundId.Swimming);
        if (clip == null) return;

        if (swimSource == null)
        {
            var go = new GameObject("Loop_Swimming");
            go.transform.SetParent(transform, false);
            swimSource = go.AddComponent<AudioSource>();
            swimSource.playOnAwake = false;
            swimSource.loop = true;
            swimSource.spatialBlend = 0f;
            swimSource.clip = clip;
            GameAudio.Route(swimSource, GameAudio.GetCategory(SoundId.Swimming));
        }

        fade = Mathf.MoveTowards(fade, InWater ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, swimFadeSeconds));
        swimSource.volume = fade * swimVolume * GameAudio.GetVolume(SoundId.Swimming);

        if (fade > 0.001f && !swimSource.isPlaying) swimSource.Play();
        else if (fade <= 0.001f && swimSource.isPlaying) swimSource.Stop();
    }
}

using UnityEngine;

public class PlayerSounds : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private AudioClip wingFlap;
    [SerializeField] private AudioClip walking;
    [SerializeField] private AudioClip running;
    [SerializeField] private AudioClip landingThud;

    [Header("Volume")]
    [Range(0f, 1f)] [SerializeField] private float wingFlapVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float walkingVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float runningVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float landingThudVolume = 1f;

    private AudioSource audioSource;
    private Animator playerAni;
    private bool wasFlying = false;

    // Surface-based footsteps (SoundBank footsteps; falls back to the clips above if the bank has none).
    private SurfaceType surface = SurfaceType.Default;
    private float surfaceTimer;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        playerAni = GetComponent<Animator>();
    }

    private void Update()
    {
        bool isFlying = playerAni.GetBool("IsFlying");
        float speed = playerAni.GetFloat("Speed");

        // Detect the frame the dragon transitions from flying to grounded
        if (wasFlying && !isFlying)
            audioSource.PlayOneShot(landingThud, landingThudVolume);

        wasFlying = isFlying;

        // In water the swim loop (PlayerWaterAudio) takes over from footsteps.
        if (WaterVolume.Count > 0 && WaterVolume.Contains(transform.position + Vector3.up * 0.4f))
        {
            if (audioSource.isPlaying) audioSource.Stop();
            return;
        }

        surfaceTimer -= Time.deltaTime;
        if (surfaceTimer <= 0f && !isFlying && speed >= 1f)
        {
            surfaceTimer = 0.2f;
            surface = FootstepSensor.Detect(transform.position);
        }

        if (isFlying)
        {
            PlayLoop(wingFlap, wingFlapVolume, 1f);
        }
        else if (speed >= 3f)
        {
            AudioClip c = StepClip(running, runningVolume, true, out float v, out float pitch);
            PlayLoop(c, v, pitch);
        }
        else if (speed >= 1f)
        {
            AudioClip c = StepClip(walking, walkingVolume, false, out float v, out float pitch);
            PlayLoop(c, v, pitch);
        }
        else
        {
            if (audioSource.isPlaying)
                audioSource.Stop();
        }
    }

    private AudioClip StepClip(AudioClip fallback, float fallbackVolume, bool run, out float volume, out float pitch)
    {
        SoundBank bank = GameAudio.Bank;
        if (bank != null && bank.TryGetStep(surface, out AudioClip clip, out float v))
        {
            volume = v;
            pitch = run ? bank.runPitch : 1f;
            return clip;
        }
        volume = fallbackVolume;
        pitch = 1f;
        return fallback;
    }

    private void PlayLoop(AudioClip clip, float volume, float pitch)
    {
        if (clip == null) return;
        audioSource.pitch = pitch;
        if (audioSource.clip == clip && audioSource.isPlaying) { audioSource.volume = volume; return; }

        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.loop = true;
        audioSource.Play();
    }
}
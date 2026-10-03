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

        if (isFlying)
        {
            PlayLoop(wingFlap, wingFlapVolume);
        }
        else if (speed >= 3f)
        {
            PlayLoop(running, runningVolume);
        }
        else if (speed >= 1f)
        {
            PlayLoop(walking, walkingVolume);
        }
        else
        {
            if (audioSource.isPlaying)
                audioSource.Stop();
        }
    }

    private void PlayLoop(AudioClip clip, float volume)
    {
        if (audioSource.clip == clip && audioSource.isPlaying) return;

        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.loop = true;
        audioSource.Play();
    }
}
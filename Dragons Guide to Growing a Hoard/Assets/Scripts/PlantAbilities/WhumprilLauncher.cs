using UnityEngine;

// =============================================================
// WhumprilLauncher.cs
// -------------------------------------------------------------
// Attach to the Whumpril prefab (needs a non-trigger Collider
// to physically collide with the player). "When the player lands on
// this plant, it will launch you into the air high above it."
//
// Tries CharacterController first (most third-person controllers in
// this project use one — see ThirdPersonCameraController references
// elsewhere), falls back to Rigidbody.AddForce if the player uses
// physics-based movement instead. Launch direction is straight up
// plus a small outward kick away from the mushroom's centre, so the
// player doesn't just re-land on the same spot.
//
// For CharacterController players, this calls PlayerController.
// ApplyExternalLaunch() directly (compile-time checked) rather than
// via SendMessage, so a missing/renamed method fails loudly instead
// of silently doing nothing. If the player object doesn't have a
// PlayerController on it — e.g. a different/prototype controller —
// it falls back to SendMessage so this script still stays decoupled
// from any other CharacterController-driven script that happens to
// expose its own "ApplyExternalLaunch(Vector3)" method.
// =============================================================
public class WhumprilLauncher : MonoBehaviour
{
    public string playerTag = "Player";

    [Tooltip("Launch speed when FULLY GROWN. A smaller Whumpril launches less - see Small Launch Up Force.")]
    public float launchUpForce = 12f;
    [Tooltip("Launch speed at the smallest size (just planted). Blends up to Launch Up Force as the plant grows " +
             "(needs a PlantGrowthSize on this plant; without one it always uses Launch Up Force).")]
    public float smallLaunchUpForce = 5f;
    [Tooltip("Auto-found on this object or a parent if left empty.")]
    [SerializeField] private PlantGrowthSize growth;

    [Header("Collider")]
    [Tooltip("The collider that actually triggers the launch - normally the MESH collider. Leave empty to " +
             "auto-pick the first MeshCollider under this object (falling back to any collider). Other " +
             "colliders on the mushroom (e.g. an old Box Collider) no longer launch the player.")]
    [SerializeField] private Collider launchCollider;
    public float launchOutwardForce = 2f;

    [Header("Sound")]
    [Tooltip("Optional: a clip just for this plant. Leave empty to use the SoundBank's Whumpril Launch sound.")]
    [SerializeField] private AudioClip launchSoundOverride;
    [Range(0f, 1f)] [SerializeField] private float launchSoundVolume = 1f;
    [Tooltip("A small Whumpril is quieter; this is the volume at the smallest size (1 = no change).")]
    [Range(0f, 1f)] [SerializeField] private float smallSizeVolumeScale = 0.6f;

    public float cooldown = 0.5f;

    [Tooltip("While the player is flying, only launch them once they've actually touched down on the " +
             "mushroom, so flying THROUGH the trigger volume at altitude doesn't bounce them.")]
    public bool flyingNeedsGroundContact = true;

    private float lastLaunchTime = -999f;

    /// <summary>Current launch speed: grows with the plant's size.</summary>
    public float CurrentLaunchUpForce =>
        Mathf.Lerp(smallLaunchUpForce, launchUpForce, growth != null ? growth.ShownGrowth01 : 1f);

    private void Awake()
    {
        if (growth == null) growth = GetComponentInParent<PlantGrowthSize>();

        if (launchCollider == null)
        {
            launchCollider = GetComponentInChildren<MeshCollider>(true);
            if (launchCollider == null) launchCollider = GetComponentInChildren<Collider>(true);
        }

        // Collision/trigger events are delivered to the collider's own GameObject, so if the launch collider
        // lives on a child (no Rigidbody on this object) a relay there forwards them up to us.
        if (launchCollider != null && launchCollider.gameObject != gameObject &&
            launchCollider.GetComponent<LauncherColliderRelay>() == null)
            launchCollider.gameObject.AddComponent<LauncherColliderRelay>().launcher = this;
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        HandleCollision(collision);
    }

    // ---- Entry points (also used by LauncherColliderRelay when the launch collider is on a child) ----
    internal void HandleCollision(Collision collision)
    {
        // Only contacts with the launch collider count (not a leftover box collider etc).
        if (launchCollider != null && collision.contactCount > 0 &&
            collision.GetContact(0).thisCollider != launchCollider) return;
        TryLaunch(collision.gameObject);
    }

    internal void HandleTrigger(Collider other)
    {
        // Trigger callbacks don't say which of OUR colliders was hit, so confirm the player really overlaps
        // the launch collider (not just some other collider on this mushroom).
        if (launchCollider != null && launchCollider.isTrigger && !OverlapsLaunchCollider(other)) return;
        if (launchCollider != null && !launchCollider.isTrigger) return; // solid launch collider => collisions only
        TryLaunch(other.gameObject);
    }

    private bool OverlapsLaunchCollider(Collider other)
    {
        if (!launchCollider.enabled) return false;
        try
        {
            return Physics.ComputePenetration(
                launchCollider, launchCollider.transform.position, launchCollider.transform.rotation,
                other, other.transform.position, other.transform.rotation,
                out _, out _);
        }
        catch { return launchCollider.bounds.Intersects(other.bounds); } // non-convex mesh etc.
    }

    // Enter handles walking / falling / flying onto the mushroom.
    private void OnTriggerEnter(Collider other)
    {
        HandleTrigger(other);
    }

    // Stay covers every case where Enter already fired but the launch didn't happen (e.g. the player
    // was mid-flight, in cooldown, or only touched down after entering the volume), and re-landing
    // while still overlapping after an earlier launch. OnTriggerEnter alone only fires once per entry.
    private void OnTriggerStay(Collider other)
    {
        HandleTrigger(other);
    }

    private void TryLaunch(GameObject obj)
    {
        if (!obj.CompareTag(playerTag)) return;
        if (Time.time - lastLaunchTime < cooldown) return;

        PlayerController pc = obj.GetComponentInParent<PlayerController>();

        if (pc != null)
        {
            // Don't re-fire while still travelling upward from a previous launch (still overlapping
            // the trigger on the way out) — wait until they're coming back down.
            if (pc.IsRising) return;

            // Flying through the trigger at altitude shouldn't launch; touching down on it should.
            if (flyingNeedsGroundContact && pc.IsFlying && !pc.IsGrounded) return;
        }

        Vector3 outward = (obj.transform.position - transform.position);
        outward.y = 0f;

        if (outward.sqrMagnitude > 0.01f)
        {
            outward.Normalize();
        }
        else
        {
            Vector3 playerForward = obj.transform.forward;
            playerForward.y = 0f;
            outward = playerForward.sqrMagnitude > 0.01f ? playerForward.normalized : Vector3.forward;
        }

        Vector3 launchVelocity = Vector3.up * CurrentLaunchUpForce + outward * launchOutwardForce;

        CharacterController cc = obj.GetComponent<CharacterController>();
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        bool launched = false;

        if (cc != null)
        {
            if (pc != null)
            {
                // Returns false if the player can't be launched right now (movement disabled).
                // The cooldown is only consumed on a real launch, so a rejected one retries next frame.
                launched = pc.ApplyExternalLaunch(launchVelocity);
            }
            else
            {
                obj.SendMessage("ApplyExternalLaunch", launchVelocity, SendMessageOptions.DontRequireReceiver);
                launched = true;
            }
        }
        else if (rb != null)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(launchVelocity, ForceMode.VelocityChange);
            launched = true;
        }
        else
        {
            Debug.LogWarning("[WhumprilLauncher] Player has neither a CharacterController nor a " +
                              "Rigidbody — can't launch.", this);
            return;
        }

        if (!launched) return;

        lastLaunchTime = Time.time;

        float sizeT = growth != null ? growth.ShownGrowth01 : 1f;
        float vol = launchSoundVolume * Mathf.Lerp(smallSizeVolumeScale, 1f, sizeT);
        if (launchSoundOverride != null) GameAudio.PlayClipAt(launchSoundOverride, transform.position, vol);
        else GameAudio.PlayAt(SoundId.WhumprilLaunch, transform.position, vol);

        Debug.Log($"[WhumprilLauncher] Launched '{obj.name}' with velocity {launchVelocity}.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.6f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * CurrentLaunchUpForce * 0.2f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * CurrentLaunchUpForce * 0.2f, 0.15f);
    }
}

// Forwards collision/trigger events from a launch collider on a child object up to its WhumprilLauncher.
// Added automatically at runtime - nothing to set up.
internal class LauncherColliderRelay : MonoBehaviour
{
    public WhumprilLauncher launcher;

    private void OnCollisionEnter(Collision c) { if (launcher != null) launcher.HandleCollision(c); }
    private void OnCollisionStay(Collision c) { if (launcher != null) launcher.HandleCollision(c); }
    private void OnTriggerEnter(Collider o) { if (launcher != null) launcher.HandleTrigger(o); }
    private void OnTriggerStay(Collider o) { if (launcher != null) launcher.HandleTrigger(o); }
}
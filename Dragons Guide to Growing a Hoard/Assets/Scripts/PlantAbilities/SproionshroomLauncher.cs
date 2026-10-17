using UnityEngine;

// =============================================================
// SproionshroomLauncher.cs
// -------------------------------------------------------------
// Attach to the Sproionshroom prefab (needs a non-trigger Collider
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
public class SproionshroomLauncher : MonoBehaviour
{
    public string playerTag = "Player";
    public float launchUpForce = 12f;
    public float launchOutwardForce = 2f;
    public float cooldown = 0.5f;

    [Tooltip("While the player is flying, only launch them once they've actually touched down on the " +
             "mushroom, so flying THROUGH the trigger volume at altitude doesn't bounce them.")]
    public bool flyingNeedsGroundContact = true;

    private float lastLaunchTime = -999f;

    private void OnCollisionEnter(Collision collision)
    {
        TryLaunch(collision.gameObject);
    }

    // Enter handles walking / falling / flying onto the mushroom.
    private void OnTriggerEnter(Collider other)
    {
        TryLaunch(other.gameObject);
    }

    // Stay covers every case where Enter already fired but the launch didn't happen (e.g. the player
    // was mid-flight, in cooldown, or only touched down after entering the volume), and re-landing
    // while still overlapping after an earlier launch. OnTriggerEnter alone only fires once per entry.
    private void OnTriggerStay(Collider other)
    {
        TryLaunch(other.gameObject);
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

        Vector3 launchVelocity = Vector3.up * launchUpForce + outward * launchOutwardForce;

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
            Debug.LogWarning("[SproionshroomLauncher] Player has neither a CharacterController nor a " +
                              "Rigidbody — can't launch.", this);
            return;
        }

        if (!launched) return;

        lastLaunchTime = Time.time;
        Debug.Log($"[SproionshroomLauncher] Launched '{obj.name}' with velocity {launchVelocity}.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.6f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * launchUpForce * 0.2f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * launchUpForce * 0.2f, 0.15f);
    }
}
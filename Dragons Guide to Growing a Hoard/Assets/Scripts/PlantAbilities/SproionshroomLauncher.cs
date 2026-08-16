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

    private float lastLaunchTime = -999f;

    private void OnCollisionEnter(Collision collision)
    {
        TryLaunch(collision.gameObject, collision.GetContact(0).point);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryLaunch(other.gameObject, other.ClosestPoint(transform.position));
    }

    private void TryLaunch(GameObject obj, Vector3 contactPoint)
    {
        if (!obj.CompareTag(playerTag)) return;
        if (Time.time - lastLaunchTime < cooldown) return;
        lastLaunchTime = Time.time;

        Vector3 outward = (obj.transform.position - transform.position);
        outward.y = 0f;

        if (outward.sqrMagnitude > 0.01f)
        {
            outward.Normalize();
        }
        else
        {
            // Player landed close to dead-center — fall back to their own facing direction
            // instead of a fixed world axis, so the kick still feels consistent with their
            // approach rather than launching them in an arbitrary compass direction.
            Vector3 playerForward = obj.transform.forward;
            playerForward.y = 0f;
            outward = playerForward.sqrMagnitude > 0.01f ? playerForward.normalized : Vector3.forward;
        }

        Vector3 launchVelocity = Vector3.up * launchUpForce + outward * launchOutwardForce;

        CharacterController cc = obj.GetComponent<CharacterController>();
        Rigidbody rb = obj.GetComponent<Rigidbody>();

        if (cc != null)
        {
            PlayerController pc = obj.GetComponent<PlayerController>();
            if (pc != null)
            {
                // Compile-time-checked hookup — if this method is ever renamed or removed,
                // the project fails to build instead of silently doing nothing at runtime.
                pc.ApplyExternalLaunch(launchVelocity);
            }
            else
            {
                // No PlayerController here (different/prototype controller script). Fall back to
                // SendMessage so this stays decoupled from whatever that script is called — it just
                // needs its own public "ApplyExternalLaunch(Vector3)" method to receive this.
                obj.SendMessage("ApplyExternalLaunch", launchVelocity, SendMessageOptions.DontRequireReceiver);
            }
        }
        else if (rb != null)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(launchVelocity, ForceMode.VelocityChange);
        }
        else
        {
            Debug.LogWarning("[SproionshroomLauncher] Player has neither a CharacterController nor a " +
                              "Rigidbody — can't launch.", this);
        }

        Debug.Log($"[SproionshroomLauncher] Launched '{obj.name}' with velocity {launchVelocity}.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.6f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * launchUpForce * 0.2f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * launchUpForce * 0.2f, 0.15f);
    }
}
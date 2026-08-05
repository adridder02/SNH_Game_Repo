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
        outward = outward.sqrMagnitude > 0.01f ? outward.normalized : Vector3.forward;

        Vector3 launchVelocity = Vector3.up * launchUpForce + outward * launchOutwardForce;

        CharacterController cc = obj.GetComponent<CharacterController>();
        Rigidbody rb = obj.GetComponent<Rigidbody>();

        if (cc != null)
        {
            // Most CharacterController-driven movement scripts read a separate velocity field each
            // frame rather than letting anything external move them directly — SendMessage lets this
            // stay decoupled from whatever the actual controller script is called. If your controller
            // exposes a public method instead (e.g. "ApplyImpulse(Vector3)"), call that directly here
            // instead of SendMessage for a compile-time-checked hookup.
            obj.SendMessage("ApplyExternalLaunch", launchVelocity, SendMessageOptions.DontRequireReceiver);
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

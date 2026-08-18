using UnityEngine;

// =============================================================
// DrifterLilyPad.cs
// -------------------------------------------------------------
// Attach to the Drifter prefab. "Some may appear with a flower on
// top - do not jump on these ones." hasFlower is a plain Inspector
// toggle for now (set per-prefab-variant, or randomised at spawn —
// see RandomiseFlower) — swap the flower mesh's active state to
// match it so the warning is visually readable in-game, not just
// via this bool.
// =============================================================
public class DrifterLilyPad : MonoBehaviour
{
    [Tooltip("If true, standing on this pad sinks it (and the player). If false, it stays buoyant no matter what stands on it.")]
    public bool hasFlower = true;

    [Tooltip("Optional: the flower mesh/GameObject, kept in sync with hasFlower so the danger is visible to the player.")]
    public GameObject flowerVisual;

    [Tooltip("How far/fast the pad sinks once triggered, while hasFlower is true.")]
    public float sinkSpeed = 0.6f;
    public float sinkDepth = 1.5f;

    [Tooltip("Randomise hasFlower on spawn instead of hand-authoring it per prefab instance. 0 = never flowered, 1 = always.")]
    [Range(-1f, 1f)] public float randomFlowerChance = -1f; // -1 = don't randomise, use hasFlower as authored

    private string playerTag = "Player";
    private bool isSinking;
    private float startY;
    private float sunkAmount;

    private void Awake()
    {
        if (randomFlowerChance >= 0f)
            hasFlower = Random.value < randomFlowerChance;

        flowerVisual?.SetActive(hasFlower);
        startY = transform.position.y;
    }

    private void OnCollisionStay(Collision collision) => CheckStandingOn(collision.gameObject);
    private void OnTriggerStay(Collider other) => CheckStandingOn(other.gameObject);

    private void CheckStandingOn(GameObject obj)
    {
        if (!hasFlower || isSinking) return;
        if (!obj.CompareTag(playerTag)) return;

        isSinking = true;
        Debug.Log($"[DrifterLilyPad] '{gameObject.name}' has a flower — sinking under the player.");
    }

    private void Update()
    {
        if (!isSinking || sunkAmount >= sinkDepth) return;

        float step = sinkSpeed * Time.deltaTime;
        sunkAmount = Mathf.Min(sinkDepth, sunkAmount + step);
        transform.position = new Vector3(transform.position.x, startY - sunkAmount, transform.position.z);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = hasFlower ? new Color(0.95f, 0.2f, 0.5f) : new Color(0.2f, 0.9f, 0.5f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.4f);
    }
}

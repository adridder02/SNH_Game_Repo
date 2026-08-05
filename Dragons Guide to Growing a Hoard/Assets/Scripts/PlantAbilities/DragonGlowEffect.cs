using UnityEngine;

// =============================================================
// DragonGlowEffect.cs
// -------------------------------------------------------------
// Spawned on the player when a Glowcap is consumed. Placeholder is a
// simple point Light childed to the player, on for 'duration' seconds
// — swap for a real emission-shader treatment or particle later
// (see the brief: "Add an emission to the dragon when used").
// Re-using/refreshing an existing glow (rather than stacking lights)
// if one is already active.
// =============================================================
public class DragonGlowEffect : MonoBehaviour
{
    private float remaining;
    private Light glowLight;

    public static void ApplyTo(GameObject player, float duration, Color colour)
    {
        if (player == null) return;

        DragonGlowEffect existing = player.GetComponentInChildren<DragonGlowEffect>();
        if (existing != null)
        {
            existing.remaining = Mathf.Max(existing.remaining, duration);
            return;
        }

        GameObject go = new GameObject("GlowcapEffect");
        go.transform.SetParent(player.transform, worldPositionStays: false);
        go.transform.localPosition = Vector3.up * 1f;

        DragonGlowEffect effect = go.AddComponent<DragonGlowEffect>();
        effect.remaining = duration;

        effect.glowLight = go.AddComponent<Light>();
        effect.glowLight.type = LightType.Point;
        effect.glowLight.color = colour;
        effect.glowLight.range = 6f;
        effect.glowLight.intensity = 1.5f;
    }

    private void Update()
    {
        remaining -= Time.deltaTime;
        if (remaining <= 0f) Destroy(gameObject);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 1f, 0.7f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}

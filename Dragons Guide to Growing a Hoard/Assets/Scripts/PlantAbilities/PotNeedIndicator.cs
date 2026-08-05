using UnityEngine;

// =============================================================
// PotNeedIndicator.cs
// -------------------------------------------------------------
// Spawned above a pot by AbilityConsumableEffects when a Pollen Puff
// is used on it. Reads the plant's SoilScore/LightScore/WaterScore
// (all public on PlantState already) and colours a placeholder marker
// by whichever is lowest — yellow = light, blue = water, brown = soil.
// Ties fall back in soil -> light -> water priority.
//
// Placeholder is a small coloured primitive (visible in-game, not just
// a Scene-view gizmo, since "the pot changes colour" is meant to be
// player-visible feedback) — swap PollenCloudMarkerPrefab on the
// AbilityItemData for a real particle/decal later; this self-builds
// one if none is assigned.
// =============================================================
public class PotNeedIndicator : MonoBehaviour
{
    private static readonly Color SoilColor = new Color(0.55f, 0.35f, 0.15f);   // brown
    private static readonly Color LightColor = new Color(0.95f, 0.85f, 0.2f);   // yellow
    private static readonly Color WaterColor = new Color(0.2f, 0.5f, 0.95f);    // blue

    private PotContents pot;
    private float remainingSeconds;

    public static PotNeedIndicator AttachTo(PotContents pot, float duration, GameObject markerPrefab)
    {
        if (pot == null || pot.Plant == null) return null;

        // Only one active indicator per pot at a time — refresh instead of stacking.
        PotNeedIndicator existing = pot.GetComponentInChildren<PotNeedIndicator>();
        if (existing != null) Destroy(existing.gameObject);

        GameObject markerGO = markerPrefab != null ? Instantiate(markerPrefab) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        markerGO.name = "PollenCloudMarker";
        markerGO.transform.SetParent(pot.transform, worldPositionStays: false);
        markerGO.transform.localPosition = Vector3.up * 1.2f;
        markerGO.transform.localScale = Vector3.one * 0.15f;

        Collider col = markerGO.GetComponent<Collider>();
        if (col != null) Destroy(col);

        PotNeedIndicator indicator = markerGO.AddComponent<PotNeedIndicator>();
        indicator.pot = pot;
        indicator.remainingSeconds = duration;
        indicator.Refresh();
        return indicator;
    }

    private void Refresh()
    {
        if (pot == null || pot.Plant == null) return;

        int soil = pot.Plant.SoilScore;
        int light = pot.Plant.LightScore;
        int water = pot.Plant.WaterScore;

        Color colour = SoilColor;
        int lowest = soil;

        if (light < lowest) { lowest = light; colour = LightColor; }
        if (water < lowest) { colour = WaterColor; }

        Renderer rend = GetComponent<Renderer>();
        if (rend != null) rend.material.color = colour;
    }

    private void Update()
    {
        remainingSeconds -= Time.deltaTime;
        if (remainingSeconds <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // Cheap enough to just refresh every frame while active — pot conditions can change quickly.
        Refresh();
    }
}

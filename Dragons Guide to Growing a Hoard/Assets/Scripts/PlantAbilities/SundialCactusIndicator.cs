using UnityEngine;

// =============================================================
// SundialCactusIndicator.cs
// -------------------------------------------------------------
// Attach alongside PlantState (and LightSensor) on the Sundial
// Cactus prefab. Reads the plant's own LightSensor and converts
// NormalisedIntensity into a 0-4 "stem count": 0% dark = 0, 25% = 1,
// 50% = 2, 75% = 3, 100% (full sunlight) = 4.
//
// INDICATORS - use either or both:
//   stemLights   : 4 POINT LIGHTS (children of the cactus). They start switched OFF (deactivated) and switch ON one
//                  by one as the stem count rises, fading in/out smoothly. Set each light's colour / range /
//                  intensity on the Light itself - the intensity you set is the one it fades up to.
//   stemObjects  : the old way - plain GameObjects (meshes etc.) toggled on/off to match StemCount.
// =============================================================
[RequireComponent(typeof(PlantState))]
public class SundialCactusIndicator : MonoBehaviour
{
    [Header("Point lights (preferred)")]
    [Tooltip("One point light per stem, in order (light 0 turns on first). Leave them deactivated in the " +
             "prefab - the script switches them on.")]
    public Light[] stemLights = new Light[4];

    [Tooltip("Seconds a light takes to fade up / down. 0 = instant on/off.")]
    [Min(0f)] public float lightFadeSeconds = 0.4f;

    [Header("Plain objects (optional)")]
    [Tooltip("Optional: GameObjects (one per stem) toggled on/off to match StemCount.")]
    public GameObject[] stemObjects = new GameObject[4];

    private LightSensor lightSensor;
    private float[] targetIntensity;   // the intensity each light was set to in the Inspector
    private float[] fade;              // 0..1 current fade of each light

    public int StemCount { get; private set; }

    private void Awake()
    {
        lightSensor = GetComponent<LightSensor>() ?? GetComponentInChildren<LightSensor>();
        if (lightSensor == null)
            Debug.LogWarning("[SundialCactusIndicator] No LightSensor found - stem count will always read 0.", this);

        int n = stemLights != null ? stemLights.Length : 0;
        targetIntensity = new float[n];
        fade = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (stemLights[i] == null) continue;
            targetIntensity[i] = stemLights[i].intensity;
            stemLights[i].gameObject.SetActive(false);
        }

        if (stemObjects != null)
            for (int i = 0; i < stemObjects.Length; i++)
                if (stemObjects[i] != null) stemObjects[i].SetActive(false);
    }

    private void Update()
    {
        float intensity = lightSensor != null ? lightSensor.NormalisedIntensity : 0f;

        StemCount =
            intensity >= 0.999f ? 4 :
            intensity >= 0.75f  ? 3 :
            intensity >= 0.50f  ? 2 :
            intensity >= 0.25f  ? 1 : 0;

        if (stemObjects != null)
            for (int i = 0; i < stemObjects.Length; i++)
                if (stemObjects[i] != null) stemObjects[i].SetActive(i < StemCount);

        UpdateLights();
    }

    private void UpdateLights()
    {
        if (stemLights == null) return;

        float step = lightFadeSeconds <= 0.001f ? 1f : Time.deltaTime / lightFadeSeconds;

        for (int i = 0; i < stemLights.Length; i++)
        {
            Light l = stemLights[i];
            if (l == null) continue;

            bool wantOn = i < StemCount;
            fade[i] = Mathf.MoveTowards(fade[i], wantOn ? 1f : 0f, step);

            if (fade[i] > 0.001f)
            {
                if (!l.gameObject.activeSelf) l.gameObject.SetActive(true);
                l.intensity = targetIntensity[i] * fade[i];
            }
            else if (l.gameObject.activeSelf)
            {
                l.gameObject.SetActive(false);   // fully faded out -> deactivated again
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.95f, 0.85f, 0.2f);
        for (int i = 0; i < StemCount; i++)
        {
            Vector3 baseP = transform.position + new Vector3((i - 1.5f) * 0.15f, 0f, 0f);
            Gizmos.DrawLine(baseP, baseP + Vector3.up * (0.2f + i * 0.05f));
        }
    }
}
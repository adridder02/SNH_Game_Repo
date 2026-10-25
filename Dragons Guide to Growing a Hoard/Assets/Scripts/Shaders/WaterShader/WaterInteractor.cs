using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Add to the player (or any object) that should disturb the water. Creates ripples and foam around it
/// while it touches the surface; moving faster makes stronger ripples. Up to 16 interactors at once.
/// </summary>
public class WaterInteractor : MonoBehaviour
{
    public WaterWaveSettings water = new WaterWaveSettings();

    [Tooltip("How far the ripples and foam spread, in metres.")]
    public float radius = 2f;
    [Range(0, 1)] public float baseStrength = 0.35f;
    [Tooltip("Extra strength per m/s of movement.")]
    public float strengthPerSpeed = 0.1f;
    [Tooltip("How far above the surface still counts as touching the water.")]
    public float contactHeight = 0.5f;

    [Tooltip("Optional: point at the feet. Defaults to this transform.")]
    public Transform feet;

    [HideInInspector] public Vector3 surfacePosition;
    [HideInInspector] public float strength;

    private Vector3 lastPosition;

    private void OnEnable()
    {
        water.ReadFromMaterial();
        lastPosition = Source.position;
        WaterInteractionManager.Register(this);
    }

    private void OnDisable() => WaterInteractionManager.Unregister(this);

    private Transform Source => feet != null ? feet : transform;

    /// <summary>Called by the manager once per frame, after all movement.</summary>
    public void Refresh()
    {
        Vector3 p = Source.position;
        float speed = (p - lastPosition).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPosition = p;

        float waterY = water.HeightAt(p);
        float touching = Mathf.Clamp01(1f - (p.y - waterY) / Mathf.Max(contactHeight, 0.01f));

        strength = touching * Mathf.Clamp01(baseStrength + speed * strengthPerSpeed);
        surfacePosition = new Vector3(p.x, waterY, p.z);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.67f, 0.92f, 0.6f);
        Gizmos.DrawWireSphere(Source.position, radius);
    }
}

/// <summary>Created automatically. Sends interactor data to the water shader every frame.</summary>
public class WaterInteractionManager : MonoBehaviour
{
    private const int MaxInteractors = 16;

    private static readonly List<WaterInteractor> Interactors = new();
    private static WaterInteractionManager instance;

    private static readonly int PositionsId = Shader.PropertyToID("_WaterInteractors");
    private static readonly int ParamsId = Shader.PropertyToID("_WaterInteractorParams");
    private static readonly int CountId = Shader.PropertyToID("_WaterInteractorCount");

    private readonly Vector4[] positions = new Vector4[MaxInteractors];
    private readonly Vector4[] parameters = new Vector4[MaxInteractors];

    public static void Register(WaterInteractor interactor)
    {
        if (!Interactors.Contains(interactor)) Interactors.Add(interactor);

        if (instance == null)
        {
            var go = new GameObject("WaterInteractionManager") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<WaterInteractionManager>();
        }
    }

    public static void Unregister(WaterInteractor interactor) => Interactors.Remove(interactor);

    private void LateUpdate()
    {
        int count = Mathf.Min(Interactors.Count, MaxInteractors);

        for (int i = 0; i < count; i++)
        {
            var interactor = Interactors[i];
            interactor.Refresh();

            Vector3 p = interactor.surfacePosition;
            positions[i] = new Vector4(p.x, p.y, p.z, interactor.radius);
            parameters[i] = new Vector4(interactor.strength, 0f, 0f, 0f);
        }

        Shader.SetGlobalVectorArray(PositionsId, positions);
        Shader.SetGlobalVectorArray(ParamsId, parameters);
        Shader.SetGlobalFloat(CountId, count);
    }

    private void OnDestroy()
    {
        Shader.SetGlobalFloat(CountId, 0f);
        if (instance == this) instance = null;
    }
}

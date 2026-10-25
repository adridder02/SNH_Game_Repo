using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BuoyantObject : MonoBehaviour
{
    private readonly Color red = new(0.92f, 0.25f, 0.2f);
    private readonly Color green = new(0.2f, 0.92f, 0.51f);
    private readonly Color blue = new(0.2f, 0.67f, 0.92f);
    private readonly Color orange = new(0.97f, 0.79f, 0.26f);

    [Header("Water")]
    public WaterWaveSettings water = new WaterWaveSettings();

    [Header("Buoyancy")]
    [Range(1, 5)] public float strength = 1f;
    [Range(0.2f, 5)] public float objectDepth = 1f;

    public float velocityDrag = 0.99f;
    public float angularDrag = 0.5f;

    [Header("Effectors")]
    public Transform[] effectors;

    private Rigidbody rb;
    private Vector3[] effectorProjections;

    [ContextMenu("Read Wave Settings From Material")]
    private void ReadWaveSettings() => water.ReadFromMaterial();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        water.ReadFromMaterial();

        effectorProjections = new Vector3[effectors.Length];
        for (var i = 0; i < effectors.Length; i++) effectorProjections[i] = effectors[i].position;
    }

    private void OnDisable()
    {
        if (rb != null) rb.useGravity = true;
    }

    private void FixedUpdate()
    {
        var effectorAmount = effectors.Length;
        if (effectorAmount == 0) return;

        for (var i = 0; i < effectorAmount; i++)
        {
            var effectorPosition = effectors[i].position;

            effectorProjections[i] = effectorPosition;
            effectorProjections[i].y = water.HeightAt(effectorPosition);

            // gravity
            rb.AddForceAtPosition(Physics.gravity / effectorAmount, effectorPosition, ForceMode.Acceleration);

            var waveHeight = effectorProjections[i].y;
            var effectorHeight = effectorPosition.y;

            if (!(effectorHeight < waveHeight)) continue; // not submerged

            var submersion = Mathf.Clamp01(waveHeight - effectorHeight) / objectDepth;
            var buoyancy = Mathf.Abs(Physics.gravity.y) * submersion * strength;

            // buoyancy
            rb.AddForceAtPosition(Vector3.up * buoyancy, effectorPosition, ForceMode.Acceleration);

            // damping (Unity 6: velocity -> linearVelocity), divided so it doesn't scale with effector count
            rb.AddForce(-rb.linearVelocity * (velocityDrag * Time.fixedDeltaTime) / effectorAmount, ForceMode.VelocityChange);
            rb.AddTorque(-rb.angularVelocity * (angularDrag * Time.fixedDeltaTime) / effectorAmount, ForceMode.Impulse);
        }
    }

    private void OnDrawGizmos()
    {
        if (effectors == null) return;

        for (var i = 0; i < effectors.Length; i++)
        {
            if (effectors[i] == null) return;

            if (!Application.isPlaying || effectorProjections == null || effectorProjections.Length != effectors.Length)
            {
                Gizmos.color = green;
                Gizmos.DrawSphere(effectors[i].position, 0.06f);
            }
            else
            {
                Gizmos.color = effectors[i].position.y < effectorProjections[i].y ? red : green;
                Gizmos.DrawSphere(effectors[i].position, 0.06f);

                Gizmos.color = orange;
                Gizmos.DrawSphere(effectorProjections[i], 0.06f);

                Gizmos.color = blue;
                Gizmos.DrawLine(effectors[i].position, effectorProjections[i]);
            }
        }
    }
}

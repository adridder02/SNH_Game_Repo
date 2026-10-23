using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WaterRippleController : MonoBehaviour
{
    private const int RippleCount = 8;

    private static readonly int RippleDataID =
        Shader.PropertyToID("_WaterRippleData");

    private static readonly int RippleSettingsID =
        Shader.PropertyToID("_WaterRippleSettings");

    [Header("Ripple Settings")]
    [SerializeField, Min(0.1f)] private float radius = 2.5f;
    [SerializeField, Min(0.1f)] private float waveSpeed = 3f;
    [SerializeField, Min(0f)] private float amplitude = 0.15f;
    [SerializeField, Min(0.1f)] private float lifetime = 2f;

    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField, Min(0.1f)] private float stepDistance = 0.6f;
    [SerializeField, Min(0.01f)] private float minimumInterval = 0.15f;

    private readonly Vector4[] rippleData =
        new Vector4[RippleCount];

    private int nextRipple;
    private Vector3 lastRipplePosition;
    private float nextRippleTime;
    private bool playerInside;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        PublishSettings();
        PublishRipples();
    }

    private void OnEnable()
    {
        PublishSettings();
        PublishRipples();
    }

    private void Update()
    {
        // Keep the shared settings synchronized with Inspector values.
        PublishSettings();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        playerInside = true;
        lastRipplePosition = GetContactPosition(other);
        nextRippleTime = Time.time + minimumInterval;

        AddRipple(lastRipplePosition);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        Vector3 position = GetContactPosition(other);

        if (!playerInside)
        {
            playerInside = true;
            lastRipplePosition = position;
            AddRipple(position);
            return;
        }

        float moved =
            Vector3.Distance(position, lastRipplePosition);

        if (moved >= stepDistance &&
            Time.time >= nextRippleTime)
        {
            AddRipple(position);
            lastRipplePosition = position;
            nextRippleTime = Time.time + minimumInterval;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        AddRipple(GetContactPosition(other));
        playerInside = false;
    }

    private Vector3 GetContactPosition(Collider player)
    {
        Bounds bounds = player.bounds;

        // Approximate the contact point using the player's feet.
        return new Vector3(
            bounds.center.x,
            bounds.min.y,
            bounds.center.z
        );
    }

    private void AddRipple(Vector3 position)
    {
        // Store X and Z in the first two components,
        // and the ripple's creation time in W.
        rippleData[nextRipple] = new Vector4(
            position.x,
            position.z,
            0f,
            Time.time
        );

        nextRipple = (nextRipple + 1) % RippleCount;

        PublishRipples();
    }

    private void PublishSettings()
    {
        Shader.SetGlobalVector(
            RippleSettingsID,
            new Vector4(radius, waveSpeed, amplitude, lifetime)
        );
    }

    private void PublishRipples()
    {
        Shader.SetGlobalVectorArray(
            RippleDataID,
            rippleData
        );
    }

    /* Complie this */
}

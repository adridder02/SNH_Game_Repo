
using UnityEngine;

public class WaterTrigger : MonoBehaviour
{
    [SerializeField] private Renderer waterRenderer;

    private static readonly int RipplePosition =
        Shader.PropertyToID("_RipplePosition");

    private static readonly int RippleStrength =
        Shader.PropertyToID("_RippleStrength");

    private MaterialPropertyBlock properties;

    void Awake()
    {
        properties = new MaterialPropertyBlock();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (waterRenderer == null) return;

        properties.Clear();
        waterRenderer.GetPropertyBlock(properties);

        properties.SetVector(
            RipplePosition,
            other.transform.position
        );

        properties.SetFloat(RippleStrength, 1f);

        waterRenderer.SetPropertyBlock(properties);
    }
}

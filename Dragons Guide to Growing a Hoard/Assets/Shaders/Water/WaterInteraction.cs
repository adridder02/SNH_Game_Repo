
using UnityEngine;

public class WaterInteraction : MonoBehaviour
{
    [SerializeField] private Renderer waterRenderer;

    private MaterialPropertyBlock properties;
    private static readonly int RipplePosition =
        Shader.PropertyToID("_RipplePosition");

    void Awake()
    {
        properties = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (waterRenderer == null) return;

        waterRenderer.GetPropertyBlock(properties);

        properties.SetVector(
            RipplePosition,
            transform.position
        );

        waterRenderer.SetPropertyBlock(properties);
    }
}

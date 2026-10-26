using UnityEngine;

// =============================================================
// RangeCircle.cs
// -------------------------------------------------------------
// A flat, see-through 2D shape drawn on the ground to show an ability's reach: a full circle (angle 360) or
// a cone/wedge (angle < 360) that points the way the followed object faces. Soft fill + brighter rim.
// Not parented to the plant (so the plant's growth scaling doesn't stretch it) - it follows it.
// Created from code:  RangeCircle c = RangeCircle.Create(plantTransform, color, radius, 0.03f, 120f, 0f);
//                     c.SetVisible(true/false);   // fades in/out
// =============================================================
public class RangeCircle : MonoBehaviour
{
    private const int Segments = 64;
    private Mesh mesh;
    private float angle = 360f;
    private float yawOffset;
    private float lastYaw;

    private Transform follow;
    private float groundOffset;
    private float radius = 1f;
    private float fadeSeconds = 0.35f;
    private float fade;
    private bool wantVisible;
    private Material material;
    private MeshRenderer meshRenderer;

    public static RangeCircle Create(Transform follow, Color color, float radius, float groundOffset = 0.03f, float angle = 360f, float yawOffset = 0f)
    {
        var go = new GameObject("RangeCircle");
        RangeCircle rc = go.AddComponent<RangeCircle>();
        rc.follow = follow;
        rc.groundOffset = groundOffset;
        rc.radius = radius;
        rc.angle = Mathf.Clamp(angle, 1f, 360f);
        rc.yawOffset = yawOffset;
        rc.Build(color);
        rc.Apply();
        return rc;
    }

    public void SetRadius(float r) { radius = r; }
    public void SetYawOffset(float y) { yawOffset = y; }
    public void SetAngle(float a)
    {
        a = Mathf.Clamp(a, 1f, 360f);
        if (Mathf.Approximately(a, angle)) return;
        angle = a;
        if (mesh != null) { Destroy(mesh); mesh = BuildMesh(angle); GetComponent<MeshFilter>().sharedMesh = mesh; }
    }
    public void SetVisible(bool visible) { wantVisible = visible; }

    private void Build(Color color)
    {
        mesh = BuildMesh(angle);

        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("GUI/Text Shader") ?? Shader.Find("Unlit/Color");
        material = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
        material.renderQueue = 2995;
        baseColor = color;
        material.color = new Color(color.r, color.g, color.b, 0f);
        meshRenderer.sharedMaterial = material;
        meshRenderer.enabled = false;
    }

    private Color baseColor;

    private static Mesh BuildMesh(float angleDeg)
    {
        // Unit-radius sector pointing along +Z (scaled to the real radius by the transform): soft fill + brighter rim band.
        const float inner = 0.94f;
        bool full = angleDeg >= 359.9f;
        float half = angleDeg * 0.5f * Mathf.Deg2Rad;
        float fillA = full ? 0.10f : 0.16f;

        var verts = new Vector3[1 + (Segments + 1) * 2];
        var cols = new Color[verts.Length];
        verts[0] = Vector3.zero;
        cols[0] = new Color(1f, 1f, 1f, fillA * 0.5f);

        for (int i = 0; i <= Segments; i++)
        {
            float a = full ? (float)i / Segments * Mathf.PI * 2f : Mathf.Lerp(-half, half, (float)i / Segments);
            Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            verts[1 + i * 2] = dir * inner;
            cols[1 + i * 2] = new Color(1f, 1f, 1f, fillA + 0.2f);
            verts[2 + i * 2] = dir;
            cols[2 + i * 2] = new Color(1f, 1f, 1f, 1f);
        }

        var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i < Segments; i++)
        {
            int a0 = 1 + i * 2, b0 = 2 + i * 2, a1 = 1 + (i + 1) * 2, b1 = 2 + (i + 1) * 2;
            tris.AddRange(new[] { 0, a0, a1 });          // fill, facing up (clockwise seen from above)
            tris.AddRange(new[] { a0, b0, a1 });         // rim band
            tris.AddRange(new[] { a1, b0, b1 });
        }

        var m = new Mesh { name = "RangeIndicatorMesh", hideFlags = HideFlags.HideAndDontSave };
        m.vertices = verts;
        m.colors = cols;
        m.triangles = tris.ToArray();
        m.RecalculateBounds();
        return m;
    }

    private void LateUpdate()
    {
        if (follow == null) { Destroy(gameObject); return; }

        float step = fadeSeconds <= 0.001f ? 1f : Time.unscaledDeltaTime / fadeSeconds;
        fade = Mathf.MoveTowards(fade, wantVisible ? 1f : 0f, step);

        bool show = fade > 0.001f;
        if (meshRenderer.enabled != show) meshRenderer.enabled = show;
        if (show) material.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * fade);

        Apply();
    }

    private void Apply()
    {
        if (follow == null) return;
        transform.position = follow.position + Vector3.up * groundOffset;
        if (angle < 359.9f)
        {
            Vector3 f = follow.forward; f.y = 0f;
            if (f.sqrMagnitude > 0.0001f) lastYaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, lastYaw + yawOffset, 0f);
        }
        else transform.rotation = Quaternion.identity;
        transform.localScale = new Vector3(radius, 1f, radius);
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
        if (mesh != null) Destroy(mesh);
    }
}

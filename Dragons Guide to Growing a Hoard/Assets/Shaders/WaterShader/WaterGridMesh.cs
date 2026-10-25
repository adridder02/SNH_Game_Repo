using UnityEngine;

/// <summary>
/// Generates a flat, subdivided grid mesh for the water surface, centered on this object.
/// Add to a GameObject, assign the water material to the MeshRenderer, then tweak the values.
/// Changes regenerate automatically in the editor; right-click the component → Generate also works.
/// </summary>

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WaterGridMesh : MonoBehaviour
{
    [Min(1f)] public float size = 100f;
    [Range(2, 1000)] public int resolution = 200;   // quads per side
    [Min(0f)] public float boundsPadding = 5f;      // keeps displaced vertices from being frustum-culled

    private Mesh mesh;

    private void OnEnable() => Generate();

    private void OnDisable() => Cleanup();

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Mesh creation isn't allowed inside OnValidate itself, so defer it.
        UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Generate(); };
    }
#endif

    [ContextMenu("Generate")]
    public void Generate()
    {
        Cleanup();

        int verts = resolution + 1;
        var vertices = new Vector3[verts * verts];
        var uvs = new Vector2[verts * verts];
        var normals = new Vector3[verts * verts];
        var indices = new int[resolution * resolution * 6];

        float half = size * 0.5f;
        for (int z = 0, i = 0; z < verts; z++)
        {
            for (int x = 0; x < verts; x++, i++)
            {
                float u = (float)x / resolution;
                float v = (float)z / resolution;
                vertices[i] = new Vector3(u * size - half, 0f, v * size - half);
                uvs[i] = new Vector2(u, v);
                normals[i] = Vector3.up;
            }
        }

        for (int z = 0, t = 0, vi = 0; z < resolution; z++, vi++)
        {
            for (int x = 0; x < resolution; x++, t += 6, vi++)
            {
                // Clockwise winding so the top face is visible from above.
                indices[t]     = vi;
                indices[t + 1] = vi + verts;
                indices[t + 2] = vi + 1;
                indices[t + 3] = vi + 1;
                indices[t + 4] = vi + verts;
                indices[t + 5] = vi + verts + 1;
            }
        }

        mesh = new Mesh
        {
            name = "Water Grid",
            hideFlags = HideFlags.DontSave,
            indexFormat = vertices.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32
                                                  : UnityEngine.Rendering.IndexFormat.UInt16
        };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = indices;

        var b = mesh.bounds;
        b.Expand(new Vector3(boundsPadding * 2f, boundsPadding * 2f, boundsPadding * 2f));
        mesh.bounds = b;

        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    private void Cleanup()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        mesh = null;
    }
}

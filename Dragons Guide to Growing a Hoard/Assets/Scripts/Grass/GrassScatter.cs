// =============================================================
// GrassScatter.cs
// -------------------------------------------------------------
// One component, two jobs:
//   • Small area + low density  -> garden dressing around pots
//   • Large area + high density -> open-field grass
// Same blade mesh, same wind shader (Custom/GrassWind) — only the
// area size and density change between the two use cases.
//
// Uses Graphics.DrawMeshInstanced, batched in groups of 1023
// (Unity's per-call instancing cap), so it scales from a handful
// of blades to tens of thousands without a compute shader.
// =============================================================

using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GrassScatter : MonoBehaviour
{
    private const int BATCH_SIZE = 1023; // Unity's DrawMeshInstanced cap per call

    [Header("Area")]
    [Tooltip("Flat rectangular area, centred on this object, in local XZ.")]
    public Vector2 areaSize = new Vector2(2f, 2f);

    [Tooltip("Blades per square metre. ~5-15 for dressing, 40-100+ for dense fields.")]
    public float densityPerSqm = 10f;

    [Tooltip("Layer mask used to find ground height for each blade via raycast.")]
    public LayerMask groundMask = ~0;

    [Header("Blade")]
    [Tooltip("Leave null to auto-generate a simple tapered blade mesh.")]
    public Mesh bladeMesh;
    [Tooltip("Must use Custom/GrassWind (or compatible) with GPU Instancing enabled on the material.")]
    public Material grassMaterial;
    public float minHeight = 0.8f;
    public float maxHeight = 1.2f;
    public float minWidth = 0.8f;
    public float maxWidth = 1.1f;

    [Header("Regeneration")]
    public int randomSeed = 0;

    private List<Matrix4x4[]> batches = new List<Matrix4x4[]>();
    private bool dirty = true;

    private void OnValidate() => dirty = true;
    private void OnEnable() => dirty = true;

    private void Update()
    {
        if (dirty)
        {
            if (bladeMesh == null)
                bladeMesh = GenerateDefaultBladeMesh();

            GenerateInstances();
            dirty = false;
        }

        if (grassMaterial == null || bladeMesh == null) return;

        foreach (Matrix4x4[] batch in batches)
            Graphics.DrawMeshInstanced(bladeMesh, 0, grassMaterial, batch, batch.Length,
                null, UnityEngine.Rendering.ShadowCastingMode.Off, false);
    }

    // ---------------------------------------------------------------
    // GenerateInstances — scatters points across areaSize, raycasts
    // down to find ground height, and bakes the results into
    // fixed-size batches ready for DrawMeshInstanced.
    // ---------------------------------------------------------------
    private void GenerateInstances()
    {
        batches.Clear();

        int count = Mathf.Max(0, Mathf.RoundToInt(areaSize.x * areaSize.y * densityPerSqm));
        if (count == 0) return;

        System.Random rng = new System.Random(randomSeed);
        List<Matrix4x4> current = new List<Matrix4x4>(BATCH_SIZE);

        for (int i = 0; i < count; i++)
        {
            float x = ((float)rng.NextDouble() - 0.5f) * areaSize.x;
            float z = ((float)rng.NextDouble() - 0.5f) * areaSize.y;
            Vector3 origin = transform.TransformPoint(new Vector3(x, 50f, z));

            Vector3 groundPos;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200f, groundMask))
                groundPos = hit.point;
            else
                groundPos = transform.TransformPoint(new Vector3(x, 0f, z)); // fallback: flat at this object's Y

            float yRot = (float)rng.NextDouble() * 360f;
            float heightScale = Mathf.Lerp(minHeight, maxHeight, (float)rng.NextDouble());
            float widthScale = Mathf.Lerp(minWidth, maxWidth, (float)rng.NextDouble());

            Matrix4x4 m = Matrix4x4.TRS(
                groundPos,
                Quaternion.Euler(0f, yRot, 0f),
                new Vector3(widthScale, heightScale, widthScale));

            current.Add(m);

            if (current.Count == BATCH_SIZE)
            {
                batches.Add(current.ToArray());
                current.Clear();
            }
        }

        if (current.Count > 0)
            batches.Add(current.ToArray());
    }

    // ---------------------------------------------------------------
    // GenerateDefaultBladeMesh — a tapered strip with vertex colour
    // baked in as the wind height-mask (r = 0 at the base, r = 1 at
    // the tip). Lets you skip authoring a mesh in Blender entirely
    // for a first pass — swap in a real mesh later if you want
    // curvature or a less uniform silhouette.
    // ---------------------------------------------------------------
    public static Mesh GenerateDefaultBladeMesh(int segments = 3, float baseWidth = 0.1f)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var tris = new List<int>();

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;       // 0 at base, 1 at tip
            float width = baseWidth * (1f - t);   // taper to a point
            float y = t;                          // unit height, scaled via transform later

            verts.Add(new Vector3(-width * 0.5f, y, 0f));
            verts.Add(new Vector3(width * 0.5f, y, 0f));
            uvs.Add(new Vector2(0f, t));
            uvs.Add(new Vector2(1f, t));
            colors.Add(new Color(t, t, t, 1f));
            colors.Add(new Color(t, t, t, 1f));

            if (i < segments)
            {
                int baseIdx = i * 2;
                tris.Add(baseIdx); tris.Add(baseIdx + 2); tris.Add(baseIdx + 1);
                tris.Add(baseIdx + 1); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
            }
        }

        Mesh mesh = new Mesh { name = "GeneratedGrassBlade" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.4f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(areaSize.x, 0.01f, areaSize.y));
    }
#endif
}
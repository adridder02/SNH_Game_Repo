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
//
// FIX: instance generation (including all the ground raycasts) is
// now spread across multiple frames with a per-frame budget instead
// of running as one big synchronous loop in a single Update() call.
// For large/dense fields the old approach could fire thousands of
// Physics.Raycast calls in one frame, causing a frame-time spike
// large enough to visibly snap anything driven by Time.deltaTime
// elsewhere in the scene (e.g. camera smoothing/lerps).
// =============================================================

using System.Collections;
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

    [Header("Performance")]
    [Tooltip("Max ground raycasts to perform per frame while (re)generating. Keeps large/dense fields from spiking frame time.")]
    public int maxRaycastsPerFrame = 200;

    private List<Matrix4x4[]> batches = new List<Matrix4x4[]>();
    private bool dirty = true;
    private Coroutine generationRoutine;

    private void OnValidate() => dirty = true;
    private void OnEnable() => dirty = true;

    private void OnDisable()
    {
        if (generationRoutine != null)
        {
            StopCoroutine(generationRoutine);
            generationRoutine = null;
        }
    }

    private void Update()
    {
        if (dirty)
        {
            if (bladeMesh == null)
                bladeMesh = GenerateDefaultBladeMesh();

            dirty = false;

            // FIX: don't block the frame with a huge synchronous raycast loop.
            // In Play mode, spread the work across frames via coroutine.
            // In edit mode (no coroutines outside Play), fall back to a
            // frame-budgeted manual pump so the editor still doesn't hitch
            // as hard on huge fields (see EditorApplication hookup below).
            if (generationRoutine != null)
                StopCoroutine(generationRoutine);

            if (Application.isPlaying)
            {
                generationRoutine = StartCoroutine(GenerateInstancesBudgeted());
            }
            else
            {
                // Edit-mode preview: still budget it, just pump manually
                // across editor updates instead of using a coroutine.
                StartEditModeGeneration();
            }
        }

        if (grassMaterial == null || bladeMesh == null) return;

        foreach (Matrix4x4[] batch in batches)
            Graphics.DrawMeshInstanced(bladeMesh, 0, grassMaterial, batch, batch.Length,
                null, UnityEngine.Rendering.ShadowCastingMode.Off, false);
    }

    // ---------------------------------------------------------------
    // Budgeted generation: scatters points across areaSize, raycasts
    // down to find ground height, and bakes results into fixed-size
    // batches ready for DrawMeshInstanced — but only processes up to
    // maxRaycastsPerFrame points before yielding to the next frame.
    // ---------------------------------------------------------------
    private IEnumerator GenerateInstancesBudgeted()
    {
        int count = Mathf.Max(0, Mathf.RoundToInt(areaSize.x * areaSize.y * densityPerSqm));

        List<Matrix4x4[]> newBatches = new List<Matrix4x4[]>();

        if (count == 0)
        {
            batches = newBatches;
            yield break;
        }

        System.Random rng = new System.Random(randomSeed);
        List<Matrix4x4> current = new List<Matrix4x4>(BATCH_SIZE);
        int processedThisFrame = 0;

        for (int i = 0; i < count; i++)
        {
            AddBladeInstance(rng, current);
            processedThisFrame++;

            if (current.Count == BATCH_SIZE)
            {
                newBatches.Add(current.ToArray());
                current.Clear();
            }

            if (processedThisFrame >= maxRaycastsPerFrame)
            {
                processedThisFrame = 0;
                yield return null; // give the frame back
            }
        }

        if (current.Count > 0)
            newBatches.Add(current.ToArray());

        // Swap in the finished result atomically so we never render a
        // half-built set while still generating.
        batches = newBatches;
        generationRoutine = null;
    }

    private void AddBladeInstance(System.Random rng, List<Matrix4x4> current)
    {
        float x = ((float)rng.NextDouble() - 0.5f) * areaSize.x;
        float z = ((float)rng.NextDouble() - 0.5f) * areaSize.y;
        Vector3 origin = transform.TransformPoint(new Vector3(x, 50f, z));

        Vector3 groundPos;
        // FIX: explicitly ignore triggers. Physics.Raycast otherwise follows
        // the global "Queries Hit Triggers" project setting, which can cause
        // ground-height sampling to land on trigger volumes (e.g. gameplay
        // triggers, camera collision boxes) instead of actual ground geometry.
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200f, groundMask, QueryTriggerInteraction.Ignore))
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
    }

#if UNITY_EDITOR
    // Edit-mode (not playing) budgeted generation, driven by the editor's
    // update loop rather than a coroutine (coroutines don't tick outside Play).
    private void StartEditModeGeneration()
    {
        System.Random rng = new System.Random(randomSeed);
        int count = Mathf.Max(0, Mathf.RoundToInt(areaSize.x * areaSize.y * densityPerSqm));
        int index = 0;
        List<Matrix4x4[]> newBatches = new List<Matrix4x4[]>();
        List<Matrix4x4> current = new List<Matrix4x4>(BATCH_SIZE);

        if (count == 0)
        {
            batches = newBatches;
            return;
        }

        void Step()
        {
            int processed = 0;
            while (index < count && processed < maxRaycastsPerFrame)
            {
                AddBladeInstance(rng, current);
                if (current.Count == BATCH_SIZE)
                {
                    newBatches.Add(current.ToArray());
                    current.Clear();
                }
                index++;
                processed++;
            }

            if (index >= count)
            {
                if (current.Count > 0) newBatches.Add(current.ToArray());
                batches = newBatches;
                UnityEditor.EditorApplication.update -= stepHandler;
            }
        }

        stepHandler = Step;
        UnityEditor.EditorApplication.update += stepHandler;
    }

    private UnityEditor.EditorApplication.CallbackFunction stepHandler;
#else
    private void StartEditModeGeneration()
    {
        // Builds don't hit this path (Update only calls it when !Application.isPlaying,
        // which cannot happen in a standalone build), kept as a no-op for safety.
    }
#endif

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
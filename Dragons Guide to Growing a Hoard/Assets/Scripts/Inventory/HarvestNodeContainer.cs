using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================
// HarvestNodeContainer.cs
// -------------------------------------------------------------
// Attach to the parent empty GameObject that holds all your
// harvest node meshes/objects as direct children.
//
// HOW IT WORKS:
//   • Every frame, finds the closest child object within
//     interactRange of the player.
//   • Shows a floating [E] prompt above that child.
//   • Press E to "harvest" it — logs a message and shows a
//     brief on-screen confirmation. Hook into your real
//     inventory system later by replacing the TODO comment
//     in OnHarvest().
//
// SETUP:
//   1. Attach this script to the parent empty.
//   2. Assign the Player transform in the Inspector.
//   3. That's it — all children are detected automatically.
//      No per-node components needed.
// =============================================================

public class HarvestNodeContainer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The player's transform. Drag your Player GameObject here.")]
    public Transform player;

    [Tooltip("The player's inventory. Auto-found in Start() if left empty.")]
    public PlayerInventory playerInventory;

    [Header("Interaction")]
    [Tooltip("How close the player must be to a node to see the prompt.")]
    public float interactRange = 2.5f;

    [Header("Prompt UI")]
    public string promptText = "[E] Harvest";
    public Vector2 promptWorldSize = new Vector2(0.7f, 0.22f);
    public float promptHeightOffset = 1.2f;

    [Header("Harvest Feedback")]
    [Tooltip("Text shown briefly on screen after harvesting.")]
    public string harvestMessage = "Harvested!";
    [Tooltip("How long the harvest message stays on screen (seconds).")]
    public float messageDuration = 1.8f;

    // ── private ──────────────────────────────────────────────
    private Transform[] nodes;

    // Prompt UI
    private GameObject promptRoot;
    private TextMeshProUGUI promptLabel;

    // Feedback UI
    private GameObject feedbackRoot;
    private TextMeshProUGUI feedbackLabel;
    private float feedbackTimer = 0f;

    private Transform currentNode = null;
    private Transform previousNode = null;
    private OutlineEffect currentOutline;

    // =========================================================
    private void Start()
    {
        if (playerInventory == null)
            playerInventory = FindObjectOfType<PlayerInventory>();

        CacheChildren();
        BuildPromptUI();
        BuildFeedbackUI();
    }

    // =========================================================
    // Re-cache if children are added/removed at runtime.
    public void CacheChildren()
    {
        nodes = new Transform[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
            nodes[i] = transform.GetChild(i);
    }

    // =========================================================
    private void Update()
    {
        if (player == null)
        {
            Debug.LogWarning("[HarvestNodeContainer] Player reference is not assigned!", this);
            return;
        }

        currentNode = FindClosestNode();

        if (currentNode != previousNode)
        {
            ClearCurrentOutline();
            ApplyOutlineFor(currentNode);
            previousNode = currentNode;
        }


        UpdatePrompt();
        UpdateFeedback();

        if (UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (currentNode != null)
                OnHarvest(currentNode);
            else
                Debug.Log($"[HarvestNodeContainer] E pressed but no node in range. " +
                          $"Checked {(nodes != null ? nodes.Length : 0)} nodes, " +
                          $"range={interactRange}, player pos={player.position}");
        }
    }
    
        // ---------------------------------------------------------------
    // Outline helpers — highlights whichever harvest node is currently
    // in range. Safe to call with a null node (does nothing).
    // ---------------------------------------------------------------
    private void ApplyOutlineFor(Transform node)
    {
        if (node == null) return;
        currentOutline = node.GetComponent<OutlineEffect>();
        currentOutline?.SetOutline(true);
    }
 
    private void ClearCurrentOutline()
    {
        currentOutline?.SetOutline(false);
        currentOutline = null;
    }

    // =========================================================
    private Transform FindClosestNode()
    {
        Transform best = null;
        float bestDist = interactRange;

        if (nodes == null || nodes.Length == 0)
        {
            Debug.LogWarning("[HarvestNodeContainer] No child nodes cached. " +
                             "Make sure children exist under this GameObject at Start().", this);
            return null;
        }

        foreach (Transform node in nodes)
        {
            if (node == null || !node.gameObject.activeSelf) continue;

            float d = Vector3.Distance(player.position, node.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = node;
            }
        }

        return best;
    }

    // Call this from the Inspector context menu to print all nodes and distances at runtime.
    [ContextMenu("Debug: Print Node Distances")]
    private void DebugPrintNodes()
    {
        if (player == null) { Debug.LogError("[HarvestNodeContainer] Player is null!"); return; }
        if (nodes == null || nodes.Length == 0) { Debug.LogError("[HarvestNodeContainer] No nodes cached — call CacheChildren first."); return; }

        Debug.Log($"[HarvestNodeContainer] {nodes.Length} nodes, interact range = {interactRange}");
        foreach (Transform node in nodes)
        {
            if (node == null) { Debug.Log("  - NULL node"); continue; }
            float d = Vector3.Distance(player.position, node.position);
            Debug.Log($"  - {node.name}  dist={d:F2}  active={node.gameObject.activeSelf}");
        }
    }

    // =========================================================
    private void OnHarvest(Transform node)
    {
        CollectablePlant plant = node.GetComponent<CollectablePlant>();
        if (plant == null)
        {
            Debug.LogWarning($"[HarvestNodeContainer] {node.name} has no CollectablePlant component — nothing to harvest.");
            return;
        }

        if (playerInventory == null)
        {
            Debug.LogWarning("[HarvestNodeContainer] No PlayerInventory found — cannot harvest.");
            return;
        }

        GameObject plantPrefab = plant.GetPlantPrefab();
        if (plantPrefab == null)
        {
            Debug.LogWarning($"[HarvestNodeContainer] {node.name} has no plant prefab assigned.");
            return;
        }

        // Same entry point the collision-based collection uses: tries the
        // grid first, falls back to Available if the grid has no room.
        // Pass the icon, detail image, and name too — plant prefabs are 3D and
        // have no SpriteRenderer, so these getters are the only source the
        // inventory slot / detail panel can display.
        playerInventory.AddPlantToInventory(plantPrefab, plant.GetPlantIcon(), plant.GetPlantImage(), plant.GetPlantName());

        Debug.Log($"[HarvestNodeContainer] Harvested: {node.name}");
        ShowFeedback($"{harvestMessage}  ({node.name})");
        if(!Tutorial.tutorialStageComplete()){
            Tutorial.removedPlant();
            Tutorial.addedPotToInventory();
        }
        // Node is done for now. Swap for a respawn-timer coroutine if these
        // nodes should regrow rather than disappear permanently.
        ClearCurrentOutline();
        node.gameObject.SetActive(false);

      
    }

    // =========================================================
    // PROMPT UI
    // =========================================================
    private void BuildPromptUI()
    {
        promptRoot = new GameObject("HarvestPrompt");
        DontDestroyOnLoad(promptRoot);

        Canvas c = promptRoot.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.sortingOrder = 50;

        RectTransform rt = promptRoot.GetComponent<RectTransform>();
        rt.sizeDelta = promptWorldSize * 100f;
        rt.localScale = Vector3.one * 0.01f;

        CanvasScaler cs = promptRoot.AddComponent<CanvasScaler>();
        cs.dynamicPixelsPerUnit = 10f;
        promptRoot.AddComponent<GraphicRaycaster>();

        // Background
        GameObject panel = MakeRect("Panel", promptRoot.transform);
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = pr.offsetMax = Vector2.zero;
        panel.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.08f, 0.85f);

        // Label
        GameObject textGO = MakeRect("Label", panel.transform);
        RectTransform tr = textGO.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;

        promptLabel = textGO.AddComponent<TextMeshProUGUI>();
        promptLabel.text = promptText;
        promptLabel.fontSize = 14;
        promptLabel.fontStyle = FontStyles.Bold;
        promptLabel.color = new Color(0.9f, 0.95f, 0.9f);
        promptLabel.alignment = TextAlignmentOptions.Center;

        promptRoot.SetActive(false);
    }

    private void UpdatePrompt()
    {
        if (promptRoot == null) return;

        bool show = currentNode != null;
        promptRoot.SetActive(show);

        if (!show) return;

        // Position above the node
        promptRoot.transform.position =
            currentNode.position + Vector3.up * promptHeightOffset;

        // Billboard
        if (Camera.main != null)
        {
            Vector3 toCamera = Camera.main.transform.position - promptRoot.transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.0001f)
                promptRoot.transform.rotation = Quaternion.LookRotation(-toCamera);
        }
    }

    // =========================================================
    // FEEDBACK UI  (screen-space, centred)
    // =========================================================
    private void BuildFeedbackUI()
    {
        feedbackRoot = new GameObject("HarvestFeedback");
        DontDestroyOnLoad(feedbackRoot);

        Canvas c = feedbackRoot.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 200;
        feedbackRoot.AddComponent<CanvasScaler>();
        feedbackRoot.AddComponent<GraphicRaycaster>();

        GameObject textGO = MakeRect("Label", feedbackRoot.transform);
        RectTransform tr = textGO.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.5f, 0.6f);
        tr.anchorMax = new Vector2(0.5f, 0.6f);
        tr.sizeDelta = new Vector2(400f, 60f);
        tr.anchoredPosition = Vector2.zero;

        feedbackLabel = textGO.AddComponent<TextMeshProUGUI>();
        feedbackLabel.fontSize = 22;
        feedbackLabel.fontStyle = FontStyles.Bold;
        feedbackLabel.color = new Color(0.55f, 1f, 0.55f);
        feedbackLabel.alignment = TextAlignmentOptions.Center;

        feedbackRoot.SetActive(false);
    }

    private void ShowFeedback(string message)
    {
        if (feedbackRoot == null) return;
        feedbackLabel.text = message;
        feedbackRoot.SetActive(true);
        feedbackTimer = messageDuration;
    }

    private void UpdateFeedback()
    {
        if (feedbackTimer <= 0f) return;

        feedbackTimer -= Time.deltaTime;

        // Fade out in the last 0.5 s
        float alpha = Mathf.Clamp01(feedbackTimer / 0.5f);
        feedbackLabel.color = new Color(
            feedbackLabel.color.r,
            feedbackLabel.color.g,
            feedbackLabel.color.b,
            alpha);

        if (feedbackTimer <= 0f)
            feedbackRoot.SetActive(false);
    }

    // =========================================================
    private void OnDestroy()
    {
        if (promptRoot != null) Destroy(promptRoot);
        if (feedbackRoot != null) Destroy(feedbackRoot);
    }

    private static GameObject MakeRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (player == null) return;
        Gizmos.color = new Color(0.8f, 1f, 0.3f, 0.15f);
        Gizmos.DrawSphere(player.position, interactRange);
        Gizmos.color = new Color(0.8f, 1f, 0.3f, 0.6f);
        Gizmos.DrawWireSphere(player.position, interactRange);
    }
#endif
}
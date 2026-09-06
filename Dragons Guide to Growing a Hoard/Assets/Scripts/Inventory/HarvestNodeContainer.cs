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
//   3. Assign promptTemplate — an InteractPromptView prefab (background +
//      keybind icon + label, built once — see InteractPromptView.cs). This
//      is the exact same component PotInteraction.cs uses for its own
//      prompt, so you can reuse that same prefab here for matching art,
//      or make a dedicated one for harvest nodes.
//   4. That's it — all children are detected automatically.
//      No per-node components needed.
// =============================================================

public class HarvestNodeContainer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The player's transform. Drag your Player GameObject here.")]
    public Transform player;

    [Tooltip("The player's inventory. Auto-found in Start() if left empty.")]
    public PlayerInventory playerInventory;

    [Header("Mission")]
    [Tooltip("This mission's 'find_node' task completes the first time a node comes into range. " +
             "Assign the same MissionData asset used on CollectablePlant/PotInteraction, and make sure " +
             "a task with Task Id 'find_node' exists on it.")]
    [SerializeField] private MissionData tutorialMission;

    [Header("Interaction")]
    [Tooltip("How close the player must be to a node to see the prompt.")]
    public float interactRange = 2.5f;

    [Header("Prompt UI")]
    [Tooltip("ON = the floating world-space prompt below (promptTemplate) that follows the node and " +
             "billboards toward the camera. OFF = the fixed screen-space prompt on the main HUD instead " +
             "(the same interactPromptHUD PotInteraction uses) — just enabled/disabled, no positioning " +
             "or billboarding. See MainUIController.SetInteractPromptVisible().")]
    public bool useWorldSpacePrompt = true;

    [Tooltip("Prefab defining the prompt's look (background/keybind icon/label — see InteractPromptView.cs), " +
             "built once in the Editor. Same component PotInteraction.cs uses for its own prompt — you can " +
             "reuse that exact prefab here, or make a separate one for harvest nodes. Only used when " +
             "useWorldSpacePrompt is ON.")]
    public InteractPromptView promptTemplate;

    [Tooltip("Label text shown on the prompt, e.g. '[E] Harvest'. Leave empty to keep whatever's already " +
             "set on the template's Label. Only used when useWorldSpacePrompt is ON.")]
    public string promptText = "[E] Harvest";

    [Tooltip("Sprite for the keybind icon, if the template has one. Leave null to keep whatever's already " +
             "set on the template's KeybindIcon. Only used when useWorldSpacePrompt is ON.")]
    public Sprite promptKeybindSprite;

    [Tooltip("Height above the node for the interact prompt. Only used when useWorldSpacePrompt is ON.")]
    public float promptHeightOffset = 1.2f;

    [Tooltip("The main HUD controller whose fixed interactPromptHUD element gets shown/hidden instead, " +
             "when useWorldSpacePrompt is OFF. Auto-found in the scene if left empty — the same instance " +
             "PotInteraction uses, since it's the same shared HUD element.")]
    public MainUIController mainUI;

    [Header("Harvest Feedback")]
    [Tooltip("Text shown briefly on screen after harvesting.")]
    public string harvestMessage = "Harvested!";

    // ── private ──────────────────────────────────────────────
    private Transform[] nodes;

    // Prompt UI
    private GameObject promptRoot;
    private InteractPromptView promptView;

    // Feedback UI is now driven by MainUIController.ShowHarvestFeedback (see mainUI field below) —
    // this used to build its own floating runtime canvas here, replaced with a proper HUD element.

    private Transform currentNode = null;
    private Transform previousNode = null;
    private OutlineEffect currentOutline;

    // =========================================================
    private void Start()
    {
        if (playerInventory == null)
            playerInventory = FindAnyObjectByType<PlayerInventory>();

        CacheChildren();

        if (useWorldSpacePrompt)
            BuildPromptUI();

        // Needed unconditionally now, not just when useWorldSpacePrompt is off — ShowHarvestFeedback
        // (the harvest confirmation popup) always goes through MainUIController regardless of which
        // interact-prompt style is in use.
        if (mainUI == null)
        {
            mainUI = FindAnyObjectByType<MainUIController>();
            if (mainUI == null)
                Debug.LogWarning("[HarvestNodeContainer] No MainUIController found/assigned — harvest " +
                                  "feedback popups (and the HUD interact prompt, if useWorldSpacePrompt " +
                                  "is off) won't show.", this);
        }
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
    private void OnDisable()
    {
        mainUI?.SetInteractPromptVisible(false, this);
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

            if (currentNode != null && tutorialMission != null)
                MissionProgressManager.Instance?.CompleteOrderedTask(tutorialMission, "find_node");
        }


        // Show/hide now; position/billboard happen in LateUpdate (see below), after the player has
        // finished moving for this frame — doing it here in Update() reads a stale position on frames
        // where movement hasn't been applied yet, which shows up as flicker/jitter.
        UpdatePromptVisibility();

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

    private void LateUpdate()
    {
        // Runs after every script's Update() this frame (including whatever moves the player), so this
        // always reads the final position for the frame instead of a half-step-stale one.
        UpdatePromptTransform();
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
        // plant_pickup is completed inside CollectablePlant.GetPlantPrefab() (called above via
        // plant.GetPlantPrefab()) — that's the single funnel point for every pickup path (harvest
        // node here, and the physical-collision path in PlayerInventory), so it's not repeated here.
        // NOTE: this used to also complete an "AddedPotToInventory" checklist task —
        // that task was dropped from the mission, so there's nothing to call anymore.

        // Node is done for now. Swap for a respawn-timer coroutine if these
        // nodes should regrow rather than disappear permanently.
        ClearCurrentOutline();
        node.gameObject.SetActive(false);
        // NOTE: this used to advance the old on-screen Tutorial instruction text here
        // (Tutorial_1.Instance.OnPickUpPot()) — not a checklist task, nothing to repoint
        // it to yet since that on-screen system hasn't been rebuilt.
      
    }

    // =========================================================
    // PROMPT UI
    // =========================================================
    private void BuildPromptUI()
    {
        if (promptTemplate == null)
        {
            Debug.LogWarning("[HarvestNodeContainer] promptTemplate is not assigned — no interact prompt " +
                              "will be shown. Assign an InteractPromptView prefab (background + keybind " +
                              "icon + label) in the Inspector — PotInteraction's prompt prefab works fine here too.", this);
            return;
        }

        promptView = Instantiate(promptTemplate);
        promptRoot = promptView.gameObject;
        promptRoot.name = "HarvestPrompt";
        promptRoot.transform.SetParent(null);
        DontDestroyOnLoad(promptRoot);

        if (promptView.canvas != null)
            promptView.canvas.renderMode = RenderMode.WorldSpace;

        if (promptView.keybindIcon != null && promptKeybindSprite != null)
        {
            promptView.keybindIcon.sprite = promptKeybindSprite;
            promptView.keybindIcon.enabled = true;
        }

        if (promptView.label != null && !string.IsNullOrEmpty(promptText))
            promptView.label.text = promptText;

        promptRoot.SetActive(false);
    }

    private void UpdatePromptVisibility()
    {
        bool shouldShow = currentNode != null;

        if (useWorldSpacePrompt)
        {
            if (promptRoot != null)
                promptRoot.SetActive(shouldShow);
        }
        else
        {
            mainUI?.SetInteractPromptVisible(shouldShow, this);
        }
    }

    private void UpdatePromptTransform()
    {
        // Only the world-space prompt needs positioning/billboarding — the HUD version is a fixed
        // screen-space element that MainUIController just enables/disables.
        if (!useWorldSpacePrompt) return;
        if (promptRoot == null || !promptRoot.activeSelf || currentNode == null) return;

        // Position above the NODE (not the player) — with multiple nodes potentially in range, this is
        // what tells you which specific one is about to be harvested.
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
    // FEEDBACK — now just forwards to MainUIController's HUD popup (see ShowHarvestFeedback there).
    // messageDuration above is no longer used here; MainUIController has its own duration field.
    // =========================================================
    private void ShowFeedback(string message)
    {
        mainUI?.ShowHarvestFeedback(message);
    }

    // =========================================================
    private void OnDestroy()
    {
        if (promptRoot != null) Destroy(promptRoot);
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
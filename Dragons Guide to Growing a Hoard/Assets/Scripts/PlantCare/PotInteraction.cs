// =============================================================
// PotInteraction.cs
// -------------------------------------------------------------
// Attach this script to the Player.
//
// HOW IT WORKS:
//   - Each frame, a sphere cast finds the nearest PotContents
//     within interactRange.
//   - Press E to open a simple on-screen context menu for that pot.
//   - The menu shows only the actions that make sense right now:
//       • No soil        → "Add Soil" (pick type)
//       • Has soil       → "Change Soil" (pick type)
//       • Has soil, no plant → "Add Plant" (pick from dragonInventory list)
//       • Has plant      → "Water Plant"
//   - Clicking a menu button executes the action and closes the menu.
//   - Press E again or walk away to close without acting.
//
// CHANGES:
//   • AddPlant now reads the bool return from PotContents.AddPlant()
//     and shows a size-mismatch warning label in the menu when the
//     player tries to plant a wrong-size plant.
//   • Plant list items now display the plant's size in brackets so
//     the player can see at a glance which plants fit the current pot.
//   • A "Pot size: X" header line is shown in the plant section.
//
// SETUP:
//   1. Attach to the Player GameObject.
//   2. Fill dragonInventory with your plant prefabs (need PlantState).
//   3. Adjust interactRange so it feels natural.
//   4. Optionally set a UI Canvas for the interaction prompt.
//
// NOTE ON SOIL:
//   Soil types are placeholders — SoilKind.Clay / Loam / Sandy.
//   Swap the names in the SoilKind enum (SoilType.cs) to match
//   your real soil variety names when you have them.
// =============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class PotInteraction : MonoBehaviour
{
    // ---------------------------------------------------------------
    // INSPECTOR
    // ---------------------------------------------------------------
    [Header("Range")]
    [Tooltip("How close the player must be to interact with a pot.")]
    public float interactRange = 3f;

    [Header("Plant Prefabs")]
    [Tooltip("List of plant prefabs the player can add to a pot. " +
             "Each must have a PlantState component attached.")]
    public PlayerInventory dragonInventory;

    [Header("Watering")]
    [Tooltip("How much water is added per Q press when watering.")]
    public float waterPerPress = 2f;

    [Tooltip("Maximum water the player can hold. Refills automatically.")]
    public float maxWaterPool = 20f;

    [Tooltip("Rate at which the player's water pool refills per second.")]
    public float poolRefillRate = 1.5f;

    [Header("UI — Interaction Prompt")]
    [Tooltip("Display '[E] Interact' prompt above pots when in range.")]
    public bool showInteractPrompt = true;

    [Tooltip("Height offset above the pot for the interact prompt.")]
    public float promptHeightOffset = 1.5f;

    [Tooltip("World-space size of the interact prompt.")]
    public Vector2 promptWorldSize = new Vector2(0.6f, 0.2f);

    // ---------------------------------------------------------------
    // Private state
    // ---------------------------------------------------------------
    private float playerWaterPool;
    private PotContents nearbyPot;
    private bool menuOpen = false;
    
    // The OutlineEffect currently switched on, if any. Tracked here so we
    // always know exactly which one to turn off — never rely on scanning
    // for "whatever's outlined right now."
    private OutlineEffect currentOutline;


    // Transient feedback message shown inside the open menu.
    // Cleared each time the menu rebuilds.
    private string pendingFeedbackMessage = null;

    // ── Runtime UI (built in code) ─────────────────────────────────
    private GameObject menuRoot;
    private Canvas menuCanvas;
    private const float BTN_WIDTH = 240f;
    private const float BTN_HEIGHT = 38f;
    private const float BTN_GAP = 6f;

    // Interact prompt UI
    private GameObject promptRoot;
    private Canvas promptCanvas;
    private TextMeshProUGUI promptText;

    // ---------------------------------------------------------------
    // Start
    // ---------------------------------------------------------------
    private void Start()
    {
        // Find the PlayerInventory component on the same GameObject or in the scene
        if (dragonInventory == null)
        {
            dragonInventory = GetComponent<PlayerInventory>();
            
            if (dragonInventory == null)
            {
                dragonInventory = FindObjectOfType<PlayerInventory>();
                
                if (dragonInventory == null)
                {
                    Debug.LogError("PlayerInventory not found! Please assign it in the Inspector.");
                }
            }
        }
        
        playerWaterPool = dragonInventory.getWaterPool();
        BuildMenuCanvas();
        if (showInteractPrompt) BuildInteractPrompt();
        CloseMenu();
    }

    private void OnDisable()
    {
        ClearCurrentOutline();
    }

    // ---------------------------------------------------------------
    // Update — scan for pots, handle E, handle Q watering
    // ---------------------------------------------------------------
    private void Update()
    {
        // Passive water refill
        //playerWaterPool = Mathf.Min(playerWaterPool + poolRefillRate * Time.deltaTime, maxWaterPool);

        // Scan for nearest pot
        PotContents found = FindNearestPot();

        // If the pot under focus changes, close any open menu
        if (found != nearbyPot)
        {
            if (menuOpen) CloseMenu();

            ClearCurrentOutline();
            nearbyPot = found;
            ApplyOutlineFor(nearbyPot);
        }

        // Update interact prompt position and visibility
        UpdateInteractPrompt();

        // E — toggle menu
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (menuOpen)
                CloseMenu();
            else if (nearbyPot != null)
                OpenMenu(nearbyPot);
        }

        // Q — quick water (works whether menu is open or not)
        if (Keyboard.current.qKey.wasPressedThisFrame && nearbyPot != null)
            QuickWater(nearbyPot);
    }

    // ---------------------------------------------------------------
    // Pot scanning
    // ---------------------------------------------------------------
    private PotContents FindNearestPot()
    {
        if (menuOpen && nearbyPot != null) return nearbyPot; // freeze while menu open

        PotContents best = null;
        float bestD = float.MaxValue;

        Collider[] hits = Physics.OverlapSphere(transform.position, interactRange);
        foreach (Collider c in hits)
        {
            PotContents pot = c.GetComponent<PotContents>()
                           ?? c.GetComponentInParent<PotContents>();
            if (pot == null) continue;

            float d = Vector3.Distance(transform.position, pot.transform.position);
            if (d < bestD) { bestD = d; best = pot; }
        }
        return best;
    }

    // ---------------------------------------------------------------
    // Outline helpers — only the plant gets outlined, and only if the
    // pot actually has one. Safe to call with a null/plantless pot.
    // ---------------------------------------------------------------
    private void ApplyOutlineFor(PotContents pot)
    {
        if (pot == null || !pot.HasPlant || pot.Plant == null) return;

        currentOutline = pot.Plant.GetComponent<OutlineEffect>();
        currentOutline?.SetOutline(true);
    }

    private void ClearCurrentOutline()
    {
        if (currentOutline == null) return;
        currentOutline.SetOutline(false);
        currentOutline = null;
    }

    // ---------------------------------------------------------------
    // Quick water (Q key)
    // ---------------------------------------------------------------
    private void QuickWater(PotContents pot)
    {
        if (!pot.HasPlant)
        {
            Debug.Log("[PotInteraction] No plant in this pot — nothing to water.");
            return;
        }
        if (dragonInventory.getWaterPool() <= 0f)
        {
            Debug.Log("[PotInteraction] Water pool empty. Wait for refill.");
            return;
        }
        float transfer = Mathf.Min(waterPerPress, dragonInventory.getWaterPool());
        if (pot.AddWater(transfer)){
            dragonInventory.reduceWaterPool(transfer);   
        }
        else
            Debug.Log("[PotInteraction] Pot is already full.");
    }

    // ===============================================================
    // INTERACT PROMPT — [E] floating above the pot
    // ===============================================================

    private void BuildInteractPrompt()
    {
        // Create a world-space canvas that we'll reposition each frame
        promptRoot = new GameObject("InteractPrompt");
        DontDestroyOnLoad(promptRoot);

        promptCanvas = promptRoot.AddComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.WorldSpace;
        promptCanvas.sortingOrder = 50;

        RectTransform canvasRect = promptRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = promptWorldSize * 100f; // High res
        canvasRect.localScale = Vector3.one * 0.01f;   // Scale to world size

        CanvasScaler scaler = promptRoot.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        promptRoot.AddComponent<GraphicRaycaster>();

        // Background panel
        GameObject panel = CreateUIElement("Panel", promptRoot.transform);
        RectTransform panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;
        Image panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.1f, 0.12f, 0.1f, 0.85f);

        // Text label
        GameObject textGO = CreateUIElement("Text", panel.transform);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        promptText = textGO.AddComponent<TextMeshProUGUI>();
        promptText.text = "[E] Interact";
        promptText.fontSize = 14;
        promptText.color = new Color(0.9f, 0.95f, 0.9f);
        promptText.fontStyle = FontStyles.Bold;
        promptText.alignment = TextAlignmentOptions.Center;

        promptRoot.SetActive(false); // Hidden by default
    }

    private void UpdateInteractPrompt()
    {
        if (!showInteractPrompt || promptRoot == null) return;

        bool shouldShow = nearbyPot != null && !menuOpen;
        promptRoot.SetActive(shouldShow);

        if (shouldShow)
        {
            // Position above the pot
            Vector3 potPos = nearbyPot.transform.position;
            promptRoot.transform.position = potPos + Vector3.up * promptHeightOffset;

            // Billboard toward camera
            if (Camera.main != null)
            {
                Vector3 toCamera = Camera.main.transform.position - promptRoot.transform.position;
                toCamera.y = 0f;
                if (toCamera.sqrMagnitude > 0.0001f)
                {
                    promptRoot.transform.rotation = Quaternion.LookRotation(-toCamera);
                }
            }
        }
    }

    // ===============================================================
    // MENU — build, open, close, populate
    // ===============================================================

    private void BuildMenuCanvas()
    {
        // Screen-space overlay canvas so it renders on top of everything
        menuRoot = new GameObject("PotInteractionMenu");
        DontDestroyOnLoad(menuRoot); // persists through scene loads

        menuCanvas = menuRoot.AddComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        menuCanvas.sortingOrder = 100;

        menuRoot.AddComponent<CanvasScaler>();
        menuRoot.AddComponent<GraphicRaycaster>();
    }

    private void OpenMenu(PotContents pot)
    {
        // Add this null check at the VERY START
        if (pot == null || pot.gameObject == null)
        {
            Debug.LogWarning("Tried to open menu for a destroyed or null pot");
            return;
        }
        GameInputModeManager.Instance?.SetPlacementMode();
        ThirdPersonCameraController.CameraLocked = true;

        // Clear old buttons
        foreach (Transform child in menuRoot.transform)
            Destroy(child.gameObject);

        // Build action list based on pot state
        List<(string label, System.Action action, bool isWarning)> actions = new();

        // ── Feedback message (e.g. size mismatch) ────────────────
        if (pendingFeedbackMessage != null)
        {
            actions.Add((pendingFeedbackMessage, null, true)); // warning row
            pendingFeedbackMessage = null;
        }

        // ── Soil actions ──────────────────────────────────────────
        string soilHeader = pot.HasSoil ? "Change Soil:" : "Add Soil:";
        actions.Add((soilHeader, null, false)); // section header (non-clickable)

        foreach (SoilKind kind in System.Enum.GetValues(typeof(SoilKind)))
        {
            SoilKind captured = kind; // closure capture
            string indicator = (pot.HasSoil && pot.CurrentSoil == captured) ? " ✓" : "";
            actions.Add(($"  {SoilDisplayName(captured)}{indicator}", () =>
            {
                pot.SetSoil(captured);
                OpenMenu(pot); // Re-open to reflect new state
            }, false));
        }

        // ── Plant actions ─────────────────────────────────────────
        if (pot != null && pot.HasSoil)
        {
            if (!pot.HasPlant)
            {
                if (dragonInventory != null && dragonInventory.GetInventory().Count > 0)
                {
                    string potSizeHeader = pot.IsStatic
                        ? "Add Plant:  (accepts any size)"
                        : $"Add Plant:  (pot size: {pot.PotSize})";
                    actions.Add((potSizeHeader, null, false));

                    foreach (GameObject prefab in dragonInventory.GetInventory())
                    {
                        if (prefab == null) continue; // Skip null prefabs
                        
                        GameObject captured = prefab;
                        PlantState ps = prefab.GetComponent<PlantState>();
                        string plantName = prefab.name.Replace("(Clone)", "").Trim();

                        string sizeTag = ps != null ? $"[{ps.plantSize}]" : "[?]";

                        bool fits = pot.IsStatic || (ps != null && ps.plantSize == pot.PotSize);
                        string prefix = fits ? "  " : "  ✗ ";

                        actions.Add(($"{prefix}{plantName} {sizeTag}", () =>
                        {
                            if (pot == null || pot.gameObject == null)
                            {
                                Debug.LogWarning("Pot was destroyed before plant could be added");
                                CloseMenu();
                                return;
                            }
                            
                            bool success = pot.AddPlant(captured);
                            if (success)
                            {
                                dragonInventory.RemoveFirstPlant(captured);
                                CloseMenu();

                                // We're standing right at this pot, so reflect
                                // the new plant in the outline immediately.
                                if (pot == nearbyPot)
                                    ApplyOutlineFor(pot);
                            }
                            else
                            {
                                string plantSizeStr = ps != null
                                    ? ps.plantSize.ToString()
                                    : "Unknown";
                                pendingFeedbackMessage =
                                    $"⚠  {plantName} is {plantSizeStr} — pot needs {pot.PotSize}";
                                OpenMenu(pot);
                            }
                        }, false));
                    }
                }
                else
                {
                    actions.Add(("[No plants Available]", null, false));
                }
            }
            else
            {
                // Has plant — offer watering and removal
                actions.Add(($"Water Plant  (pool: {dragonInventory.getWaterPool():F1})", () =>
                {
                    if (pot != null) QuickWater(pot);
                    if (pot != null && pot.gameObject != null) OpenMenu(pot);
                    else CloseMenu();
                }, false));

            System.Action removePlant = () =>
            {
                ClearCurrentOutline();
                pot.RemovePlant(dragonInventory);
                CloseMenu();
            };
            actions.Add(("Remove Plant", removePlant, false));
            }
        }

        // ── Close button ──────────────────────────────────────────
        actions.Add(("[ Close ]", CloseMenu, false));

        // ── Layout ────────────────────────────────────────────────
        float totalHeight = actions.Count * (BTN_HEIGHT + BTN_GAP) - BTN_GAP + 16f;
        float startY = totalHeight * 0.5f - 8f;

        // Panel background
        GameObject panel = CreateUIElement("Panel", menuRoot.transform);
        RectTransform panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(BTN_WIDTH + 20f, totalHeight);
        panelRT.anchoredPosition = Vector2.zero;
        Image panelImg = panel.AddComponent<Image>();
        panelImg.color = Color.black;

        for (int i = 0; i < actions.Count; i++)
        {
            var (label, action, isWarning) = actions[i];
            float y = startY - i * (BTN_HEIGHT + BTN_GAP);
            bool isHeader = action == null;

            if (isHeader)
            {
                GameObject lblGO = CreateUIElement(label, panel.transform);
                RectTransform lr = lblGO.GetComponent<RectTransform>();
                lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
                lr.sizeDelta = new Vector2(BTN_WIDTH, BTN_HEIGHT);
                lr.anchoredPosition = new Vector2(0f, y);
                TextMeshProUGUI txt = lblGO.AddComponent<TextMeshProUGUI>();
                txt.text = label;
                txt.fontSize = 12;
                txt.color = isWarning ? new Color(1f, 0.4f, 0.4f) : Color.white;
                txt.fontStyle = FontStyles.Bold;
                txt.alignment = TextAlignmentOptions.MidlineLeft;
            }
            else
            {
                GameObject btnGO = CreateUIElement("Btn_" + i, panel.transform);
                RectTransform br = btnGO.GetComponent<RectTransform>();
                br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f);
                br.sizeDelta = new Vector2(BTN_WIDTH, BTN_HEIGHT);
                br.anchoredPosition = new Vector2(0f, y);

                Image bg = btnGO.AddComponent<Image>();
                bg.color = Color.black;

                Button btn = btnGO.AddComponent<Button>();
                System.Action captured = action;
                btn.onClick.AddListener(() => captured?.Invoke());

                ColorBlock cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(0.75f, 0.75f, 0.75f);
                cb.pressedColor = new Color(0.5f, 0.5f, 0.5f);
                btn.colors = cb;
                btn.targetGraphic = bg;

                GameObject txtGO = CreateUIElement("Label", btnGO.transform);
                RectTransform tr = txtGO.GetComponent<RectTransform>();
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
                tr.offsetMin = new Vector2(8f, 0f); tr.offsetMax = Vector2.zero;
                TextMeshProUGUI txt = txtGO.AddComponent<TextMeshProUGUI>();
                txt.text = label;
                txt.fontSize = 11;
                txt.color = label.StartsWith("  ✗") ? new Color(0.6f, 0.6f, 0.6f) : Color.white;
                txt.alignment = TextAlignmentOptions.MidlineLeft;
            }
        }

        menuOpen = true;
    }

    private void CloseMenu()
    {
        GameInputModeManager.Instance?.SetGameplayMode();
        ThirdPersonCameraController.CameraLocked = false;

        foreach (Transform child in menuRoot.transform)
            Destroy(child.gameObject);
        menuOpen = false;
    }

    // ---------------------------------------------------------------
    // Helper — create a bare GameObject with RectTransform
    // ---------------------------------------------------------------
    private GameObject CreateUIElement(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    // ---------------------------------------------------------------
    // Helper — readable soil name
    // Swap these strings once you have your real soil variety names.
    // ---------------------------------------------------------------
    private static string SoilDisplayName(SoilKind kind) => kind switch
    {
        SoilKind.Clay => "Clay  (moisture-retaining)",
        SoilKind.Loam => "Loam  (rich & balanced)",
        SoilKind.Sandy => "Sandy (fast-draining)",
        SoilKind.Water => "Water (permanently saturated)",
        _ => kind.ToString()
    };

    // ---------------------------------------------------------------
    // Gizmos — interaction range sphere in the Scene view
    // ---------------------------------------------------------------
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.2f);
        Gizmos.DrawSphere(transform.position, interactRange);
        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
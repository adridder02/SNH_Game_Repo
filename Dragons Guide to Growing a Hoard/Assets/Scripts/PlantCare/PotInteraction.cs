// =============================================================
// PotInteraction.cs
// -------------------------------------------------------------
// Attach this to the Player.
//
// HOW IT WORKS:
//   - Each frame, a sphere cast finds the nearest PotContents
//     within interactRange.
//   - Press E to open a simple on-screen context menu for that pot.
//   - The menu shows only the actions that make sense right now:
//       • No soil        → "Add Soil" (pick type)
//       • Has soil       → "Change Soil" (pick type)
//       • Has soil, no plant → "Add Plant" (pick from plantPrefabs list)
//       • Has plant      → "Water Plant"
//   - Clicking a menu button executes the action and closes the menu.
//   - Press E again or walk away to close without acting.
//
// SETUP:
//   1. Attach to the Player GameObject.
//   2. Fill plantPrefabs with your plant prefabs (need PlantState).
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
    public List<GameObject> plantPrefabs = new List<GameObject>();

    [Header("Watering")]
    [Tooltip("How much water is added per Q press when watering.")]
    public float waterPerPress = 2f;

    [Tooltip("Maximum water the player can hold. Refills automatically.")]
    public float maxWaterPool = 20f;

    [Tooltip("Rate at which the player's water pool refills per second.")]
    public float poolRefillRate = 1.5f;

    [Header("UI — Interaction Prompt")]
    [Tooltip("Optional world-space or screen-space label showing [E] Interact.")]
    public GameObject interactPromptRoot;

    // ---------------------------------------------------------------
    // Private state
    // ---------------------------------------------------------------
    private float playerWaterPool;
    private PotContents nearbyPot;
    private bool menuOpen = false;

    // ── Runtime UI (built in code) ─────────────────────────────────
    private GameObject menuRoot;
    private Canvas menuCanvas;
    private const float BTN_WIDTH = 220f;
    private const float BTN_HEIGHT = 38f;
    private const float BTN_GAP = 6f;

    // ---------------------------------------------------------------
    // Start
    // ---------------------------------------------------------------
    private void Start()
    {
        playerWaterPool = maxWaterPool;
        BuildMenuCanvas();
        CloseMenu();
    }

    // ---------------------------------------------------------------
    // Update — scan for pots, handle E, handle Q watering
    // ---------------------------------------------------------------
    private void Update()
    {
        // Passive water refill
        playerWaterPool = Mathf.Min(playerWaterPool + poolRefillRate * Time.deltaTime, maxWaterPool);

        // Scan for nearest pot
        PotContents found = FindNearestPot();

        // If the pot under focus changes, close any open menu
        if (found != nearbyPot)
        {
            if (menuOpen) CloseMenu();
            nearbyPot = found;
        }

        // Show/hide the [E] prompt
        if (interactPromptRoot != null)
            interactPromptRoot.SetActive(nearbyPot != null && !menuOpen);

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
    // Quick water (Q key)
    // ---------------------------------------------------------------
    private void QuickWater(PotContents pot)
    {
        if (!pot.HasPlant)
        {
            Debug.Log("[PotInteraction] No plant in this pot — nothing to water.");
            return;
        }
        if (playerWaterPool <= 0f)
        {
            Debug.Log("[PotInteraction] Water pool empty. Wait for refill.");
            return;
        }
        float transfer = Mathf.Min(waterPerPress, playerWaterPool);
        if (pot.AddWater(transfer))
            playerWaterPool -= transfer;
        else
            Debug.Log("[PotInteraction] Pot is already full.");
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
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Clear old buttons
        foreach (Transform child in menuRoot.transform)
            Destroy(child.gameObject);

        // Build action list based on pot state
        List<(string label, System.Action action)> actions = new();

        // ── Soil actions ──────────────────────────────────────────
        string soilHeader = pot.HasSoil ? "Change Soil:" : "Add Soil:";
        actions.Add((soilHeader, null)); // section header (non-clickable)

        foreach (SoilKind kind in System.Enum.GetValues(typeof(SoilKind)))
        {
            SoilKind captured = kind; // closure capture
            string indicator = (pot.HasSoil && pot.CurrentSoil == captured) ? " Y " : "";
            actions.Add(($"  {SoilDisplayName(captured)}{indicator}", () =>
            {
                pot.SetSoil(captured);
                // Re-open to reflect new state
                OpenMenu(pot);
            }
            ));
        }

        // ── Plant actions ─────────────────────────────────────────
        if (pot.HasSoil)
        {
            if (!pot.HasPlant)
            {
                if (plantPrefabs.Count > 0)
                {
                    actions.Add(("Add Plant:", null)); // header
                    foreach (GameObject prefab in plantPrefabs)
                    {
                        GameObject captured = prefab;
                        string plantName = prefab.name.Replace("(Clone)", "").Trim();
                        actions.Add(($"  {plantName}", () =>
                        {
                            pot.AddPlant(captured);
                            CloseMenu();
                        }
                        ));
                    }
                }
                else
                {
                    actions.Add(("[No plant prefabs assigned]", null));
                }
            }
            else
            {
                // Has plant — offer watering and removal
                actions.Add(($"Water Plant  (pool: {playerWaterPool:F1})", () =>
                {
                    QuickWater(pot);
                    OpenMenu(pot); // refresh label
                }
                ));

                actions.Add(("Remove Plant", () =>
                {
                    pot.RemovePlant();
                    CloseMenu();
                }
                ));
            }
        }

        // ── Close button ──────────────────────────────────────────
        actions.Add(("[ Close ]", CloseMenu));

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
        panelImg.color = new Color(0.06f, 0.08f, 0.06f, 0.92f);

        for (int i = 0; i < actions.Count; i++)
        {
            var (label, action) = actions[i];
            float y = startY - i * (BTN_HEIGHT + BTN_GAP);
            bool isHeader = action == null;

            if (isHeader)
            {
                // Non-interactive section label
                GameObject lblGO = CreateUIElement(label, panel.transform);
                RectTransform lr = lblGO.GetComponent<RectTransform>();
                lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
                lr.sizeDelta = new Vector2(BTN_WIDTH, BTN_HEIGHT);
                lr.anchoredPosition = new Vector2(0f, y);
                TextMeshProUGUI txt = lblGO.AddComponent<TextMeshProUGUI>();
                txt.text = label;
                txt.fontSize = 12;
                txt.color = new Color(0.65f, 0.85f, 0.65f);
                txt.fontStyle = FontStyles.Bold;
                txt.alignment = TextAlignmentOptions.MidlineLeft;
            }
            else
            {
                // Clickable button
                GameObject btnGO = CreateUIElement("Btn_" + i, panel.transform);
                RectTransform br = btnGO.GetComponent<RectTransform>();
                br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f);
                br.sizeDelta = new Vector2(BTN_WIDTH, BTN_HEIGHT);
                br.anchoredPosition = new Vector2(0f, y);

                Image bg = btnGO.AddComponent<Image>();
                bg.color = new Color(0.15f, 0.20f, 0.15f, 0.85f);

                Button btn = btnGO.AddComponent<Button>();
                System.Action captured = action;
                btn.onClick.AddListener(() => captured?.Invoke());

                // Hover tint
                ColorBlock cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(0.75f, 1f, 0.75f);
                cb.pressedColor = new Color(0.55f, 0.85f, 0.55f);
                btn.colors = cb;
                btn.targetGraphic = bg;

                GameObject txtGO = CreateUIElement("Label", btnGO.transform);
                RectTransform tr = txtGO.GetComponent<RectTransform>();
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
                tr.offsetMin = new Vector2(8f, 0f); tr.offsetMax = Vector2.zero;
                TextMeshProUGUI txt = txtGO.AddComponent<TextMeshProUGUI>();
                txt.text = label;
                txt.fontSize = 11;
                txt.color = Color.white;
                txt.alignment = TextAlignmentOptions.MidlineLeft;
            }
        }

        menuOpen = true;
    }

    private void CloseMenu()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

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

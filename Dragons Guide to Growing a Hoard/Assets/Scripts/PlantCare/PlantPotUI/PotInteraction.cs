// =============================================================
// PotInteraction.cs
// -------------------------------------------------------------
// Attach this script to the Player.
//
// HOW IT WORKS:
//   - Each frame, a sphere cast finds the nearest PotContents
//     within interactRange.
//   - Press E to open the pot menu (built by PotMenuUIController —
//     this script no longer builds any UI itself, see the note below).
//   - Press E again or walk away to close without acting.
//   - Q quick-waters the nearby pot directly, whether the menu is
//     open or not — unchanged from before.
//
// UI CHANGE:
//   All the runtime-built text menu code (BuildMenuCanvas, OpenMenu's
//   button-list builder, CreateUIElement, SoilDisplayName, etc.) has
//   been removed. That responsibility now belongs entirely to
//   PotMenuUIController, which drives the real Main/Choose Soil/
//   Choose Plant panels. This script just tells it when to open and
//   close, and gives it a few callbacks (RefreshOutlineFor, WaterPot,
//   CloseMenu) so it can affect the player's world state without
//   needing to know about outlines, camera locking, or input modes.
//
// SETUP:
//   1. Attach to the Player GameObject.
//   2. Assign potMenuUI — the PotMenuUIController on your pot-menu Canvas.
//   3. Fill dragonInventory (auto-found if left empty).
//   4. Adjust interactRange so it feels natural.
//   5. Optionally set a UI Canvas for the interaction prompt.
// =============================================================

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
    [Tooltip("The player's plant inventory — read by PotMenuUIController's Choose Plant panel.")]
    public PlayerInventory dragonInventory;

    [Header("Menu UI")]
    [Tooltip("Drives the real Main/Choose Soil/Choose Plant panels. This script only calls " +
             "Open()/Close() on it — see PotMenuUIController.cs for all the panel logic.")]
    [SerializeField] private PotMenuUIController potMenuUI;

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
    private OutlineEffect potOutline;

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

        if (potMenuUI == null)
            Debug.LogWarning("[PotInteraction] potMenuUI is not assigned — pressing E won't be able to open anything.", this);

        playerWaterPool = dragonInventory.getWaterPool();
        if (showInteractPrompt) BuildInteractPrompt();
        menuOpen = false;
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
        if (pot == null) return;

        // Always highlight the pot itself
        potOutline = pot.GetComponent<OutlineEffect>();
        potOutline?.SetOutline(true);

        // Only highlight the plant if one is actually present
        if (pot.HasPlant && pot.Plant != null)
        {
            currentOutline = pot.Plant.GetComponent<OutlineEffect>();
            currentOutline?.SetOutline(true);
        }
    }

    private void ClearCurrentOutline()
    {
        currentOutline?.SetOutline(false);
        currentOutline = null;

        potOutline?.SetOutline(false);
        potOutline = null;
    }

    /// <summary>Called by PotMenuUIController after an action changes a pot's plant (add/remove) — reapplies the outline if that pot is still the one in focus.</summary>
    public void RefreshOutlineFor(PotContents pot)
    {
        if (pot == nearbyPot)
        {
            ClearCurrentOutline();
            ApplyOutlineFor(pot);
        }
    }

    // ---------------------------------------------------------------
    // Quick water (Q key, and the menu's Water button)
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
        if (pot.AddWater(transfer))
        {
            dragonInventory.reduceWaterPool(transfer);
        }
        else
            Debug.Log("[PotInteraction] Pot is already full.");
    }

    /// <summary>Public wrapper so PotMenuUIController's Water button can reuse the same logic as the Q key.</summary>
    public void WaterPot(PotContents pot) => QuickWater(pot);

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
    // MENU — just tells PotMenuUIController when to open/close, plus
    // the input-mode/camera-lock/outline side effects that go with it.
    // ===============================================================

    private void OpenMenu(PotContents pot)
    {
        if (pot == null || pot.gameObject == null)
        {
            Debug.LogWarning("Tried to open menu for a destroyed or null pot");
            return;
        }

        GameInputModeManager.Instance?.SetPlacementMode();
        ThirdPersonCameraController.CameraLocked = true;

        // Same cursor treatment Inventory/Journal use while a UI menu is open —
        // this was missing here, which is why the pot menu didn't free the cursor.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        menuOpen = true;

        if (potMenuUI != null)
            potMenuUI.Open(pot, dragonInventory, this);
    }

    /// <summary>Closes the pot menu. Public so PotMenuUIController's close button can call it directly.</summary>
    public void CloseMenu()
    {
        GameInputModeManager.Instance?.SetGameplayMode();
        ThirdPersonCameraController.CameraLocked = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        menuOpen = false;

        if (potMenuUI != null)
            potMenuUI.Close();
    }

    // ---------------------------------------------------------------
    // Helper — create a bare GameObject with RectTransform (still
    // used by the world-space interact prompt above).
    // ---------------------------------------------------------------
    private GameObject CreateUIElement(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

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
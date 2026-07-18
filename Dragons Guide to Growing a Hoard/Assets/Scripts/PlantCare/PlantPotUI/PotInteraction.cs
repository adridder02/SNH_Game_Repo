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
//   5. Assign promptTemplate — an InteractPromptView prefab (background +
//      keybind icon + label, built once — see InteractPromptView.cs).
//      This script only instantiates it and fills in the sprite/text.
// =============================================================

using UnityEngine;
using UnityEngine.InputSystem;

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
    [Tooltip("Display an interact prompt above pots when in range.")]
    public bool showInteractPrompt = true;

    [Tooltip("ON = the floating world-space prompt below (promptTemplate) that follows the player and " +
             "billboards toward the camera. OFF = a fixed screen-space prompt on the main HUD instead — " +
             "just enabled/disabled, no positioning or billboarding. See MainUIController.SetInteractPromptVisible().")]
    public bool useWorldSpacePrompt = true;

    [Tooltip("Height above the player's head for the interact prompt. Only used when useWorldSpacePrompt is ON.")]
    public float promptHeightOffset = 2.0f;

    [Tooltip("Prefab defining the whole three-part look (background/keybind icon/label — see " +
             "InteractPromptView.cs), built and positioned once in the Editor. PotInteraction just " +
             "instantiates a single copy of this and repositions/billboards it — it never builds or " +
             "aligns any of the three parts itself. Only used when useWorldSpacePrompt is ON.")]
    [SerializeField] private InteractPromptView promptTemplate;

    [Tooltip("Sprite for the keybind icon (e.g. an '[E]' keycap). Leave null to keep whatever's already " +
             "set on the template's KeybindIcon. Only used when useWorldSpacePrompt is ON.")]
    public Sprite interactKeybindSprite;

    [Tooltip("Label text, e.g. 'Interact'. Leave empty to keep whatever's already set on the template's " +
             "Label. Only used when useWorldSpacePrompt is ON.")]
    public string interactPromptLabel = "Interact";

    [Tooltip("The main HUD controller whose fixed interactPromptHUD element gets shown/hidden instead, " +
             "when useWorldSpacePrompt is OFF. Auto-found in the scene if left empty.")]
    [SerializeField] private MainUIController mainUI;

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
    private InteractPromptView promptView;

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

        if (showInteractPrompt)
        {
            if (useWorldSpacePrompt)
            {
                BuildInteractPrompt();
            }
            else if (mainUI == null)
            {
                mainUI = FindObjectOfType<MainUIController>();
                if (mainUI == null)
                    Debug.LogWarning("[PotInteraction] useWorldSpacePrompt is OFF but no MainUIController " +
                                      "was found/assigned — the interact prompt won't be shown.", this);
            }
        }

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

        // Show/hide the interact prompt now; its position/rotation are updated in LateUpdate (see below),
        // after the player has finished moving for this frame — doing it here in Update() was reading a
        // stale position on frames where movement hadn't been applied yet, which read as flicker/jitter.
        UpdateInteractPromptVisibility();

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

    private void LateUpdate()
    {
        // Runs after every script's Update() this frame (including whatever moves the player), so this
        // always reads the player's final position/rotation for the frame instead of a half-step-stale one.
        UpdateInteractPromptTransform();
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
        if (promptTemplate == null)
        {
            Debug.LogWarning("[PotInteraction] promptTemplate is not assigned — no interact prompt will be " +
                              "shown. Assign your prompt prefab (background + keybind icon + label) in the Inspector.", this);
            return;
        }

        promptView = Instantiate(promptTemplate);
        promptRoot = promptView.gameObject;
        promptRoot.name = "InteractPrompt";
        promptRoot.transform.SetParent(null);
        DontDestroyOnLoad(promptRoot);

        if (promptView.canvas != null)
            promptView.canvas.renderMode = RenderMode.WorldSpace;

        if (promptView.keybindIcon != null && interactKeybindSprite != null)
        {
            promptView.keybindIcon.sprite = interactKeybindSprite;
            promptView.keybindIcon.enabled = true;
        }

        if (promptView.label != null && !string.IsNullOrEmpty(interactPromptLabel))
            promptView.label.text = interactPromptLabel;

        promptRoot.SetActive(false); // Hidden by default
    }

    private void UpdateInteractPromptVisibility()
    {
        if (!showInteractPrompt) return;

        bool shouldShow = nearbyPot != null && !menuOpen;

        if (useWorldSpacePrompt)
        {
            if (promptRoot != null)
                promptRoot.SetActive(shouldShow);
        }
        else
        {
            mainUI?.SetInteractPromptVisible(shouldShow);
        }
    }

    private void UpdateInteractPromptTransform()
    {
        // Only the world-space prompt needs positioning/billboarding — the HUD version is a fixed
        // screen-space element that MainUIController just enables/disables.
        if (!useWorldSpacePrompt) return;
        if (!showInteractPrompt || promptRoot == null || !promptRoot.activeSelf) return;

        // Position above the PLAYER (this script's own transform) rather than the stationary pot —
        // that way it moves naturally with the player (walking, jumping, stairs, slopes) instead of
        // sitting at one fixed height the whole time.
        promptRoot.transform.position = transform.position + Vector3.up * promptHeightOffset;

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
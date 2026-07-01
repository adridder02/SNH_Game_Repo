using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class UI_Inventory_Script : MonoBehaviour, PlayerControls.IGamePlayActions
{
    [Header("Inventory of Player")]
    [SerializeField] private PlayerInventory playerInventory;

    private VisualElement[] slot = new VisualElement[9];
    private bool isInventoryOpen = false;
    private PlayerControls playerControls;
    private UIDocument uiDocument;
    private VisualElement root;
    private VisualElement inventoryRoot; // The "Box" container

    void Awake()
    {
        Debug.Log("=== UI_Inventory_Script Awake() ===");

        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogError("❌ UIDocument component not found!");
            return;
        }

        playerControls = new PlayerControls();

        try
        {
            var inventoryAction = playerControls.GamePlay.Get().FindAction("Inventory");
            if (inventoryAction != null)
            {
                Debug.Log("✅ Inventory action FOUND");
                playerControls.GamePlay.SetCallbacks(this);
            }
            else
            {
                Debug.LogError("❌ Inventory action NOT FOUND! Add it to Input Action Asset.");
            }
        }
        catch
        {
            Debug.LogError("❌ Inventory action NOT FOUND!");
        }

        if (playerInventory == null)
            playerInventory = FindObjectOfType<PlayerInventory>();

        root = uiDocument.rootVisualElement;

        if (root == null)
        {
            Debug.LogError("❌ Root VisualElement is NULL!");
            return;
        }

        // Grab the Box container (top-level panel in your UXML)
        inventoryRoot = root.Q<VisualElement>("Box");
        if (inventoryRoot == null)
        {
            // Fallback: use root directly
            Debug.LogWarning("⚠️ 'Box' not found, using root directly.");
            inventoryRoot = root;
        }

        CacheSlots();

        // ✅ KEY FIX: Hide via display style, NOT SetActive(false)
        // This keeps the GameObject alive so input listeners stay registered.
        SetInventoryVisible(false);

        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
    }

    /// <summary>
    /// Query and cache all 9 slot VisualElements from the UXML.
    /// Call once after root is ready.
    /// </summary>
    void CacheSlots()
    {
        Debug.Log($"✅ Root has {root.childCount} children");

        for (int i = 0; i < 9; i++)
        {
            string slotName = "Inv_" + (i + 1);
            slot[i] = root.Q<VisualElement>(slotName);

            if (slot[i] != null)
            {
                Debug.Log($"✅ Found slot: {slot[i].name}");
                int index = i;
                // Remove old callbacks first to avoid duplicates on re-cache
                slot[i].UnregisterCallback<ClickEvent>(OnSlotClickedCallback);
                slot[i].RegisterCallback<ClickEvent>(OnSlotClickedCallback);
            }
            else
            {
                Debug.LogWarning($"❌ Slot '{slotName}' not found in UXML!");
            }
        }
    }

    // Stored delegate so we can unregister it cleanly
    private void OnSlotClickedCallback(ClickEvent evt)
    {
        VisualElement clicked = evt.currentTarget as VisualElement;
        for (int i = 0; i < slot.Length; i++)
        {
            if (slot[i] == clicked)
            {
                OnSlotClicked(i);
                return;
            }
        }
    }

    /// <summary>
    /// Show or hide the inventory panel using DisplayStyle — never SetActive().
    /// </summary>
    void SetInventoryVisible(bool visible)
    {
        if (inventoryRoot == null) return;
        inventoryRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void OnEnable()
    {
        if (playerControls != null)
            playerControls.Enable();
    }

    void OnDisable()
    {
        if (playerControls != null)
            playerControls.Disable();
    }

    void OnDestroy()
    {
        if (playerControls != null)
            playerControls.Dispose();
    }

    public void OnMove(InputAction.CallbackContext context) { }
    public void OnFly(InputAction.CallbackContext context) { }
    public void OnLook(InputAction.CallbackContext context) { }

    public void OnInventory(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Debug.Log("🟢 INVENTORY PRESSED!");
            ToggleInventory();
        }
    }

    public void ToggleInventory()
    {
        isInventoryOpen = !isInventoryOpen;

        SetInventoryVisible(isInventoryOpen);

        if (isInventoryOpen)
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            UpdateInventoryUI();
        }
        else
        {
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }
    }

    void UpdateInventoryUI()
    {
        if (playerInventory == null)
        {
            Debug.LogError("❌ PlayerInventory is null!");
            return;
        }

        List<GameObject> inventory = playerInventory.GetInventory();
        Debug.Log($"📦 Updating UI with {inventory.Count} items");

        for (int i = 0; i < 9; i++)
        {
            if (slot[i] == null)
            {
                Debug.LogWarning($"❌ Slot {i} is null!");
                continue;
            }

            if (i < inventory.Count && inventory[i] != null)
            {
                UpdateSlot(i, inventory[i]);
            }
            else
            {
                ClearSlot(i);
            }
        }
    }

    void UpdateSlot(int index, GameObject item)
    {
        Debug.Log($"=== Updating Slot {index} with {item.name} ===");

        if (slot[index] == null)
        {
            Debug.LogError($"❌ Slot {index} is null!");
            return;
        }

        // --- Image ---
        Image slotImage = slot[index].Q<Image>("Image");
        if (slotImage != null)
        {
            SpriteRenderer sr = item.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                slotImage.sprite = sr.sprite;
                slotImage.style.display = DisplayStyle.Flex;
                Debug.Log($"✅ Set image for slot {index}");
            }
            else
            {
                slotImage.sprite = null;
                slotImage.style.display = DisplayStyle.None;
                Debug.Log($"⚠️ No sprite for slot {index}");
            }
        }

        // --- Label ---
        Label slotLabel = slot[index].Q<Label>("Label");
        if (slotLabel != null)
        {
            slotLabel.text = item.name;
            slotLabel.style.display = DisplayStyle.Flex;
            // ✅ Don't force color/fontSize here — let USS handle it.
            //    Only override if you have no stylesheet controlling it.
            Debug.Log($"✅ Label text set to: '{slotLabel.text}'");
        }
        else
        {
            Debug.LogError($"❌ Label 'Label' not found in slot {index}!");
        }
    }

    void ClearSlot(int index)
    {
        if (slot[index] == null) return;

        Image slotImage = slot[index].Q<Image>("Image");
        if (slotImage != null)
        {
            slotImage.sprite = null;
            slotImage.style.display = DisplayStyle.None;
        }

        Label slotLabel = slot[index].Q<Label>("Label");
        if (slotLabel != null)
        {
            slotLabel.text = "";
            slotLabel.style.display = DisplayStyle.None;
        }
    }

    void OnSlotClicked(int index)
    {
        Debug.Log($"🖱️ Slot {index + 1} was clicked!");

        if (playerInventory == null) return;

        List<GameObject> inventory = playerInventory.GetInventory();
        if (index < inventory.Count && inventory[index] != null)
        {
            Debug.Log($"Using item: {inventory[index].name}");
            // TODO: trigger item use logic here
        }
    }
}
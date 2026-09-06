using UnityEngine;
using UnityEngine.InputSystem;

public class ExitMenuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject exitMenuRoot;
    [SerializeField] private InventoryUIController inventoryController;
    [SerializeField] private PotMenuUIController potMenuController;
    [SerializeField] private JournalUIController journalController;
    [Tooltip("The main HUD controller — used to hide the HUD (bars/hotbar/icons) while the exit menu is " +
             "open and show it again on close. Auto-found in the scene if left empty.")]
    [SerializeField] private MainUIController mainUI;

    public bool IsExitMenuOpen => exitMenuRoot != null && exitMenuRoot.activeSelf;

    void Awake()
    {
        if (exitMenuRoot != null)
            exitMenuRoot.SetActive(false);

        if (inventoryController == null)
            inventoryController = FindAnyObjectByType<InventoryUIController>();

        if (potMenuController == null)
            potMenuController = FindAnyObjectByType<PotMenuUIController>();

        if (journalController == null)
            journalController = FindAnyObjectByType<JournalUIController>();

        if (mainUI == null)
            mainUI = FindAnyObjectByType<MainUIController>();
    }

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (IsExitMenuOpen)
        {
            CloseExitMenu();
            return;
        }

        if (potMenuController != null && potMenuController.IsOpen)
        {
            potMenuController.CloseSubmenuOrMenu();
            return;
        }

        if (inventoryController != null && inventoryController.IsInventoryOpen)
        {
            inventoryController.CloseInventory();
            return;
        }

        if (journalController != null && journalController.IsJournalOpen)
        {
            journalController.CloseJournal();
            return;
        }

        OpenExitMenu();
    }

    public void OpenExitMenu()
    {
        if (exitMenuRoot != null)
            exitMenuRoot.SetActive(true);

        MenuLayerManager.NotifyOpened(this, CloseExitMenu);
        mainUI?.SetHudHidden(true, this);

        // Stronger forcing for the problematic Exit Menu
        ForceMenuState();
        // NOTE: this used to hide the old on-screen Tutorial panel here. Hook whatever
        // replaces it in when that system exists.
    }

    public void CloseExitMenu()
    {
        if (exitMenuRoot != null)
            exitMenuRoot.SetActive(false);

        MenuLayerManager.NotifyClosed(this);
        mainUI?.SetHudHidden(false, this);

        GameInputModeManager.Instance?.SetGameplayMode();
        // NOTE: this used to re-show the old on-screen Tutorial panel here. Hook whatever
        // replaces it in when that system exists.
    }

    /// <summary>
    /// Extra aggressive state forcing for Exit Menu
    /// </summary>
    private void ForceMenuState()
    {
        GameInputModeManager.Instance?.SetMenuUIMode();

        // Extra safety calls
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ThirdPersonCameraController.CameraLocked = true;

        // Disable camera input directly
        var cam = FindAnyObjectByType<ThirdPersonCameraController>();
        if (cam != null)
        {
            var axis = cam.GetComponent<Unity.Cinemachine.CinemachineInputAxisController>();
            if (axis != null) axis.enabled = false;
        }

        // One more frame of forcing
        Invoke(nameof(ForceAgain), 0.02f);
    }

    private void ForceAgain()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ThirdPersonCameraController.CameraLocked = true;
    }
}
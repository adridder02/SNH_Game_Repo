using UnityEngine;
using UnityEngine.InputSystem;

public class GameInputModeManager : MonoBehaviour
{
    public static GameInputModeManager Instance { get; private set; }

    public enum InputMode
    {
        Gameplay,
        Placement,
        UI
    }

    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private PlayerController playerController;

    private InputActionMap gameplayMap;
    private InputActionMap cameraMap;
   
    public InputMode CurrentMode { get; private set; }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        if (inputActions == null)
        {
            Debug.LogError("GameInputModeManager: InputActionAsset not assigned.");
            return;
        }

        gameplayMap = inputActions.FindActionMap("GamePlay", false);
        cameraMap = inputActions.FindActionMap("Camera", false);

        if (gameplayMap == null)
            Debug.LogError("Missing Action Map: GamePlay");

        if (cameraMap == null)
            Debug.LogError("Missing Action Map: Camera");
    }

    private void Start()
    {
        SetGameplayMode();
    }
    
   
    void Update()
    {}

    public void SetGameplayMode()
    {
        CurrentMode = InputMode.Gameplay;

        gameplayMap?.Enable();
        cameraMap?.Enable();

        if (playerController != null)
            playerController.SetMovementEnabled(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void SetPlacementMode()
    {
        CurrentMode = InputMode.Placement;

        // KEEP gameplay enabled so player can still move
        gameplayMap?.Enable();

        // Disable camera look / zoom only
        cameraMap?.Disable();

        if (playerController != null)
            playerController.SetMovementEnabled(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// Used while a full-screen UI panel (inventory, menus, etc.) is open.
    /// Movement and camera look/zoom are disabled, but the GamePlay map
    /// stays enabled so actions like "Inventory" (used to close the panel
    /// again) keep firing.
    /// </summary>
    public void SetUIMode()
    {
        CurrentMode = InputMode.UI;

        // Keep GamePlay map enabled — the Inventory toggle action needs to
        // keep firing so the player can close the inventory again.
        gameplayMap?.Enable();

        // Stop camera look / zoom
        cameraMap?.Disable();

        if (playerController != null)
            playerController.SetMovementEnabled(false);

        // Cursor is handled by InventoryUIController itself, so it's left
        // alone here.
    }
}
using UnityEngine;
using UnityEngine.InputSystem;

public class GameInputModeManager : MonoBehaviour
{
    public static GameInputModeManager Instance { get; private set; }

    public enum InputMode
    {
        Gameplay,
        MenuUI,      // Menus: movement ON, camera OFF, cursor visible
        Placement
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

        if (gameplayMap == null) Debug.LogError("Missing Action Map: GamePlay");
        if (cameraMap == null) Debug.LogError("Missing Action Map: Camera");
    }

    private void Start()
    {
        SetGameplayMode();
    }

    public void SetGameplayMode()
    {
        CurrentMode = InputMode.Gameplay;

        gameplayMap?.Enable();
        cameraMap?.Enable();

        if (playerController != null)
            playerController.SetMovementEnabled(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        ThirdPersonCameraController.CameraLocked = false;

        var camController = FindAnyObjectByType<ThirdPersonCameraController>();
        if (camController != null)
        {
            var inputAxis = camController.GetComponent<Unity.Cinemachine.CinemachineInputAxisController>();
            if (inputAxis != null)
                inputAxis.enabled = true;
        }
    }

    /// <summary>
    /// For menus (Inventory, Journal, Pot Menu, Exit Menu, etc.).
    /// Keeps movement enabled, disables only camera, shows cursor.
    /// </summary>
    public void SetMenuUIMode()
    {
        CurrentMode = InputMode.MenuUI;

        gameplayMap?.Enable();
        cameraMap?.Disable();

        if (playerController != null)
            playerController.SetMovementEnabled(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Explicitly lock camera
        ThirdPersonCameraController.CameraLocked = true;

        // Disable camera input component as backup
        var camController = FindAnyObjectByType<ThirdPersonCameraController>();
        if (camController != null)
        {
            var inputAxis = camController.GetComponent<Unity.Cinemachine.CinemachineInputAxisController>();
            if (inputAxis != null)
                inputAxis.enabled = false;
        }
    }

    public void SetPlacementMode()
    {
        CurrentMode = InputMode.Placement;

        gameplayMap?.Enable();
        cameraMap?.Disable();

        if (playerController != null)
            playerController.SetMovementEnabled(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ThirdPersonCameraController.CameraLocked = true;

        var camController = FindAnyObjectByType<ThirdPersonCameraController>();
        if (camController != null)
        {
            var inputAxis = camController.GetComponent<Unity.Cinemachine.CinemachineInputAxisController>();
            if (inputAxis != null)
                inputAxis.enabled = false;
        }
    }
}
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

    private bool altOverrideActive = false;

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

    private void Update()
    {
        // Alt-to-free-cursor only makes sense during normal gameplay;
        // menu/placement modes already show the cursor and lock the camera.
        if (CurrentMode != InputMode.Gameplay)
            return;

        bool altHeld = Keyboard.current != null &&
                       (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed);

        if (altHeld && !altOverrideActive)
        {
            altOverrideActive = true;
            ApplyAltOverride();
        }
        else if (!altHeld && altOverrideActive)
        {
            altOverrideActive = false;
            RemoveAltOverride();
        }
    }

    private void ApplyAltOverride()
    {
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

    private void RemoveAltOverride()
    {
        // Safety check: only restore gameplay cursor/camera state if we're
        // still in Gameplay mode (in case a mode switch happened in between).
        if (CurrentMode != InputMode.Gameplay)
            return;

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

    public void SetGameplayMode()
    {
        CurrentMode = InputMode.Gameplay;
        altOverrideActive = false;

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

        // Back to gameplay — bring the bottom-bar tutorial tip back if that's what's currently showing.
        TutorialSequenceController.Instance?.SetMenuOpen(false);
    }

    /// <summary>
    /// For menus (Inventory, Journal, Pot Menu, Exit Menu, etc.).
    /// Keeps movement enabled, disables only camera, shows cursor.
    /// </summary>
    public void SetMenuUIMode()
    {
        CurrentMode = InputMode.MenuUI;
        altOverrideActive = false;

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

        // A menu just covered the screen — hide the bottom-bar tutorial tip so it doesn't sit behind it.
        TutorialSequenceController.Instance?.SetMenuOpen(true);
    }

    public void SetPlacementMode()
    {
        CurrentMode = InputMode.Placement;
        altOverrideActive = false;

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

        // Same as menu UI mode — hide the bottom-bar tip while placement mode's own UI is up.
        TutorialSequenceController.Instance?.SetMenuOpen(true);
    }
}
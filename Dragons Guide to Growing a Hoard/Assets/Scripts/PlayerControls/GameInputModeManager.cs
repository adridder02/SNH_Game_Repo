using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
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

        // This object is DontDestroyOnLoad, so Start() below only ever fires once, the very
        // first time it's created — a later scene reload (Restart from the exit menu, a scene
        // change generally) doesn't re-run it, but Cursor.lockState/Cursor.visible are global
        // engine state that ISN'T reset by a scene load either. Net result without this: the
        // cursor stays stuck at whatever the exit menu last set it to (visible/unlocked) after
        // Restart, until the player happens to open+close a menu again, which is the only other
        // place that calls SetGameplayMode(). Re-syncing on every scene load fixes that generally,
        // not just for the exit menu's Restart button specifically.
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        SetGameplayMode();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        // playerController was pointing at the PREVIOUS scene's player, which got destroyed on
        // load — re-find it in whatever just loaded rather than keep a dangling reference.
        playerController = FindAnyObjectByType<PlayerController>();

        // Only auto-lock the cursor into gameplay mode if there's actually a player to control in
        // this scene. Guards against this firing on a menu-only scene (title screen, etc.) that
        // might load later and has no PlayerController — locking/hiding the cursor there would
        // break clicking its own UI buttons.
        if (playerController != null)
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
        ThirdPersonCameraController.AllowRotationWhileLockedIfRightClickHeld = false;

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
        ThirdPersonCameraController.AllowRotationWhileLockedIfRightClickHeld = false;

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

        // The one difference from SetMenuUIMode's identical-looking lock above: holding right-click
        // still lets the player reposition the camera without leaving Placement mode (see the field's
        // own comment on ThirdPersonCameraController) — a deliberate action instead of the camera
        // swiveling on every small mouse movement while they're just trying to hold a hover cell
        // steady. Right-click no longer cancels the mode either (PlacementSystem/WallPlacementSystem
        // dropped that) — Escape is the only way out now.
        ThirdPersonCameraController.AllowRotationWhileLockedIfRightClickHeld = true;

        var camController = FindAnyObjectByType<ThirdPersonCameraController>();
        if (camController != null)
        {
            var inputAxis = camController.GetComponent<Unity.Cinemachine.CinemachineInputAxisController>();
            if (inputAxis != null)
                inputAxis.enabled = false;
        }

        // Deliberately NOT hiding the bottom-bar tutorial tip here (unlike SetMenuUIMode above) —
        // placement mode is exactly when steps like "press F", "scroll to find the smallest pot",
        // "left-click to place" need to stay visible so the player can actually follow them while
        // they're in the mode the step is guiding them through. Real menus (Inventory/Journal/Pot
        // Menu/Exit Menu, via SetMenuUIMode) still hide it, since those genuinely cover the screen.
    }
}
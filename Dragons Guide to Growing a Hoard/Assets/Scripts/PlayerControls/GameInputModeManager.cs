using UnityEngine;
using UnityEngine.InputSystem;

public class GameInputModeManager : MonoBehaviour
{
    public static GameInputModeManager Instance { get; private set; }

    public enum InputMode
    {
        Gameplay,
        Placement
    }

    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private PlayerController playerController;

    private InputActionMap gameplayMap;
    private InputActionMap cameraMap;
    private bool CursorState = false;

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
    
    //Making the cursor visable so we can see it //!needs placement mode testing
    void Update()
    {
       Mouse mouse = Mouse.current;
       if (mouse.rightButton.wasPressedThisFrame)
       {   
           
           
           if (CursorState == false)
           { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; CursorState = true; }
           else
           { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; CursorState = false; }
           
       }
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
}
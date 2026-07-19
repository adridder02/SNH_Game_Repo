using UnityEngine;

/// <summary>
/// Central cursor manager for 4 states: Normal, Clickable (hover), Hand (grab-ready), HandClose (grabbing).
/// Attach this to a persistent object (e.g. a GameManager) - only one instance should exist.
/// </summary>
public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    public enum CursorState { Normal, Clickable, Hand, HandClose }

    [Header("Cursor Textures")]
    [SerializeField] private Texture2D normalCursor;
    [SerializeField] private Texture2D clickableCursor;
    [SerializeField] private Texture2D handCursor;
    [SerializeField] private Texture2D handCloseCursor;

    [Header("Settings")]
    [Tooltip("Offset from top-left of the texture that represents the click point.")]
    [SerializeField] private Vector2 hotSpot = Vector2.zero;
    [SerializeField] private CursorMode cursorMode = CursorMode.Auto;

    private CursorState currentState = (CursorState)(-1); 

    void Awake()
    {
        // Simple singleton so any script can call CursorManager.Instance.SetCursor(...)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        //DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        SetCursor(CursorState.Normal);
    }

    public void SetCursor(CursorState state)
    {
        if (currentState == state) return; // avoid redundant native calls
        currentState = state;

        Texture2D tex = state switch
        {
            CursorState.Normal => normalCursor,
            CursorState.Clickable => clickableCursor,
            CursorState.Hand => handCursor,
            CursorState.HandClose => handCloseCursor,
            _ => normalCursor
        };

        Cursor.SetCursor(tex, hotSpot, cursorMode);
    }

    public CursorState CurrentState => currentState;
}

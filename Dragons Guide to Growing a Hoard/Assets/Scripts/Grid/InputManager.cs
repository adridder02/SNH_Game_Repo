using UnityEngine.InputSystem;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    [SerializeField] private Camera sceneCamera;

    // !! IMPORTANT: the greenhouse surface plane(s) MUST be on a layer included
    // in this mask, AND they must have a Collider component, otherwise the
    // raycast will never hit and placement will silently fail at (0,0,0).
    [SerializeField] private LayerMask placementLayerMask;

    [Tooltip("Layer(s) your wall-mount surfaces (Clovenwick, etc.) sit on. Kept separate from " +
             "placementLayerMask so a mouse position that happens to line up with both a floor pot " +
             "grid and a wall grid always resolves to whichever raycast the active tool actually asked for.")]
    [SerializeField] private LayerMask wallLayerMask;

    [SerializeField] private bool debugRaycast = false;
  
    private Vector3 lastPosition;
    private Vector3 lastWallPosition;
    private Vector3 lastWallNormal = Vector3.forward;

    private void Awake()
    {
        if (sceneCamera == null)
            sceneCamera = Camera.main;

    }
    

    public Vector3 GetSelectedMapPosition()
    {
        if (sceneCamera == null)
            return lastPosition;

        Ray ray = sceneCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 200f, placementLayerMask))
        {
            lastPosition = hit.point;
        }

        if (debugRaycast)
            Debug.Log($"[InputManager] Raycast hit: {lastPosition} | LayerMask value: {placementLayerMask.value}");

        return lastPosition;
    }

    /// <summary>Same idea as GetSelectedMapPosition but raycasts wallLayerMask instead — used by
    /// WallPlacementSystem so wall-mount tools (Clovenwick) never get confused by whatever's under
    /// the mouse on the floor grid's layer, and vice versa.</summary>
    public Vector3 GetSelectedWallPosition()
    {
        if (sceneCamera == null)
            return lastWallPosition;

        Ray ray = sceneCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 200f, wallLayerMask))
        {
            lastWallPosition = hit.point;
            lastWallNormal = hit.normal;
        }

        if (debugRaycast)
            Debug.Log($"[InputManager] Wall raycast hit: {lastWallPosition} | LayerMask value: {wallLayerMask.value}");

        return lastWallPosition;
    }

    /// <summary>Surface normal of the last successful wall raycast — lets WallPlacementSystem orient
    /// spawned mounts to sit flush against whichever wall was actually hit.</summary>
    public Vector3 GetSelectedWallNormal() => lastWallNormal;
}
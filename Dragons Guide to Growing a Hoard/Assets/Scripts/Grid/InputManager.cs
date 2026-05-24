using UnityEngine.InputSystem;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    [SerializeField] private Camera sceneCamera;

    // !! IMPORTANT: the greenhouse surface plane(s) MUST be on a layer included
    // in this mask, AND they must have a Collider component, otherwise the
    // raycast will never hit and placement will silently fail at (0,0,0).
    [SerializeField] private LayerMask placementLayerMask;

    [SerializeField] private bool debugRaycast = false;

    private Vector3 lastPosition;

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
}
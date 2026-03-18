using System.Runtime.Serialization;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private Vector3 lastPosition;
    [SerializeField] LayerMask placemnetLayermask;

    public Vector3 GetSelectedMapPosition()
    {
        /* //!uses the old input system
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = sceneCamera.nearClipPlane; 
        */
        
        Ray ray = sceneCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;
        if(Physics.Raycast(ray, out hit, 100, placemnetLayermask))
        {
            lastPosition = hit.point;
        }
    
        return lastPosition;
    }

    public void Update()
    {
        GetSelectedMapPosition();
    }
}

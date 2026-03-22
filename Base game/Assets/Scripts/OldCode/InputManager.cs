using System.Runtime.Serialization;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using UnityEngine;
using Unity.Mathematics;

public class InputManager : MonoBehaviour
{
    [SerializeField] private Camera sceneCamera;
    [SerializeField] LayerMask placemnetLayermask;
    private Vector3 lastPosition;

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
            //Debug.Log(lastPosition);
        }
    
        return lastPosition;
    }


}

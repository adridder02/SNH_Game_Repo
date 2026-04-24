using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCameraController : MonoBehaviour
{   
    //Zoom in and out
    [SerializeField] private float zoomSpeed = 2f;
    //smoothing
    [SerializeField] private float zoomLerpSpeed = 10f;

    [SerializeField] private float mouseSensitivity = 4.5f;

    //how close to player you can get 
    [SerializeField] private float minDistance = 3f;
    [SerializeField] private float maxDistance = 15f;

    //add mouse sensitivity 
    //[SerializeField] private MouseSensitivity  mouseSensitivity;

    //private camera rotation
    //private CameraRotation _cameraRotation;

    //input values
    //private Vector2 _input;

    //variables
    private PlayerControls controls;
    private CinemachineCamera cam;
    private CinemachineOrbitalFollow orbital;

    private CinemachineInputAxisController inputAxis;

    //save difference in scroll 
    private Vector2 scrollDelta;

    //for smooth mouse movement
    private float targetZoom;
    private float currentZoom;



    void Start()
    {
        controls = new PlayerControls();
        controls.Enable();
        controls.Camera.MouseZoom.performed += HandleMouseScroll;

        //hides the cursor during gameplay
        //Cursor.lockState = CursorLockMode.Locked;
        cam = GetComponent<CinemachineCamera>();
        orbital = cam.GetComponent<CinemachineOrbitalFollow>();
        inputAxis = cam.GetComponent<CinemachineInputAxisController>();
        targetZoom = currentZoom = orbital.Radius;
        setSensitivity(mouseSensitivity);

    }

    
    private void HandleMouseScroll(InputAction.CallbackContext context)
    {
        scrollDelta = context.ReadValue<Vector2>();
        Debug.Log($"Mouse is scrolling. Value: {scrollDelta}");

    }

   /*  public void Look(InputAction.CallbackContext context)
    {
        _input = context.ReadValue<Vector2>();
    }
     */
    void Update()
    {
        if (scrollDelta.y != 0)
        {
            if (orbital != null)
            {
                targetZoom = Mathf.Clamp(orbital.Radius - scrollDelta.y * zoomSpeed , minDistance, maxDistance);
                scrollDelta = Vector2.zero;
            }
        }

        currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * zoomLerpSpeed);
        orbital.Radius = currentZoom;

    }

    public void setSensitivity(float newSpeed)
    {   
        
        foreach (var c in inputAxis.Controllers)
        {   
            //this is how we will set the mouse sensitivity
            if(c.Name == "Look Orbit X")
            {  
               //Debug.Log("You found me");
               c.Input.Gain = newSpeed;
            }
        }
        
        /* 
        GenericPropertyJSON:{"name":"data","type":-1,"children":[{"name":"Name","type":3,"val":"Look Orbit X"},{"name":"Owner","type":5,"val":"UnityEditor.ObjectWrapperJSON:{\"guid\":\"\",\"localId\":0,\"type\":0,\"instanceID\":44582}"},{"name":"Enabled","type":1,"val":true},{"name":"Input","type":-1,"children":[{"name":"InputAction","type":5,"val":"UnityEditor.ObjectWrapperJSON:{\"guid\":\"1d6e640e716dc4ff6989b73d02023f2b\",\"localId\":-5630151704836100654,\"type\":3,\"instanceID\":44610}"},{"name":"Gain","type":2,"val":3},{"name":"CancelDeltaTime","type":1,"val":false}]},{"name":"InputValue","type":2,"val":0},{"name":"Driver","type":-1,"children":[{"name":"AccelTime","type":2,"val":0.1},{"name":"DecelTime","type":2,"val":0.2}]}]}
        */
    }




    
}

/* [Serializable]
public struct MouseSensitivity
{
    public float horizontal;
    public float vertical;
}

public struct CameraRotation
{
    public float Pitch;
    public float Yaw;
}
 */
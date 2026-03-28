using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{   
    //how we will get player look an transform according to that
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float speed = 5f;
    [SerializeField] private float jumpHeight = 2f;

    [SerializeField] private float gravity = -9.8f;
    
    //[SerializeField] private float rotationSpeed = 250f;
  
    [SerializeField] private InputActionAsset inputActions;
    
    [SerializeField] private bool shouldFaceMoveDirection = false;
  
    //variables
    private InputActionMap gameplayMap;
    private CharacterController controller;
    private Vector3 moveInput;
    private Vector3 velocity;

    //flying 


    void Awake()
    {  
       //this was needed so that gameplay doesn't become a project wide action
       gameplayMap = inputActions.FindActionMap("GamePlay", throwIfNotFound: true);
    
    }
    void Start()
    {   controller = GetComponent<CharacterController>();  }

    void OnEnable()
    { gameplayMap.Enable(); }

    void OnDisable()
    { gameplayMap.Disable(); }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
        //Debug.Log($"Move Input:{moveInput}");
    }
  

    public void OnJump(InputAction.CallbackContext context)
    {
        //Debug.Log($"Jumping:{context.performed} - Is Grounded: {controller.isGrounded} ");
        //Debug.Log("This happens when");
        if (context.performed && controller.isGrounded)
        {
            Debug.Log("Jump");
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        
        }
     


    }
    void Update() 
    {    
        
        Walking();
        Flying();
        
     
    }
   
    //naming can (onJump) but it is quite a procees so I am neglecting that
    private void Flying()
    {  
        //inAir = false;
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
       
    }

    private void Walking()
    {   

        //rotation function later 
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;
        controller.Move(moveDirection * speed * Time.deltaTime);

        if (shouldFaceMoveDirection && moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion toRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, toRotation, 10f * Time.deltaTime);
        }
    

    }




     
}

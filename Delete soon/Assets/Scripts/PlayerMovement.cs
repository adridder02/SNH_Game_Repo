using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{   
    Rigidbody playerRigid;
    public InputActionAsset playerMovement;
    [SerializeField] GameObject camrea;
    [SerializeField ] float speed;
    [SerializeField ] float rotationSpeed;
    
    private Vector2 m_moveMent;
    private Vector2 m_lookAmt;
    private InputAction playerMoveAction;
    private InputAction playerLookAction;

    private void OnEnable()
    {
        playerMovement.FindActionMap("Player").Enable();
    }
    private void Awake()
    {
        playerMoveAction = InputSystem.actions.FindAction("Move");
        playerLookAction = InputSystem.actions.FindAction("Look");
        playerRigid = GetComponent<Rigidbody>();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        m_moveMent = playerMoveAction.ReadValue<Vector2>();
        m_lookAmt = playerLookAction.ReadValue<Vector2>();

    }

  
    private void FixedUpdate()
    {
        Walking();
        Rotating();
    }

    private void Walking()
    {
        float _y = camrea.transform.rotation.y;
        playerRigid.MovePosition(playerRigid.position + transform.forward * m_moveMent.y * speed * Time.deltaTime);
    }
    private void Rotating()
    {
        if(m_moveMent.y != 0)
        {
            float rotationAmt = m_lookAmt.x * rotationSpeed* Time.deltaTime;
            Quaternion deltaRot = Quaternion.Euler(0,rotationAmt,0);
            playerRigid.MoveRotation(playerRigid.rotation * deltaRot);
        }
    }
}

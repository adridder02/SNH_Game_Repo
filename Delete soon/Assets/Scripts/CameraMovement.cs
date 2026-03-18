using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMovement : MonoBehaviour
{
    //!This object is the camrea that follows the player
    [SerializeField] GameObject camera;
    [SerializeField] InputAction mouseMovement;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //mouseMovement = new InputAction();
       //mouseMovement.UI.Enable();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

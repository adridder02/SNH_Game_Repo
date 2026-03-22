using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

public class BuildingSystem : MonoBehaviour
{
    public static BuildingSystem current;
    public GridLayout gridLayout;
    private Grid grid;

    [SerializeField] private Tilemap MainTilemap;
    [SerializeField] private TileBase whiteTile;

    public GameObject prefab1;
    public GameObject prefab2; 
    private InputAction actionA;
    private InputAction actionB;

    private PlaceableObject objectToPlace;

    #region  Unity methods

    private void Awake()
    {
        current = this;
        grid = gridLayout.gameObject.GetComponent<Grid>();    
        
        // Bind A key
        actionA = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/a");
        // Bind B key
        actionB = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/b");
    }

    private void OnEnable()
    {
        actionA.Enable();
        actionB.Enable();

        actionA.performed += OnActionPerformed;
        actionB.performed += OnActionPerformed;
    }

    private void OnDisable()
    {
        //actionA.performed -= OnActionPerformed;
        //actionB.performed -= OnActionPerformed;

        actionA.Disable();
        actionB.Disable();
    }

    private void OnActionPerformed(InputAction.CallbackContext context)
    {
        // Check which key triggered the event
        if (context.control == Keyboard.current.aKey)
        { InitializeWithObject(prefab1); Debug.Log("A"); }
        else if (context.control == Keyboard.current.bKey)
        { InitializeWithObject(prefab2); Debug.Log("B");}       
    }

    #endregion

    #region  Utils
    
    public static Vector3 GetMouseWorldPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;
        if(Physics.Raycast(ray, out hit))
        {
            return hit.point;
        }
        else
        {
            return Vector3.zero;
        }
    }

    public Vector3 SnapCoordinateToGrid(Vector3 position)
    {
        Vector3Int cellPos = gridLayout.WorldToCell(position);
        position = grid.GetCellCenterWorld(cellPos);
        return position;
    }

    #endregion

    #region Building Placement
     
     public void InitializeWithObject(GameObject prefab)
    {
        Vector3 position = SnapCoordinateToGrid(Vector3.zero);

        GameObject obj = Instantiate(prefab, position, Quaternion.identity);
        objectToPlace = obj.GetComponent<PlaceableObject>();
        obj.AddComponent<ObjectDrag>();
    }


    #endregion
}

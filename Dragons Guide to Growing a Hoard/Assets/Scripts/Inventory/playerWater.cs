using UnityEngine;

public class playerWater : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    /*[Header("Water Inventory")]
    [SerializeField] private float maxWaterRefill = 20f;
    private static float waterPool = 20f;
    [SerializeField] private ImageFillBar waterBar;//drag and drop
    */
    [Header("Player Inventory")]
    [SerializeField] private PlayerInventory pI;//drag and drop
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnCollisionEnter(Collision collision)
    {
        int waterLayer = LayerMask.NameToLayer("WaterRefill");
        if (collision.gameObject.layer == waterLayer)
        {
            if(pI != null)
                pI.refillWaterPool();
            Debug.Log($"Water is refilled");
        }
    }

}

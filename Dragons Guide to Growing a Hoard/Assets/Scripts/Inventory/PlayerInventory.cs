using UnityEngine;
using System.Collections.Generic;

public class PlayerInventory : MonoBehaviour
{
    [Header("This is the Player Inventory")]
    [SerializeField] public List<GameObject> plantsInInventory = new List<GameObject>();
    [SerializeField] private int inventorySize = 0;
    [SerializeField] private int maxInventorySize = 9;
    [Header("This is Water Inventory")]
    [SerializeField] private float maxWaterRefill = 20f;
    private float waterPool = 20f;


    void Start()
    {
        // Initialize inventory size from the list (in case it was edited in Inspector)
        inventorySize = plantsInInventory.Count;
        Debug.Log($"PlayerInventory initialized with {inventorySize} items");
    }

    void OnCollisionEnter(Collision collision)
    {
        // Check if the collided object is on layer "Plant_Seeds"
        int plantLayer = LayerMask.NameToLayer("Plant_Seeds");
        if (collision.gameObject.layer == plantLayer && !IsInventoryFull())
        {
            CollectablePlant plant = collision.gameObject.GetComponent<CollectablePlant>();
            if (plant != null)
            {
                GameObject plantPrefab = plant.GetPlantPrefab();
                if (plantPrefab != null)
                {
                    AddPlantToInventory(plantPrefab);
                    Destroy(collision.gameObject); // Destroy the plant object after collecting it
                    Debug.Log($"Collected: {plantPrefab.name}. Inventory: {inventorySize}/{maxInventorySize}");
                }
                else
                {
                    Debug.LogWarning("Plant prefab is null!");
                }
            }
            else
            {
                Debug.LogWarning("No CollectablePlant component found on the object!");
            }
        }
        int waterLayer = LayerMask.NameToLayer("WaterRefill");
        if(collision.gameObject.layer == waterLayer){
            this.refillWaterPool();
            Debug.Log($"Water is refilled to: {this.waterPool}");
        }
    }

    public bool IsInventoryFull()
    {
        return inventorySize >= maxInventorySize;
    }

    public List<GameObject> GetInventory()
    {
        return plantsInInventory;
    }

    public int GetInventorySize()
    {
        return inventorySize;
    }

    public int GetMaxInventorySize()
    {
        return maxInventorySize;
    }

    public void RemoveFirstPlant(GameObject plantPrefab)
    {
        int index = plantsInInventory.FindIndex(p => p == plantPrefab);
        if (index != -1)
        {
            plantsInInventory.RemoveAt(index);
            inventorySize--;
            Debug.Log($"Removed: {plantPrefab.name}. Inventory: {inventorySize}/{maxInventorySize}");
        }
        else
        {
            Debug.LogWarning($"Plant {plantPrefab?.name} not found in inventory!");
        }
    }

    public void RemovePlantAtIndex(int index)
    {
        if (index >= 0 && index < plantsInInventory.Count)
        {
            string plantName = plantsInInventory[index]?.name ?? "Unknown";
            plantsInInventory.RemoveAt(index);
            inventorySize--;
            Debug.Log($"Removed item at index {index}: {plantName}. Inventory: {inventorySize}/{maxInventorySize}");
        }
        else
        {
            Debug.LogWarning($"Index {index} is out of range!");
        }
    }

    public void AddPlantToInventory(GameObject plantPrefab)
    {
        if (!IsInventoryFull())
        {
            plantsInInventory.Add(plantPrefab);
            inventorySize++;
            Debug.Log($"Added: {plantPrefab.name}. Inventory: {inventorySize}/{maxInventorySize}");
        }
        else
        {
            Debug.LogWarning("Inventory is full! Cannot add more items.");
        }
    }

    public void ClearInventory()
    {
        plantsInInventory.Clear();
        inventorySize = 0;
        Debug.Log("Inventory cleared!");
    }

    // Check if inventory contains a specific plant
    public bool ContainsPlant(GameObject plantPrefab)
    {
        return plantsInInventory.Contains(plantPrefab);
    }

    // Get the first plant in inventory (useful for crafting or using items)
    public GameObject GetFirstPlant()
    {
        if (plantsInInventory.Count > 0)
        {
            return plantsInInventory[0];
        }
        return null;
    }
    public float getWaterPool(){
        return this.waterPool;
    }
    public void reduceWaterPool(float decreaseW){
        this.waterPool -= decreaseW;
    }
    public void refillWaterPool(){
        this.waterPool = maxWaterRefill;
    }
}
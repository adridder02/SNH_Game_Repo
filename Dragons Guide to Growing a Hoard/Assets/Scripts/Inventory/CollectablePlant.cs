using UnityEngine;

public class CollectablePlant : MonoBehaviour
{
    [Header("Plant Settings")]
    [SerializeField] private GameObject plantPrefab; // The prefab to add to inventory
    [SerializeField] private string plantName = "Plant";
    [SerializeField] private Sprite plantIcon; // Optional: for UI

    public GameObject GetPlantPrefab()
    {
        // If plantPrefab is not set, use this game object itself
        if (plantPrefab == null)
        {
            plantPrefab = gameObject;
        }
        return plantPrefab;
    }

    public string GetPlantName()
    {
        return plantName;
    }

    public Sprite GetPlantIcon()
    {
        return plantIcon;
    }
}
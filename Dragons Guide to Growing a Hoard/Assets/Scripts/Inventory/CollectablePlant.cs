using UnityEngine;

public class CollectablePlant : MonoBehaviour
{
    [Header("Plant Settings")]
    [Tooltip("REQUIRED: the actual plant prefab asset to add to the player's inventory. " +
             "This must be a prefab from your Project window (the same kind used when " +
             "planting into pots), NOT this scene object itself.")]
    [SerializeField] private GameObject plantPrefab;
    [SerializeField] private string plantName = "Plant";
    [SerializeField] private Sprite plantIcon; // Optional: for UI

    public GameObject GetPlantPrefab()
    {
        if (plantPrefab == null)
        {
            Debug.LogError($"[CollectablePlant] '{gameObject.name}' has no Plant Prefab assigned! " +
                            "Drag the plant's prefab asset into the Plant Prefab field in the Inspector.", this);
            return null;
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
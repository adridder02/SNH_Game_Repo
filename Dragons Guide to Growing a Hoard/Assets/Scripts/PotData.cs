using UnityEngine;

[CreateAssetMenu(fileName = "NewPotData", menuName = "Greenhouse/Pot Data")]
public class PotData : ScriptableObject
{
    [Header("Pot Info")]
    public string potName = "Pot";

    // size in grid cells: (1,1) (2,1) (2,2)
    public Vector2Int size = Vector2Int.one;

    [Header("Prefabs")]
    public GameObject potPrefab;          // the actual placed pot
    public GameObject previewPrefab;      // semi transparent preview 
}

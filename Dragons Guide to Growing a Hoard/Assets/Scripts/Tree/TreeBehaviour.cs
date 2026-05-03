using UnityEngine;

public class TreeBehaviour : MonoBehaviour
{
    [SerializeField]public PlantState Plant;
    public int PlantLevelScores = 0;
    
    public void TreeValues()
    {
        if (Plant.CurrentState == PlantStateEnum.Intermediate)
        {
            PlantLevelScores += 1;
            Debug.Log("Your levels " + PlantLevelScores);
        } 
    }
}

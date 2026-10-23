using UnityEngine;

// =============================================================
// PlantMissionTracker.cs
// -------------------------------------------------------------
// Completes the tutorial's plant mission tasks from real gameplay (via PlantEvents). Put ONE on any
// object in the tutorial scene and assign:
//   Mission  - the MissionData that holds complete_plant / harvest / plant_new / use_puff
//   Species  - the PlantSpeciesData to track (Pollen Puff)
// Tasks are completed with CompleteOrderedTask, so each only counts once the ones before it in the
// mission's list are done - that's what makes plant_new mean "plant ANOTHER one" (planting the first
// puff happens while earlier tasks are still pending, so it's ignored).
// Task ids can be changed below if your ids differ.
// =============================================================
public class PlantMissionTracker : MonoBehaviour
{
    [SerializeField] private MissionData mission;
    [SerializeField] private PlantSpeciesData species;

    [Header("Task ids (must match the MissionData)")]
    [SerializeField] private string completePlantTaskId = "complete_plant";
    [SerializeField] private string harvestTaskId = "harvest";
    [SerializeField] private string plantNewTaskId = "plant_new";
    [SerializeField] private string usePuffTaskId = "use_puff";

    private void OnEnable()
    {
        PlantEvents.Completed += OnCompleted;
        PlantEvents.Harvested += OnHarvested;
        PlantEvents.Planted += OnPlanted;
        PlantEvents.ConsumableUsed += OnConsumableUsed;
    }

    private void OnDisable()
    {
        PlantEvents.Completed -= OnCompleted;
        PlantEvents.Harvested -= OnHarvested;
        PlantEvents.Planted -= OnPlanted;
        PlantEvents.ConsumableUsed -= OnConsumableUsed;
    }

    private bool Matches(PlantSpeciesData s) => mission != null && (species == null || s == species);

    private void Complete(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        MissionProgressManager.Instance?.CompleteOrderedTask(mission, id);
    }

    private void OnCompleted(PlantSpeciesData s) { if (Matches(s)) Complete(completePlantTaskId); }
    private void OnHarvested(PlantSpeciesData s) { if (Matches(s)) Complete(harvestTaskId); }
    private void OnPlanted(PlantSpeciesData s) { if (Matches(s)) Complete(plantNewTaskId); }
    private void OnConsumableUsed(AbilityItemData d, PlantSpeciesData s) { if (Matches(s)) Complete(usePuffTaskId); }
}

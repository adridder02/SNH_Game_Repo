using System;

// =============================================================
// PlantEvents.cs
// -------------------------------------------------------------
// Static "something happened to a plant" events, so scene-level scripts (e.g. PlantMissionTracker)
// can react without every pot/plant needing a reference to them. The species is the plant's
// PlantState.journalSpecies (may be null for plants without one).
// =============================================================
public static class PlantEvents
{
    /// <summary>A plant was put into a pot (after it actually went in).</summary>
    public static event Action<PlantSpeciesData> Planted;
    /// <summary>A plant reached full growth (first time, per plant instance).</summary>
    public static event Action<PlantSpeciesData> Completed;
    /// <summary>A plant was harvested from a pot.</summary>
    public static event Action<PlantSpeciesData> Harvested;
    /// <summary>A consumable was applied to a plant in a pot.</summary>
    public static event Action<AbilityItemData, PlantSpeciesData> ConsumableUsed;

    public static void RaisePlanted(PlantSpeciesData s) => Planted?.Invoke(s);
    public static void RaiseCompleted(PlantSpeciesData s) => Completed?.Invoke(s);
    public static void RaiseHarvested(PlantSpeciesData s) => Harvested?.Invoke(s);
    public static void RaiseConsumableUsed(AbilityItemData d, PlantSpeciesData s) => ConsumableUsed?.Invoke(d, s);
}

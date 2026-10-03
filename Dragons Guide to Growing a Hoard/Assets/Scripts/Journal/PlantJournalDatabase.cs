using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// =============================================================
// PlantJournalDatabase.cs
// -------------------------------------------------------------
// The full list of every plant species that can appear in the
// journal. One asset for the whole game — drag every
// PlantSpeciesData asset you create into allSpecies.
//
// SETUP:
//   Create > Plants > Plant Journal Database
//   Assign it on JournalUIController.
// =============================================================
[CreateAssetMenu(fileName = "PlantJournalDatabase", menuName = "Plants/Plant Journal Database")]
public class PlantJournalDatabase : ScriptableObject
{
    public List<PlantSpeciesData> allSpecies = new List<PlantSpeciesData>();

    /// <summary>All species belonging to one journal row (Sunny/Dark/Water), in list order.</summary>
    public List<PlantSpeciesData> GetByCategory(PlantType category)
    {
        return allSpecies.Where(s => s != null && s.category == category).ToList();
    }

    /// <summary>All species assigned to one Progress-page room (Main/East/West), in list order. Unlike
    /// GetByCategory's journal rows, the Progress page shows every species here regardless of discovery
    /// state — undiscovered ones just render with their disabled icon instead of being left out.</summary>
    public List<PlantSpeciesData> GetByRoom(RoomType room)
    {
        return allSpecies.Where(s => s != null && s.room == room).ToList();
    }
}
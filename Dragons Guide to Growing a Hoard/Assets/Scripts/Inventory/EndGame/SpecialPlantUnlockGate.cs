// =============================================================
// SpecialPlantUnlockGate.cs
// -------------------------------------------------------------
// Whether a PlantSpeciesData with requiresRoomUnlock = true (see
// PlantSpeciesData.cs) can currently be planted: its room must be fully
// discovered/completed in the Journal (same "every species in this room
// has hit its gold milestone" check ProgressPageUIController's completion
// bar uses) AND that room's linked ZoneHealth happiness (see
// RoomZoneRegistry) must clear the species' own happinessThreshold.
//
// Called from PotMenuUIController's Choose Plant panel before letting the
// player select one of these species.
// =============================================================
public static class SpecialPlantUnlockGate
{
    /// <summary>Flip this on to skip every check below and treat every requiresRoomUnlock species
    /// as plantable — for testing, so you don't have to 100% a room for real every time. Toggle it
    /// via SpecialPlantUnlockDebug's Inspector checkbox (add that component anywhere in the scene),
    /// or call this directly from your own debug UI/console.</summary>
    public static bool DebugForceUnlocked = false;

    /// <summary>Whether `species` can currently be planted. Always true for species that don't opt
    /// into requiresRoomUnlock at all.</summary>
    public static bool IsPlantable(PlantSpeciesData species, PlantJournalDatabase database, PlantJournalManager journalManager)
    {
        if (species == null || !species.requiresRoomUnlock) return true;
        if (DebugForceUnlocked) return true;

        return IsRoomComplete(species.room, database, journalManager) &&
               IsRoomHappyEnough(species.room, species.happinessThreshold);
    }

    /// <summary>True only if every species assigned to this room (PlantJournalDatabase.GetByRoom)
    /// has hit its completion milestone — same denominator ProgressPageUIController's completion
    /// bar for that room uses, so "the progress bar is full" and this agree by construction.</summary>
    public static bool IsRoomComplete(RoomType room, PlantJournalDatabase database, PlantJournalManager journalManager)
    {
        if (database == null || journalManager == null) return false;

        var speciesInRoom = database.GetByRoom(room);
        if (speciesInRoom.Count == 0) return false; // nothing assigned to the room — can't be "full"

        foreach (var s in speciesInRoom)
        {
            if (s == null) continue;
            if (!journalManager.IsCompleted(s)) return false;
        }
        return true;
    }

    public static bool IsRoomHappyEnough(RoomType room, float threshold)
    {
        ZoneHealth zone = RoomZoneRegistry.Instance != null ? RoomZoneRegistry.Instance.GetZone(room) : null;
        if (zone == null) return false; // no zone wired up — treat as not happy enough rather than silently passing
        return zone.ZoneHappiness >= threshold;
    }

    /// <summary>Human-readable reason to show the player when they try to select a locked species.
    /// Empty string if it's actually plantable right now.</summary>
    public static string GetLockedReason(PlantSpeciesData species, PlantJournalDatabase database, PlantJournalManager journalManager)
    {
        if (IsPlantable(species, database, journalManager)) return "";

        bool roomDone = IsRoomComplete(species.room, database, journalManager);
        bool happy = IsRoomHappyEnough(species.room, species.happinessThreshold);

        if (!roomDone && !happy)
            return "You need to fully discover every plant in this room and keep it happy before you can grow this.";
        if (!roomDone)
            return "You need to fully discover every plant in this room first.";
        return "This room needs to be happier before you can grow this.";
    }
}

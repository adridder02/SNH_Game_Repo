using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// =============================================================
// PlantJournalManager.cs
// -------------------------------------------------------------
// Owns discovery state: which species has the player harvested
// at least once? Persists via PlayerPrefs (swap Save()/Load() for
// your real save system if you have one).
//
// HOOKED IN FROM: PlayerInventory.AddPlantToInventory(), which is
// already the single entry point for harvesting + returning a
// plant from a pot — see the comment there.
//
// SCENE SETUP:
//   Put this on a persistent GameObject (same rule as the other
//   manager singletons in this project, e.g. GameInputModeManager)
//   so it survives scene loads and Awake() has definitely run
//   before anything tries to mark a discovery.
// =============================================================
public class PlantJournalManager : MonoBehaviour
{
    public static PlantJournalManager Instance { get; private set; }

    [SerializeField] private PlantJournalDatabase database;

    private const string SaveKey = "PlantJournal_Discovered";
    private readonly HashSet<string> discoveredIds = new HashSet<string>();

    // "Completed" tracks the gold-milestone progression system (a species that's been fully
    // ripened via PlantProgress.IsComplete at least once — see PlantProgress.Update()), which is
    // stricter and separate from "discovered" (harvested at least once, any ripeness). A species
    // is always discovered before it can be completed, but the two sets are kept independent
    // rather than assumed-implied, in case future content ever discovers a species some other way.
    private readonly HashSet<string> completedIds = new HashSet<string>();

    // ---- Care-row guesses (the journal dropdowns) -------------------------------------------------
    // Per species: what the player has picked for water / light / soil (-1 = unanswered, otherwise the
    // enum value), plus whether they pressed "fill in answers", which locks the dropdowns to the truth.
    private const string GuessKey = "PlantJournal_Guesses";
    private readonly Dictionary<string, CareGuess> guesses = new Dictionary<string, CareGuess>();

    [Serializable]
    public class CareGuess
    {
        public string id;
        public int water = -1, light = -1, soil = -1;
        public bool locked;
        // The player's own picks, remembered while the answers are revealed so unlocking can give them back.
        public int prevWater = -1, prevLight = -1, prevSoil = -1;
    }

    public CareGuess GetGuess(PlantSpeciesData species)
    {
        if (species == null) return null;
        if (!guesses.TryGetValue(species.ResolvedId, out CareGuess g))
        {
            g = new CareGuess { id = species.ResolvedId };
            guesses[g.id] = g;
        }
        return g;
    }

    /// <summary>Stores one dropdown pick. Ignored once the species is locked.</summary>
    public void SetGuess(PlantSpeciesData species, int water, int light, int soil)
    {
        CareGuess g = GetGuess(species);
        if (g == null || g.locked) return;
        g.water = water; g.light = light; g.soil = soil;
        SaveGuesses();
    }

    /// <summary>Fills in the correct answers and locks the dropdowns for this species. The player's own picks
    /// are remembered so HideAnswers can bring them back.</summary>
    public void RevealAnswers(PlantSpeciesData species)
    {
        CareGuess g = GetGuess(species);
        if (g == null) return;
        if (!g.locked)
        {
            g.prevWater = g.water; g.prevLight = g.light; g.prevSoil = g.soil;
        }
        g.water = (int)species.waterRequirement;
        g.light = (int)species.lightRequirement;
        g.soil = (int)species.preferredSoilDisplay;
        g.locked = true;
        SaveGuesses();
    }

    /// <summary>Unlocks the dropdowns again and puts back whatever the player had picked before revealing.</summary>
    public void HideAnswers(PlantSpeciesData species)
    {
        CareGuess g = GetGuess(species);
        if (g == null || !g.locked) return;
        g.water = g.prevWater; g.light = g.prevLight; g.soil = g.prevSoil;
        g.locked = false;
        SaveGuesses();
    }

    private void SaveGuesses()
    {
        PlayerPrefs.SetString(GuessKey, JsonUtility.ToJson(new GuessSave { entries = guesses.Values.ToList() }));
        PlayerPrefs.Save();
    }

    private void LoadGuesses()
    {
        guesses.Clear();
        if (!PlayerPrefs.HasKey(GuessKey)) return;
        GuessSave data = JsonUtility.FromJson<GuessSave>(PlayerPrefs.GetString(GuessKey));
        if (data?.entries == null) return;
        foreach (CareGuess g in data.entries)
            if (g != null && !string.IsNullOrEmpty(g.id)) guesses[g.id] = g;
    }

    [Serializable]
    private class GuessSave { public List<CareGuess> entries; }

    /// <summary>Fired whenever a new species is discovered or completed (or state is loaded), so JournalUIController / ProgressPageUIController can redraw.</summary>
    public event Action OnJournalChanged;

    public PlantJournalDatabase Database => database;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
        LoadGuesses();
    }

    public bool IsDiscovered(PlantSpeciesData species)
    {
        return species != null && discoveredIds.Contains(species.ResolvedId);
    }

    /// <summary>Whether EVERY species at exactly this tier (across the whole database, all rooms —
    /// tiers aren't scoped per-room) has hit its gold/fully-ripened milestone at least once — see
    /// IsCompleted. Crystal species (requiresRoomUnlock == true) are excluded regardless of
    /// whatever tier value they happen to have, since they're not really "in" a tier — they're the
    /// separate top-of-the-pyramid reward above tiers 1-3. Used by JournalUIController's gradual
    /// description-unlock rules (see its GetUnlockedDescriptions), not by anything else — this
    /// doesn't affect the Progress page's own tier grouping, which reads tier/requiresRoomUnlock
    /// directly off each species instead.</summary>
    public bool AllOfTierCompleted(int tier)
    {
        if (database == null || database.allSpecies == null) return false;

        bool anyAtThisTier = false;

        foreach (PlantSpeciesData species in database.allSpecies)
        {
            if (species == null || species.requiresRoomUnlock || species.tier != tier) continue;

            anyAtThisTier = true;
            if (!IsCompleted(species)) return false;
        }

        // No species exist at this tier at all — treat as "not satisfied" rather than vacuously
        // true, so an empty/misconfigured tier doesn't silently unlock everything above it.
        return anyAtThisTier;
    }

    /// <summary>Marks a species as seen. Returns true only if this was a NEW discovery (useful for a "New!" popup later).</summary>
    public bool MarkDiscovered(PlantSpeciesData species)
    {
        if (species == null) return false;

        if (discoveredIds.Add(species.ResolvedId))
        {
            Debug.Log($"[PlantJournalManager] Discovered: {species.displayName}");
            Save();
            OnJournalChanged?.Invoke();
            return true;
        }
        return false;
    }

    public bool IsCompleted(PlantSpeciesData species)
    {
        return species != null && completedIds.Contains(species.ResolvedId);
    }

    /// <summary>Marks a species as having reached its gold/fully-ripened milestone at least once (called
    /// from PlantProgress the first time a plant of this species hits IsComplete). Returns true only if
    /// this was a NEW completion, so callers can react (e.g. a "Gold!" popup) without re-triggering on
    /// every subsequent harvest of the same species.</summary>
    public bool MarkCompleted(PlantSpeciesData species)
    {
        if (species == null) return false;

        // A species can't be "more discovered" than completed, but make sure the journal entry
        // exists either way — belt-and-braces in case completion is ever reached through a path
        // that skipped MarkDiscovered.
        discoveredIds.Add(species.ResolvedId);

        if (completedIds.Add(species.ResolvedId))
        {
            Debug.Log($"[PlantJournalManager] Gold milestone reached: {species.displayName}");
            Save();
            OnJournalChanged?.Invoke();
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------
    // PERSISTENCE
    // ------------------------------------------------------------
    private void Save()
    {
        var data = new SaveData { ids = discoveredIds.ToList(), completedIds = completedIds.ToList() };
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    private void Load()
    {
        discoveredIds.Clear();
        completedIds.Clear();

        if (!PlayerPrefs.HasKey(SaveKey))
        {
            OnJournalChanged?.Invoke();
            return;
        }

        string json = PlayerPrefs.GetString(SaveKey);
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        if (data?.ids != null)
            foreach (string id in data.ids)
                discoveredIds.Add(id);
        if (data?.completedIds != null)
            foreach (string id in data.completedIds)
                completedIds.Add(id);

        OnJournalChanged?.Invoke();
    }

    /// <summary>Debug/testing helper — wipes all discovery + completion progress.</summary>
    [ContextMenu("Debug: Reset Journal Progress")]
    public void ResetProgress()
    {
        discoveredIds.Clear();
        completedIds.Clear();
        PlayerPrefs.DeleteKey(SaveKey);
        guesses.Clear();
        PlayerPrefs.DeleteKey(GuessKey);
        OnJournalChanged?.Invoke();
        Debug.Log("[PlantJournalManager] Journal progress reset.");
    }

    [Serializable]
    private class SaveData
    {
        public List<string> ids;
        public List<string> completedIds;
    }
}
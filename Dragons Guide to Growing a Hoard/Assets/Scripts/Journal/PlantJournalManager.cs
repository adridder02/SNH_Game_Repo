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
        //DontDestroyOnLoad(gameObject);
        Load();
    }

    public bool IsDiscovered(PlantSpeciesData species)
    {
        return species != null && discoveredIds.Contains(species.ResolvedId);
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
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// =============================================================
// NewItemTracker.cs
// -------------------------------------------------------------
// Owns "new item" badge state for the Inventory and Journal HUD
// icons, plus their in-menu per-slot badges. Tracked PER ITEM TYPE
// (a plant species' ResolvedId, or an AbilityItemData asset's
// .name) — getting a second copy of something already owned never
// re-flags it. Persists via PlayerPrefs, same pattern as
// PlantJournalManager.
//
// TWO INDEPENDENT DOMAINS, each with its own "ever acquired" +
// "still unseen" pair, because the Inventory badge and the Journal
// badge for the SAME plant clear INDEPENDENTLY — viewing it in one
// menu doesn't clear the other (you could open the Journal without
// ever opening Inventory, or vice versa):
//   - Inventory — any InventoryItemInstance (plant) or
//     AbilityItemInstance (consumable/placeable) type. Hooked from
//     PlayerInventory.AddPlantToInventory() and
//     PlayerAbilityInventory.Add().
//   - Journal — plant species specifically, keyed the SAME way
//     PlantJournalManager keys discovery (species.ResolvedId), so
//     the two systems can never disagree about which plant they're
//     both talking about. Hooked from the same
//     AddPlantToInventory() call site, right alongside
//     PlantJournalManager.MarkDiscovered().
//
// CLEARED FROM: InventorySlotUI.OnPointerClick (inventory, both the
// main grid and Available — see that file), JournalSlotUI.OnPointerClick
// (journal).
//
// SCENE SETUP: same rule as PlantJournalManager — put this on a
// persistent GameObject (ideally the SAME one, or right next to it)
// so it survives scene loads and Awake() has definitely run before
// anything tries to mark something acquired.
// =============================================================
public class NewItemTracker : MonoBehaviour
{
    public static NewItemTracker Instance { get; private set; }

    private const string SaveKey = "NewItemTracker_State";

    // Inventory domain
    private readonly HashSet<string> everAcquiredInventory = new HashSet<string>();
    private readonly HashSet<string> unseenInventory = new HashSet<string>();

    // Journal domain
    private readonly HashSet<string> everAcquiredJournal = new HashSet<string>();
    private readonly HashSet<string> unseenJournal = new HashSet<string>();

    /// <summary>Fired whenever anything here changes — HUD dots and in-menu slot badges should
    /// both refresh off this event rather than polling every frame. Also lets a slot showing the
    /// SAME item type as one just clicked elsewhere (e.g. two copies of the same new plant
    /// species sitting in different grid cells) clear its badge too, without needing a full grid
    /// redraw to notice.</summary>
    public event Action OnChanged;

    public bool HasAnyUnseenInventory => unseenInventory.Count > 0;
    public bool HasAnyUnseenJournal => unseenJournal.Count > 0;

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
    }

    // ---------------------------------------------------------------
    // INVENTORY domain
    // ---------------------------------------------------------------
    public bool IsUnseenInInventory(string id) => !string.IsNullOrEmpty(id) && unseenInventory.Contains(id);

    /// <summary>Call whenever an item TYPE enters the player's inventory (a plant harvested, an
    /// ability item stack gained). Safe to call every time, even for a type the player already
    /// owns — only the very first time for a given id actually flags it as new.</summary>
    public void MarkAcquiredInventory(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!everAcquiredInventory.Add(id)) return; // already had this type before — not new

        unseenInventory.Add(id);
        Save();
        OnChanged?.Invoke();
    }

    /// <summary>Call when the player clicks/views this item's slot in Inventory — main grid or
    /// Available, both call this the same way. Clears its badge permanently (until, in theory, a
    /// ResetProgress()).</summary>
    public void MarkSeenInInventory(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!unseenInventory.Remove(id)) return; // wasn't flagged as new — nothing to clear

        Save();
        OnChanged?.Invoke();
    }

    // ---------------------------------------------------------------
    // JOURNAL domain
    // ---------------------------------------------------------------
    public bool IsUnseenInJournal(string id) => !string.IsNullOrEmpty(id) && unseenJournal.Contains(id);

    public void MarkAcquiredJournal(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!everAcquiredJournal.Add(id)) return;

        unseenJournal.Add(id);
        Save();
        OnChanged?.Invoke();
    }

    public void MarkSeenInJournal(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!unseenJournal.Remove(id)) return;

        Save();
        OnChanged?.Invoke();
    }

    // ------------------------------------------------------------
    // PERSISTENCE
    // ------------------------------------------------------------
    private void Save()
    {
        var data = new SaveData
        {
            everAcquiredInventory = everAcquiredInventory.ToList(),
            unseenInventory = unseenInventory.ToList(),
            everAcquiredJournal = everAcquiredJournal.ToList(),
            unseenJournal = unseenJournal.ToList(),
        };
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    private void Load()
    {
        everAcquiredInventory.Clear();
        unseenInventory.Clear();
        everAcquiredJournal.Clear();
        unseenJournal.Clear();

        if (!PlayerPrefs.HasKey(SaveKey))
        {
            OnChanged?.Invoke();
            return;
        }

        string json = PlayerPrefs.GetString(SaveKey);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        if (data?.everAcquiredInventory != null)
            foreach (string id in data.everAcquiredInventory) everAcquiredInventory.Add(id);
        if (data?.unseenInventory != null)
            foreach (string id in data.unseenInventory) unseenInventory.Add(id);
        if (data?.everAcquiredJournal != null)
            foreach (string id in data.everAcquiredJournal) everAcquiredJournal.Add(id);
        if (data?.unseenJournal != null)
            foreach (string id in data.unseenJournal) unseenJournal.Add(id);

        OnChanged?.Invoke();
    }

    /// <summary>Debug/testing helper — wipes all "new item" badge state (both domains).</summary>
    [ContextMenu("Debug: Reset New Item Badges")]
    public void ResetProgress()
    {
        everAcquiredInventory.Clear();
        unseenInventory.Clear();
        everAcquiredJournal.Clear();
        unseenJournal.Clear();
        PlayerPrefs.DeleteKey(SaveKey);
        OnChanged?.Invoke();
        Debug.Log("[NewItemTracker] New-item badge state reset.");
    }

    [Serializable]
    private class SaveData
    {
        public List<string> everAcquiredInventory;
        public List<string> unseenInventory;
        public List<string> everAcquiredJournal;
        public List<string> unseenJournal;
    }
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =============================================================
// ProgressPageUIController.cs
// -------------------------------------------------------------
// Drives the journal's Progress page — three rooms (Main Hall /
// East / West), each its own page with a grid of species icons
// generated the same way the Plants page grid is (see
// JournalUIController's class comment / ManualSlotLayout): one
// hidden template slot per room, cloned and positioned per species
// at runtime. Prev/Next just swap which room page is active — same
// idea as JournalUIController's top-level ShowPage, one level down.
//
// Unlike the Plants page grid, a room's grid shows EVERY species
// assigned to it, discovered or not — undiscovered ones render with
// the disabled icon instead of being left out (see
// PlantJournalDatabase.GetByRoom).
//
// ROOM ORDER: Main -> East -> West. Main has no Prev, West has no
// Next — RoomOrder below is the single source of truth for that
// sequence, and its indices line up with the Rooms array (mainRoom/
// eastRoom/westRoom in that order).
//
// SETUP (per room, same as the Plants page grid):
//   1. Build mainRoom/eastRoom/westRoom as sibling page GameObjects,
//      each with whatever static decoration that room needs (lorem
//      text, background art, etc.) plus one icon row.
//   2. In each room's icon row, place one real ProgressSlotUI slot —
//      that's the template — and assign it + the row to that room's
//      slotTemplate/iconRow fields below. Hidden at startup and
//      cloned per species in that room, same as JournalSlotUI.
//   3. Assign database; journalManager auto-finds
//      PlantJournalManager.Instance if left empty.
//
// COMPLETION BAR: each room can optionally have its own ImageFillBar
// (RoomPage.progressBar) showing (species completed in this room) /
// (total species assigned to this room) — same denominator as the
// icon grid, so 1 completed out of 5 species in Main Hall fills it
// to 1/5. "Completed" here means the gold milestone (see
// PlantJournalManager.IsCompleted / PlantProgress.ReportGoldMilestone),
// which is stricter than "discovered" — a species can be discovered
// (icon = Active) without being completed (icon = Gold) yet.
// =============================================================
public class ProgressPageUIController : MonoBehaviour
{
    [Serializable]
    private class RoomPage
    {
        [Tooltip("This room's page GameObject. Everything else on the room (lorem text, background " +
                 "art, etc.) lives under here and is left alone — only the icon grid below gets touched.")]
        public GameObject page;

        [Tooltip("Container this room's icons are spawned into. Anchor/pivot must be top-left (0,1), " +
                 "same as the journal/inventory grids.")]
        public RectTransform iconRow;

        [Tooltip("An existing slot GameObject already placed in iconRow, with a ProgressSlotUI " +
                 "component. Hidden at startup and cloned per species in this room.")]
        public ProgressSlotUI slotTemplate;

        [Tooltip("Optional. This room's completion bar — fills according to (species completed in this " +
                 "room) / (total species assigned to this room), same denominator as the icon grid above. " +
                 "Leave empty if this room doesn't show one.")]
        public ImageFillBar progressBar;

        [NonSerialized] public RectTransform slotTemplateRect;
        [NonSerialized] public List<ProgressSlotUI> spawnedSlots = new List<ProgressSlotUI>();
    }

    // Single source of truth for the flip order and for which ends hide Prev/Next.
    // Indices here line up with the Rooms array below (Main, East, West).
    private static readonly RoomType[] RoomOrder = { RoomType.Main, RoomType.East, RoomType.West };

    [Header("Data")]
    [SerializeField] private PlantJournalDatabase database;
    [Tooltip("Auto-found via PlantJournalManager.Instance if left empty.")]
    [SerializeField] private PlantJournalManager journalManager;

    [Header("Rooms — order matches the flip sequence: Main -> East -> West")]
    [SerializeField] private RoomPage mainRoom;
    [SerializeField] private RoomPage eastRoom;
    [SerializeField] private RoomPage westRoom;

    [Header("Grid Layout")]
    [Tooltip("Columns per row before wrapping, shared by all three rooms' grids.")]
    [SerializeField] private int columns = 5;
    [SerializeField] private float cellGapX = 8f;
    [SerializeField] private float cellGapY = 8f;

    [Header("Room Header (optional)")]
    [Tooltip("Optional. Shows the current room's title, e.g. 'The Main Hall'. Leave empty if each " +
             "room page already has its own static title baked in.")]
    [SerializeField] private TMP_Text roomTitleText;

    [Header("Prev/Next")]
    [Tooltip("Flips to the previous room. Hidden on the first room (Main).")]
    [SerializeField] private Button previousRoomButton;
    [Tooltip("Flips to the next room. Hidden on the last room (West).")]
    [SerializeField] private Button nextRoomButton;

    private RoomPage[] Rooms => new[] { mainRoom, eastRoom, westRoom };
    private int currentRoomIndex = 0;

    void Awake()
    {
        if (journalManager == null)
            journalManager = PlantJournalManager.Instance != null ? PlantJournalManager.Instance : FindObjectOfType<PlantJournalManager>();

        foreach (RoomPage room in Rooms)
        {
            if (room?.slotTemplate == null) continue;
            room.slotTemplateRect = room.slotTemplate.GetComponent<RectTransform>();
            room.slotTemplate.gameObject.SetActive(false); // hide the live scene object used as a clone source
        }

        previousRoomButton?.onClick.AddListener(() => StepRoom(-1));
        nextRoomButton?.onClick.AddListener(() => StepRoom(1));
    }

    void OnEnable()
    {
        if (journalManager != null)
            journalManager.OnJournalChanged += RefreshCurrentRoom;

        // Always reopen on the Main Hall, same rule JournalUIController uses for
        // reopening on the Plants page — the Progress page shouldn't remember
        // whatever room it was left on last time.
        currentRoomIndex = 0;
        ShowRoom(currentRoomIndex);
    }

    void OnDisable()
    {
        if (journalManager != null)
            journalManager.OnJournalChanged -= RefreshCurrentRoom;
    }

    /// <summary>Jumps straight to a specific room, bypassing the Prev/Next flip sequence — used by
    /// JournalUIController's room shortcut buttons (Main Room / East Wing / West Wing) so the player
    /// can go directly to a room instead of flipping through Prev/Next to reach it.</summary>
    public void GoToRoom(RoomType room)
    {
        int index = Array.IndexOf(RoomOrder, room);
        if (index < 0) return;

        currentRoomIndex = index;
        ShowRoom(currentRoomIndex);
    }

    private void StepRoom(int direction)
    {
        int newIndex = currentRoomIndex + direction;
        if (newIndex < 0 || newIndex >= RoomOrder.Length) return; // at an end — buttons are hidden here anyway

        currentRoomIndex = newIndex;
        ShowRoom(currentRoomIndex);
    }

    private void ShowRoom(int index)
    {
        RoomPage[] rooms = Rooms;

        for (int i = 0; i < rooms.Length; i++)
            rooms[i]?.page?.SetActive(i == index);

        if (roomTitleText != null)
            roomTitleText.text = GetRoomDisplayName(RoomOrder[index]);

        RefreshCurrentRoom();

        // Hidden (not just disabled) at either end of the room order, matching
        // JournalUIController's Prev/Next behavior on the detail page.
        if (previousRoomButton != null) previousRoomButton.gameObject.SetActive(index > 0);
        if (nextRoomButton != null) nextRoomButton.gameObject.SetActive(index < rooms.Length - 1);
    }

    /// <summary>Repopulates the currently-shown room's grid from the database. Called on room change
    /// and whenever discovery state changes (OnJournalChanged) so a newly-found plant's icon flips
    /// from disabled to active without needing to flip pages away and back.</summary>
    private void RefreshCurrentRoom()
    {
        if (database == null) return;

        RoomPage room = Rooms[currentRoomIndex];
        if (room == null || room.slotTemplate == null || room.iconRow == null)
        {
            Debug.LogWarning("[ProgressPageUIController] Current room is missing its slot template or icon row — check the Inspector.");
            return;
        }

        PopulateIcons(room, RoomOrder[currentRoomIndex]);
    }

    private void PopulateIcons(RoomPage room, RoomType roomType)
    {
        foreach (ProgressSlotUI old in room.spawnedSlots)
            if (old != null) Destroy(old.gameObject);
        room.spawnedSlots.Clear();

        List<PlantSpeciesData> speciesInRoom = database.GetByRoom(roomType);
        int completedCount = 0;

        for (int i = 0; i < speciesInRoom.Count; i++)
        {
            PlantSpeciesData species = speciesInRoom[i];
            bool unlocked = journalManager != null && journalManager.IsDiscovered(species);
            bool completed = journalManager != null && journalManager.IsCompleted(species);
            if (completed) completedCount++;

            ProgressSlotUI slot = Instantiate(room.slotTemplate, room.iconRow);
            slot.gameObject.SetActive(true);

            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.anchoredPosition = ManualSlotLayout.GetPosition(room.slotTemplateRect, i, columns, cellGapX, cellGapY);

            slot.Initialize(species, unlocked, completed);
            room.spawnedSlots.Add(slot);
        }

        // Same denominator as the grid above (every species assigned to this room, not just
        // discovered ones) — e.g. 1 of 5 completed in Main Hall fills the bar to 1/5, same as
        // the example in the request.
        if (room.progressBar != null)
            room.progressBar.SetValue(completedCount, speciesInRoom.Count);
    }

    private static string GetRoomDisplayName(RoomType room)
    {
        switch (room)
        {
            case RoomType.Main: return "The Main Hall";
            case RoomType.East: return "The East Room";
            case RoomType.West: return "The West Room";
            default: return room.ToString();
        }
    }
}
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =============================================================
// ProgressPageUIController.cs
// -------------------------------------------------------------
// Drives the journal's Progress page — three rooms (Main Hall /
// East / West), each its own page with FOUR rows of species icons:
// Tier 1, Tier 2, Tier 3, and that room's Crystal (the room's
// isCrystal species — not really a "tier" the way 1-3 are,
// more the top of the pyramid above them). Each row is generated
// the same way the Plants page grid is (see JournalUIController's
// class comment / ManualSlotLayout): one hidden template slot per
// room, cloned and positioned per species at runtime, just now
// routed into one of four row containers by PlantSpeciesData.tier /
// isCrystal instead of all going into one flat grid.
//
// Unlike the Plants page grid, every row shows EVERY species
// assigned to it, discovered or not — undiscovered ones render with
// the disabled icon instead of being left out (see
// PlantJournalDatabase.GetByRoom).
//
// ROOM ORDER: Main -> East -> West. Main has no Prev, West has no
// Next — RoomOrder below is the single source of truth for that
// sequence, and its indices line up with the Rooms array (mainRoom/
// eastRoom/westRoom in that order).
//
// SETUP (per room):
//   1. Build mainRoom/eastRoom/westRoom as sibling page GameObjects,
//      each with whatever static decoration that room needs (lorem
//      text, background art, etc.) plus FOUR icon rows — tier1Row,
//      tier2Row, tier3Row, crystalRow.
//   2. Place ONE real ProgressSlotUI slot somewhere in the room (it
//      doesn't matter which row) — that's the shared template,
//      cloned into whichever row each species actually belongs in.
//      Assign it to that room's slotTemplate below.
//   3. Assign database; journalManager auto-finds
//      PlantJournalManager.Instance if left empty.
//
// COMPLETION BAR: each room can optionally have its own ImageFillBar
// (RoomPage.progressBar) showing (species completed in this room) /
// (total species assigned to this room) across ALL FOUR rows — same
// denominator as before, tiers/crystal just changed how the icons
// are laid out, not what counts toward the bar. "Completed" here
// means the gold milestone (see PlantJournalManager.IsCompleted /
// PlantProgress.ReportGoldMilestone), which is stricter than
// "discovered" — a species can be discovered (icon = Active) without
// being completed (icon = Gold) yet.
// =============================================================
public class ProgressPageUIController : MonoBehaviour
{
    [Serializable]
    private class RoomPage
    {
        [Tooltip("This room's page GameObject. Everything else on the room (lorem text, background " +
                 "art, etc.) lives under here and is left alone — only the icon rows below get touched.")]
        public GameObject page;

        [Tooltip("Row container for this room's Tier 1 species (PlantSpeciesData.tier <= 1) — unlocks " +
                 "everything on pickup, see the journal's gradual-unlock rules.")]
        public RectTransform tier1Row;

        [Tooltip("Row container for this room's Tier 2 species.")]
        public RectTransform tier2Row;

        [Tooltip("Row container for this room's Tier 3 species — the top tier below the crystal.")]
        public RectTransform tier3Row;

        [Tooltip("Row container for this room's Crystal species (PlantSpeciesData.isCrystal " +
                 "== true) — the top of the pyramid, not really a 'tier' the way 1-3 are, so it gets " +
                 "its own row rather than being folded into tier3.")]
        public RectTransform crystalRow;

        [Tooltip("An existing slot GameObject already placed somewhere in this room, with a " +
                 "ProgressSlotUI component. Hidden at startup and cloned into whichever row a given " +
                 "species belongs in — only needs to exist ONCE per room, not once per row, since " +
                 "every row uses the same slot design.")]
        public ProgressSlotUI slotTemplate;

        [Tooltip("Optional. This room's completion bar — fills according to (species completed in this " +
                 "room, across all rows) / (total species assigned to this room, across all rows), same " +
                 "denominator as before. Leave empty if this room doesn't show one.")]
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
    [Tooltip("Columns per row before wrapping, shared by all rows in all three rooms.")]
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

    /// <summary>Repopulates the currently-shown room's rows from the database. Called on room change
    /// and whenever discovery state changes (OnJournalChanged) so a newly-found plant's icon flips
    /// from disabled to active without needing to flip pages away and back.</summary>
    private void RefreshCurrentRoom()
    {
        if (database == null) return;

        RoomPage room = Rooms[currentRoomIndex];
        if (room == null || room.slotTemplate == null)
        {
            Debug.LogWarning("[ProgressPageUIController] Current room is missing its slot template — check the Inspector.");
            return;
        }

        PopulateIcons(room, RoomOrder[currentRoomIndex]);
    }

    /// <summary>Which row a species belongs in — Crystal (isCrystal) takes priority over
    /// tier, since a crystal's tier value (if any is even set) doesn't matter once it's flagged as
    /// the room's crystal. Tier 1 and below all land in the Tier 1 row (so a species left at the
    /// default tier=1, or anything unusually set to 0 or negative, still shows up rather than being
    /// silently dropped); tier 2 exactly goes to Tier 2; anything 3 and up goes to Tier 3.</summary>
    private static RectTransform GetTargetRow(RoomPage room, PlantSpeciesData species)
    {
        if (species.isCrystal) return room.crystalRow;
        if (species.tier <= 1) return room.tier1Row;
        if (species.tier == 2) return room.tier2Row;
        return room.tier3Row;
    }

    private void PopulateIcons(RoomPage room, RoomType roomType)
    {
        foreach (ProgressSlotUI old in room.spawnedSlots)
            if (old != null) Destroy(old.gameObject);
        room.spawnedSlots.Clear();

        List<PlantSpeciesData> speciesInRoom = database.GetByRoom(roomType);
        int completedCount = 0;

        // Each row lays itself out independently (its own left-to-right/wrap position count),
        // hence a separate running index per row rather than one shared index across all of them.
        int tier1Index = 0, tier2Index = 0, tier3Index = 0, crystalIndex = 0;

        foreach (PlantSpeciesData species in speciesInRoom)
        {
            bool unlocked = journalManager != null && journalManager.IsDiscovered(species);
            bool completed = journalManager != null && journalManager.IsCompleted(species);
            if (completed) completedCount++;

            RectTransform targetRow = GetTargetRow(room, species);
            if (targetRow == null)
            {
                // That row isn't wired up in the Editor for this room — skip rather than error, so
                // a room that (say) has no Tier 3 species yet doesn't need a Tier 3 row assigned.
                // Logged rather than silent, though — an unassigned row otherwise looks EXACTLY
                // like "not all the plants are showing" with no way to tell why from the UI alone.
                string which = species.isCrystal ? "crystalRow"
                    : species.tier <= 1 ? "tier1Row"
                    : species.tier == 2 ? "tier2Row"
                    : "tier3Row";
                Debug.LogWarning($"[ProgressPageUIController] '{species.displayName}' wants " +
                                  $"{which} in room '{roomType}', but that row isn't assigned on this " +
                                  "room's RoomPage in the Inspector — it won't appear at all.", this);
                continue;
            }

            int slotIndex = species.isCrystal ? crystalIndex++
                : species.tier <= 1 ? tier1Index++
                : species.tier == 2 ? tier2Index++
                : tier3Index++;

            ProgressSlotUI slot = Instantiate(room.slotTemplate, targetRow);
            slot.gameObject.SetActive(true);

            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.anchoredPosition = ManualSlotLayout.GetPosition(room.slotTemplateRect, slotIndex, columns, cellGapX, cellGapY);

            slot.Initialize(species, unlocked, completed);
            room.spawnedSlots.Add(slot);
        }

        // Same denominator as before (every species assigned to this room, not just discovered
        // ones, across all four rows) — e.g. 1 of 5 completed in Main Hall fills the bar to 1/5.
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
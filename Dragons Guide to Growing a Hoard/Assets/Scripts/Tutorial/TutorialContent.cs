using System.Collections.Generic;
using UnityEngine;

// =============================================================
// TutorialContent.cs
// -------------------------------------------------------------
// STOPGAP for the time crunch: the entire tutorial script hardcoded
// as one ordered List<TutorialStep>, instead of typing every row into
// the Inspector by hand. Covers movement, Plant Basics, Water Plant,
// the Journal tour (Progress/Plants/Guide bookmarks), and the Miasma
// greenhouse intro — message text, step type (Portable/BottomBar),
// and advance behavior are all filled in already.
//
// WHAT'S LEFT FOR YOU:
//   Every Portable step's `target` field is left null here — that's
//   the one thing that can't be hardcoded, since it has to point at
//   an actual object/UI element placed in your scene. After loading
//   this (see below), open TutorialSequenceController's `steps` list
//   in the Inspector and drag a Transform into `target` for each
//   Portable entry. The comment above each entry in BuildDefaultSteps()
//   says what to point it at. BottomBar entries need nothing.
//
// HOW TO LOAD IT:
//   Add TutorialSequenceController to your tutorial object, then
//   right-click the component header in the Inspector (or the gear
//   icon) and choose "Load Hardcoded Tutorial Script (Plant Basics +
//   Water Plant)" — that's a [ContextMenu] method on
//   TutorialSequenceController that just calls BuildDefaultSteps()
//   below and drops the result into `steps`. You can still hand-edit
//   anything afterward; this only sets the initial list.
//
// This intentionally does NOT set linkedMission/linkedTaskId — wire
// those up later once TutorialMissionAssetGenerator's assets exist
// and you're ready for the "proper" auto-advance-off-mission-
// progress setup. Every step below advances on click for now.
// =============================================================
public static class TutorialContent
{
    public static List<TutorialStep> BuildDefaultSteps()
    {
        return new List<TutorialStep>
        {
            // ---------------------------------------------------
            // INSTRUCTION — movement basics, bottom bar, no targets needed
            // ---------------------------------------------------
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Use 'WASD' and your mouse to move around" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Press the spacebar to jump" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Press double space to fly" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Tilt the mouse up to fly up" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Tilt the mouse down to fly down" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Once you hit the ground, you'll land" },

            // ---------------------------------------------------
            // PLANT BASICS
            // ---------------------------------------------------
            // target: a dead plant instance in the world
            new TutorialStep { type = TutorialPromptType.Portable, message = "Find a dead plant", targetIsWorldSpace = true },
            // target: same dead plant
            new TutorialStep { type = TutorialPromptType.Portable, message = "Press [E] to pick up the plant", targetIsWorldSpace = true },
            // target: the inventory icon (UI)
            new TutorialStep { type = TutorialPromptType.Portable, message = "Press [I] to open your inventory", targetIsWorldSpace = false },
            // target: the "Available" section inside the inventory panel (UI)
            new TutorialStep { type = TutorialPromptType.Portable, message = "Long-click on the plant and drag the Pollen Puff to the Available section on the left. This makes it available for planting.", targetIsWorldSpace = false },
            // target: the filter bar inside the inventory panel (UI)
            new TutorialStep { type = TutorialPromptType.Portable, message = "If you're unable to find a plant, use this filter system: All, Water Plant, Sun Plant, Dead Plant, Dark Plant.", targetIsWorldSpace = false },
            // target: the inventory's back button (UI)
            new TutorialStep { type = TutorialPromptType.Portable, message = "Click the back button to return to the game", targetIsWorldSpace = false },
            // target: the water source in the world
            new TutorialStep { type = TutorialPromptType.Portable, message = "Go to the water source nearby", targetIsWorldSpace = true },

            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Sit in the water until your water bar is filled" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Press [F] to enter 'placing' mode" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Press [Q] to water your plants" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Using the scroll wheel, find the smallest pot" },

            // target: a sunny grid square where the pot should be placed
            new TutorialStep { type = TutorialPromptType.Portable, message = "Left-click on a sunny square of the grid to place a pot", targetIsWorldSpace = true },
            // target: the pot that was just placed
            new TutorialStep { type = TutorialPromptType.Portable, message = "Press [E] to interact with the pot", targetIsWorldSpace = true },
            // target: the soil-type selection UI
            new TutorialStep { type = TutorialPromptType.Portable, message = "Select loam as the soil type", targetIsWorldSpace = false },
            // target: the Pollen Puff slot/option in the planting UI
            new TutorialStep { type = TutorialPromptType.Portable, message = "Select the Pollen Puff to plant the plant", targetIsWorldSpace = false },

            // ---------------------------------------------------
            // WATER PLANT — wrap-up info, bottom bar, no targets needed
            // ---------------------------------------------------
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "The top bar shows the plant's happiness. This lets you know whether the plant is happy and thriving." },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "In order to get this plant happy, you will need to do a few things: the correct soil or water it needs to grow in, the correct amount of water it receives, and the correct sunlight levels." },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "The bottom bar within the plant menu displays plant progression" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "If a plant is happy for a prolonged period, you'll be rewarded with the total completion of the plant" },

            // ---------------------------------------------------
            // JOURNAL TOUR
            // ---------------------------------------------------
            // target: the journal icon (UI) — the book icon in the top-right corner
            new TutorialStep { type = TutorialPromptType.Portable, message = "Press [J] to open your journal", targetIsWorldSpace = false },
            // target: the "Progress" bookmark tab (UI)
            new TutorialStep { type = TutorialPromptType.Portable, message = "Open the \"Progress\" bookmark", targetIsWorldSpace = false },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Here you will find the progress of each room you have completed" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "There are three types of rooms for you to view: The Main Hall, The East Wing, and The West Wing" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "By exploring these rooms, you can find more plants, which can be viewed here" },
            // target: the "Plants" bookmark tab (UI)
            new TutorialStep { type = TutorialPromptType.Portable, message = "Open the \"Plants\" bookmark", targetIsWorldSpace = false },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Here you can find your personal collection of plants you have collected" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Here you can find the tier of the plant, ranging from tier one all the way to tier three" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Here is the difficulty of the plant. This tells you how needy it is — the needier it is, the harder the plant is to take care of." },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Using your observational skills, select the correct water level, sunlight level, and soil type to know how to take care of the plant" },
            // target: the "Guide" bookmark tab (UI)
            new TutorialStep { type = TutorialPromptType.Portable, message = "Open the \"Guide\" bookmark", targetIsWorldSpace = false },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Here you can find the tutorial again to review if you so need to" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "On the first page is the guide's title, and on the second are the details of the guide" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Press your esc button to exit the game when you're ready to leave" },
            // target: the journal's back button (UI)
            new TutorialStep { type = TutorialPromptType.Portable, message = "Press the back button and return to the game", targetIsWorldSpace = false },

            // ---------------------------------------------------
            // MIASMA — greenhouse intro
            // ---------------------------------------------------
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Welcome to the greenhouse" },
            // target: the yellow happiness bar (UI, top-left)
            new TutorialStep { type = TutorialPromptType.Portable, message = "The yellow bar is your happiness level — it shows the overall plant happiness/health of the given room", targetIsWorldSpace = false },
            // target: the purple Miasma bar (UI, top-left)
            new TutorialStep { type = TutorialPromptType.Portable, message = "The purple bar on the top left tracks the greenhouse's Miasma", targetIsWorldSpace = false },
            // target: the pink fog inside the greenhouse (world)
            new TutorialStep { type = TutorialPromptType.Portable, message = "The pink fog within the greenhouse is an indicator of the Miasma within the greenhouse", targetIsWorldSpace = true },
            // target: the tree at the center of the greenhouse (world)
            new TutorialStep { type = TutorialPromptType.Portable, message = "Miasma is produced by the tree found in the center of the greenhouse", targetIsWorldSpace = true },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Miasma is detrimental to plant health and happiness" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "To decrease Miasma, you will need to find and take good care of the plants within the greenhouse" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "You don't want to know what happens when you don't" },
        };
    }
}

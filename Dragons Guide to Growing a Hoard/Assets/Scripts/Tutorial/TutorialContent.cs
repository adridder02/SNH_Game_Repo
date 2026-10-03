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
//   Every Portable step's `portablePrompt` field is left null here —
//   that's the one thing that can't be hardcoded, since each one is
//   a fully hand-built prompt object (outline + bubble + text — see
//   TutorialPromptBox.cs) that only exists once you've built it in
//   your scene. After loading this (see below), open
//   TutorialSequenceController's `steps` list in the Inspector and
//   drag a TutorialPromptBox into `portablePrompt` for each Portable
//   entry. The comment above each entry in BuildDefaultSteps() says
//   what it's for. BottomBar entries need nothing.
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
    // Must match the TaskXxx constants in PlayerController.cs exactly — those are what actually
    // get passed to MissionProgressManager.CompleteTask() when the player does each thing.
    private const string TaskMoveWasd = "move_wasd";
    private const string TaskJumpSpace = "jump_space";
    private const string TaskFlyDoubleSpace = "fly_double_space";
    private const string TaskFlyUpTilt = "fly_up_tilt";
    private const string TaskFlyDownTilt = "fly_down_tilt";
    private const string TaskLandGround = "land_ground";

    // Must match the task id strings used in CollectablePlant.cs / HarvestNodeContainer.cs /
    // PlacementSystem.cs / PotInteraction.cs / PlayerWaterSource.cs exactly, AND must be in the
    // same order as harvestMission.tasks — CompleteOrderedTask only lets a task complete when it's
    // next in that list, so if this mission's task order doesn't match, a linked step below will
    // wait forever for a completion that's being silently rejected out of order.
    // find_node/plant_pickup are kept here for reference even though nothing below links to them —
    // see the note above stepPlacePot for why there's no Portable step for either anymore.
    private const string TaskFindNode = "find_node";
    private const string TaskPlantPickup = "plant_pickup";
    private const string TaskPlacePot = "place_pot";
    private const string TaskWaterPlant = "water_plant";
    private const string TaskFindWater = "find_water";
    private const string TaskWaterRefill = "water_refill";

    /// <param name="movementMission">Optional. If assigned, the first six steps (the movement-basics
    /// bottom-bar tips) are auto-linked to this mission's tasks, so they advance the instant
    /// PlayerController reports the matching action instead of waiting for a click. Pass null to leave
    /// all steps click-only, same as before.</param>
    /// <param name="harvestMission">Optional. If assigned, the five remaining linkable entries (the
    /// plant_pickup gate, then place pot -> water plant -> find water -> refill) are auto-linked the
    /// same way. find_node has no step at all — it's only ever surfaced via the objective banner. This
    /// mission's tasks list MUST be ordered find_node, plant_pickup, place_pot, water_plant, find_water,
    /// water_refill for the auto-advance to actually fire (see CompleteOrderedTask everywhere it's
    /// called). The inventory-tour and soil/pot-interaction steps in between stay click-only — they're
    /// UI walkthrough, not tracked gameplay actions.</param>
    public static List<TutorialStep> BuildDefaultSteps(MissionData movementMission = null, MissionData harvestMission = null)
    {
        // Captured so we can link them below without hunting for them by index after the fact —
        // reordering/editing anything else in the list won't silently break the linking.
        // find_node and plant_pickup are no longer separate steps here — the objective banner
        // (TutorialObjectiveUI) covers "find a node" / "press E to pick up" on its own, so there's no
        // Portable prompt for either. The sequence below picks up right after the movement mission ends.
        // Holds the sequence here (shows nothing) until plant_pickup completes, so stepOpenInventory's
        // bubble doesn't pop up the instant the movement mission ends — it waits for the player to
        // actually find the node and pick the plant up first (handled entirely by the objective banner).
        var stepWaitForPickup = new TutorialStep { type = TutorialPromptType.Gate };
        var stepOpenInventory = new TutorialStep { type = TutorialPromptType.Portable, message = "Press [I] to open your inventory", portablePrompt = null, externalTriggerId = "inventory_opened", advanceOnClick = false };
        var stepPlacePot = new TutorialStep { type = TutorialPromptType.Portable, message = "Left-click on a sunny square of the grid to place a pot", portablePrompt = null };
        var stepSelectPollenPuff = new TutorialStep { type = TutorialPromptType.Portable, message = "Select the Pollen Puff to plant the plant", portablePrompt = null };
        var stepGoToWater = new TutorialStep { type = TutorialPromptType.Portable, message = "Go to the water source nearby", portablePrompt = null };
        var stepSitInWater = new TutorialStep { type = TutorialPromptType.BottomBar, message = "Sit in the water until your water bar is filled" };

        List<TutorialStep> steps = new List<TutorialStep>
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
            // Nothing shown here — waits for plant_pickup (see stepWaitForPickup above).
            stepWaitForPickup,
            // target: the inventory icon (UI)
            stepOpenInventory,
            // target: the "Available" section inside the inventory panel (UI)
            new TutorialStep { type = TutorialPromptType.Portable, message = "Long-click on the plant and drag the Pollen Puff to the Available section on the left. This makes it available for planting.", portablePrompt = null },
            // target: the filter bar inside the inventory panel (UI)
            new TutorialStep { type = TutorialPromptType.Portable, message = "If you're unable to find a plant, use this filter system: All, Water Plant, Sun Plant, Dead Plant, Dark Plant.", portablePrompt = null },
            // target: the inventory's back button (UI)
            new TutorialStep { type = TutorialPromptType.Portable, message = "Click the back button to return to the game", portablePrompt = null },

            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Press [F] to enter 'placing' mode" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Press [Q] to water your plants" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Using the scroll wheel, find the smallest pot" },

            // target: a sunny grid square where the pot should be placed
            stepPlacePot,
            // target: the pot that was just placed
            new TutorialStep { type = TutorialPromptType.Portable, message = "Press [E] to interact with the pot", portablePrompt = null },
            // target: the soil-type selection UI
            new TutorialStep { type = TutorialPromptType.Portable, message = "Select loam as the soil type", portablePrompt = null },
            // target: the Pollen Puff slot/option in the planting UI. Note: this step's bubble stays up
            // until water_plant completes, not just until Pollen Puff is clicked — there's no dedicated
            // step for the actual [Q] water press, so this one covers both "select it" and "water it".
            stepSelectPollenPuff,
            // target: the water source in the world
            stepGoToWater,
            stepSitInWater,

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
            new TutorialStep { type = TutorialPromptType.Portable, message = "Press [J] to open your journal", portablePrompt = null },
            // target: the "Progress" bookmark tab (UI)
            new TutorialStep { type = TutorialPromptType.Portable, message = "Open the \"Progress\" bookmark", portablePrompt = null },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Here you will find the progress of each room you have completed" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "There are three types of rooms for you to view: The Main Hall, The East Wing, and The West Wing" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "By exploring these rooms, you can find more plants, which can be viewed here" },
            // target: the "Plants" bookmark tab (UI)
            new TutorialStep { type = TutorialPromptType.Portable, message = "Open the \"Plants\" bookmark", portablePrompt = null },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Here you can find your personal collection of plants you have collected" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Here you can find the tier of the plant, ranging from tier one all the way to tier three" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Here is the difficulty of the plant. This tells you how needy it is — the needier it is, the harder the plant is to take care of." },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Using your observational skills, select the correct water level, sunlight level, and soil type to know how to take care of the plant" },
            // target: the "Guide" bookmark tab (UI)
            new TutorialStep { type = TutorialPromptType.Portable, message = "Open the \"Guide\" bookmark", portablePrompt = null },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Here you can find the tutorial again to review if you so need to" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "On the first page is the guide's title, and on the second are the details of the guide" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Press your esc button to exit the game when you're ready to leave" },
            // target: the journal's back button (UI)
            new TutorialStep { type = TutorialPromptType.Portable, message = "Press the back button and return to the game", portablePrompt = null },

            // ---------------------------------------------------
            // MIASMA — greenhouse intro
            // ---------------------------------------------------
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Welcome to the greenhouse" },
            // target: the yellow happiness bar (UI, top-left)
            new TutorialStep { type = TutorialPromptType.Portable, message = "The yellow bar is your happiness level — it shows the overall plant happiness/health of the given room", portablePrompt = null },
            // target: the purple Miasma bar (UI, top-left)
            new TutorialStep { type = TutorialPromptType.Portable, message = "The purple bar on the top left tracks the greenhouse's Miasma", portablePrompt = null },
            // target: the pink fog inside the greenhouse (world)
            new TutorialStep { type = TutorialPromptType.Portable, message = "The pink fog within the greenhouse is an indicator of the Miasma within the greenhouse", portablePrompt = null },
            // target: the tree at the center of the greenhouse (world)
            new TutorialStep { type = TutorialPromptType.Portable, message = "Miasma is produced by the tree found in the center of the greenhouse", portablePrompt = null },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "Miasma is detrimental to plant health and happiness" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "To decrease Miasma, you will need to find and take good care of the plants within the greenhouse" },
            new TutorialStep { type = TutorialPromptType.BottomBar, message = "You don't want to know what happens when you don't" },
        };

        if (movementMission != null)
        {
            string[] movementTaskIds =
            {
                TaskMoveWasd, TaskJumpSpace, TaskFlyDoubleSpace,
                TaskFlyUpTilt, TaskFlyDownTilt, TaskLandGround
            };

            // The six movement-tip BottomBar steps are the first six entries above, in the same order
            // as movementTaskIds. If you reorder/add steps above this list, update this loop's range too.
            for (int i = 0; i < movementTaskIds.Length && i < steps.Count; i++)
            {
                steps[i].linkedMission = movementMission;
                steps[i].linkedTaskId = movementTaskIds[i];
                steps[i].advanceOnClick = false; // now auto-advances off the real action instead
            }
        }

        if (harvestMission != null)
        {
            // find_node still gets completed by gameplay (HarvestNodeContainer) — it's just not tied to
            // a Portable step or a gate here, so nothing to link for it specifically.
            (TutorialStep step, string taskId)[] harvestLinks =
            {
                (stepWaitForPickup, TaskPlantPickup),
                (stepPlacePot, TaskPlacePot),
                (stepSelectPollenPuff, TaskWaterPlant),
                (stepGoToWater, TaskFindWater),
                (stepSitInWater, TaskWaterRefill),
            };

            foreach (var (step, taskId) in harvestLinks)
            {
                step.linkedMission = harvestMission;
                step.linkedTaskId = taskId;
                step.advanceOnClick = false; // now auto-advances off the real action instead
            }
        }

        return steps;
    }
}
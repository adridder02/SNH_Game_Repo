using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

// =============================================================
// SaveWorld.cs
// -------------------------------------------------------------
// Reads the live scene into a SaveFile (Capture) and puts a SaveFile back into a freshly
// loaded scene (Restore). SaveManager owns timing/files; this owns "what is the world".
//
// SCENE-AUTHORED objects (static pots, harvest nodes, crystals) are matched by hierarchy PATH
// (scene + parent chain + name), so renaming/re-parenting one of those in the editor orphans
// its old save entry (it just loads as the scene's default). Player-placed things (pots,
// ability placeables, wall mushrooms) are re-created from their asset NAMES via SaveRegistry.
//
// NOT SAVED (honest list): miasma spread, per-placeable internal timers/charges, in-flight
// effects/cooldowns, scene pots the player picked up or moved, plant "Display Info" stashes
// (icons come from the species data instead), pot algae, camera angle.
// =============================================================
public static class SaveWorld
{
    // ------------------------------------------------------------
    // PATH IDS
    // ------------------------------------------------------------
    public static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (Transform c = t; c != null; c = c.parent)
            parts.Add(c.name + "~" + SameNameIndex(c));
        parts.Reverse();
        return t.gameObject.scene.name + "/" + string.Join("/", parts);
    }

    private static int SameNameIndex(Transform t)
    {
        int idx = 0;
        if (t.parent != null)
        {
            for (int i = 0; i < t.GetSiblingIndex(); i++)
                if (t.parent.GetChild(i).name == t.name) idx++;
        }
        else
        {
            foreach (GameObject root in t.gameObject.scene.GetRootGameObjects())
            {
                if (root.transform == t) break;
                if (root.name == t.name) idx++;
            }
        }
        return idx;
    }

    private static T[] All<T>() where T : UnityEngine.Object
        => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);

    private static bool IsRuntimeClone(GameObject go) => go.name.Contains("(Clone)");

    // ============================================================
    // CAPTURE
    // ============================================================
    public static SaveFile Capture(string reason)
    {
        var f = new SaveFile
        {
            reason = reason,
            savedAtUtc = DateTime.UtcNow.ToString("o"),
            scene = SceneManager.GetActiveScene().name,
            gameClock = GameClock.Now,
            missionJson = MissionProgressManager.Instance != null ? MissionProgressManager.Instance.CaptureJson() : "",
        };

        CapturePrefs(f);
        CapturePlayer(f);
        CapturePots(f);
        CaptureHarvested(f);
        CaptureWalls(f);
        CapturePlaceables(f);
        CaptureCrystals(f);
        CaptureHotbar(f);
        return f;
    }

    // ---- PlayerPrefs ----
    private static List<string> DiscoverTutorialKeys()
    {
        // The tutorial "completion flag" keys are typed into inspectors (private fields), so find them
        // by reflection rather than making you list them twice.
        var keys = new List<string>();
        foreach (TutorialSequenceController c in All<TutorialSequenceController>())
        {
            string k = ReadString(c, "completionFlagKey");
            if (!string.IsNullOrEmpty(k)) { keys.Add(k); keys.Add(k + "_Started"); }
        }
        foreach (MainUIController m in All<MainUIController>())
        {
            string k = ReadString(m, "tutorialCompletedPrefsKey");
            if (!string.IsNullOrEmpty(k)) keys.Add(k);
        }
        return keys;
    }

    private static string ReadString(object obj, string field)
    {
        FieldInfo fi = obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return fi != null ? fi.GetValue(obj) as string : null;
    }

    private static void CapturePrefs(SaveFile f)
    {
        SaveRegistry r = SaveRegistry.Instance;
        var added = new HashSet<string>();

        void AddInt(string k)
        {
            if (string.IsNullOrEmpty(k) || !added.Add(k) || !PlayerPrefs.HasKey(k)) return;
            f.prefs.Add(new PrefEntry { key = k, type = 0, i = PlayerPrefs.GetInt(k) });
        }
        void AddString(string k)
        {
            if (string.IsNullOrEmpty(k) || !added.Add(k) || !PlayerPrefs.HasKey(k)) return;
            f.prefs.Add(new PrefEntry { key = k, type = 1, s = PlayerPrefs.GetString(k) });
        }
        void AddFloat(string k)
        {
            if (string.IsNullOrEmpty(k) || !added.Add(k) || !PlayerPrefs.HasKey(k)) return;
            f.prefs.Add(new PrefEntry { key = k, type = 2, f = PlayerPrefs.GetFloat(k) });
        }

        foreach (string k in SaveRegistry.BuiltInStringKeys) AddString(k);
        foreach (string k in DiscoverTutorialKeys()) { AddInt(k); f.knownPrefKeys.Add(k); }
        if (r != null)
        {
            foreach (string k in r.intPrefKeys) { AddInt(k); f.knownPrefKeys.Add(k); }
            foreach (string k in r.stringPrefKeys) { AddString(k); f.knownPrefKeys.Add(k); }
            foreach (string k in r.floatPrefKeys) { AddFloat(k); f.knownPrefKeys.Add(k); }
        }
        // (Mission progress + lore live inside missionJson, not as prefs.)
    }

    // ---- Player ----
    private static void CapturePlayer(SaveFile f)
    {
        PlayerInventory inv = UnityEngine.Object.FindAnyObjectByType<PlayerInventory>();
        if (inv == null) return;

        f.player.position = inv.transform.position;
        f.player.yaw = inv.transform.eulerAngles.y;
        inv.CaptureSave(f.player);
    }

    // ---- Pots ----
    private static void CapturePots(SaveFile f)
    {
        PlacementSystem placement = UnityEngine.Object.FindAnyObjectByType<PlacementSystem>();

        foreach (PotContents pc in All<PotContents>())
        {
            if (!pc.gameObject.scene.IsValid()) continue;

            bool dynamic = IsRuntimeClone(pc.gameObject);
            var s = new PotSave
            {
                dynamic = dynamic,
                hasSoil = pc.HasSoil,
                soil = (int)pc.CurrentSoil,
                water = pc.WaterLevel,
                rotation = pc.transform.rotation,
            };

            if (dynamic)
            {
                if (pc.GridData == null || pc.potData == null || placement == null) continue;
                int surfaceIdx = SurfaceIndex(placement, pc.GridData);
                if (surfaceIdx < 0) continue;
                PlacementData pd = pc.GridData.GetPlacement(pc.GridOrigin);
                s.pot = pc.potData.name;
                s.surface = surfaceIdx;
                s.origin = pc.GridOrigin;
                s.size = pd != null ? pd.Size : pc.potData.size;
            }
            else
            {
                s.id = PathOf(pc.transform);
            }

            if (pc.HasPlant && pc.Plant != null && pc.CurrentPlantPrefab != null)
            {
                s.plant = pc.CurrentPlantPrefab.name;
                PlantCondition cond = pc.Plant.CaptureCondition();
                s.plantDead = cond.isPermanentlyDead;
                s.plantHealth01 = cond.startingHealth01;
                PlantProgress pr = pc.Plant.GetComponent<PlantProgress>();
                if (pr == null) pr = pc.Plant.GetComponentInChildren<PlantProgress>();
                if (pr != null) { s.progress01 = pr.Progress01; s.complete = pr.IsComplete; }
            }
            else s.plant = "";

            f.pots.Add(s);
        }
    }

    private static int SurfaceIndex(PlacementSystem placement, GridData gd)
    {
        IReadOnlyList<GreenhouseSurface> list = placement.Surfaces;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && placement.GetGridData(list[i]) == gd) return i;
        return -1;
    }

    // ---- Harvest nodes ----
    private static void CaptureHarvested(SaveFile f)
    {
        foreach (Transform t in HarvestNodeContainer.HarvestedSnapshot())
            f.harvestedNodes.Add(PathOf(t));
    }

    // ---- Wall mushrooms ----
    private static void CaptureWalls(SaveFile f)
    {
        WallPlacementSystem wall = UnityEngine.Object.FindAnyObjectByType<WallPlacementSystem>();
        if (wall == null) return;
        foreach (WallPlacementSystem.WallRecord r in wall.PlacedRecords)
        {
            if (r.data == null || r.surfaceIndex < 0) continue;
            f.walls.Add(new WallSave { mushroom = r.data.name, surface = r.surfaceIndex, origin = r.origin });
        }
    }

    // ---- Ability placeables ----
    private static void CapturePlaceables(SaveFile f)
    {
        PlacementSystem placement = UnityEngine.Object.FindAnyObjectByType<PlacementSystem>();
        if (placement == null) return;

        foreach (AbilityPlaceable p in All<AbilityPlaceable>())
        {
            if (p.Data == null || p.GridData == null) continue;
            int idx = SurfaceIndex(placement, p.GridData);
            if (idx < 0) continue;
            f.placeables.Add(new PlaceableSave
            {
                item = p.Data.name,
                surface = idx,
                origin = p.GridOrigin,
                size = p.GridSize,
                position = p.transform.position,
                rotation = p.transform.rotation,
            });
        }
    }

    // ---- Crystals ----
    private static void CaptureCrystals(SaveFile f)
    {
        foreach (WorldTreeCrystals c in All<WorldTreeCrystals>())
        {
            var s = new CrystalSave { id = PathOf(c.transform) };
            s.revealed.AddRange(c.CaptureRevealed());
            f.crystals.Add(s);
        }
    }

    // ---- Hotbar ----
    private static void CaptureHotbar(SaveFile f)
    {
        AbilityHotbarSystem hb = UnityEngine.Object.FindAnyObjectByType<AbilityHotbarSystem>();
        if (hb == null) return;
        for (int i = 0; i < hb.SlotCount; i++)
        {
            AbilityItemData a = hb.GetAssigned(i);
            f.hotbar.Add(a != null ? a.name : "");
        }
    }

    // ============================================================
    // RESTORE
    // ============================================================
    public static void Restore(SaveFile f)
    {
        SaveRegistry r = SaveRegistry.Instance;

        ApplyPrefs(f);
        GameClock.Now = f.gameClock;

        // Order matters: pots + harvest state first (they can raise planted/harvest events), the
        // inventory next, mission progress LAST so it overwrites anything those events completed.
        RestorePots(f, r);
        RestoreHarvested(f);
        RestoreWalls(f, r);
        RestorePlaceables(f, r);
        RestoreCrystals(f);
        RestorePlayer(f, r);
        RestoreHotbar(f, r);

        if (!string.IsNullOrEmpty(f.missionJson))
            MissionProgressManager.Instance?.RestoreJson(f.missionJson);
    }

    /// <summary>Writes the saved PlayerPrefs back. SaveManager calls this BEFORE the scene loads so the scene's own
    /// startup code (tutorial 'already completed' flags, journal) sees the right values.</summary>
    public static void ApplyPrefs(SaveFile f)
    {
        foreach (PrefEntry p in f.prefs)
        {
            switch (p.type)
            {
                case 0: PlayerPrefs.SetInt(p.key, p.i); break;
                case 1: PlayerPrefs.SetString(p.key, p.s ?? ""); break;
                default: PlayerPrefs.SetFloat(p.key, p.f); break;
            }
        }
        PlayerPrefs.Save();

        // The journal / new-item badge singletons read their prefs once at startup — tell them to re-read.
        PlantJournalManager.Instance?.ReloadFromPrefs();
        NewItemTracker.Instance?.ReloadFromPrefs();
    }

    private static void RestorePots(SaveFile f, SaveRegistry r)
    {
        PlacementSystem placement = UnityEngine.Object.FindAnyObjectByType<PlacementSystem>();

        var byPath = new Dictionary<string, PotContents>();
        foreach (PotContents pc in All<PotContents>())
            if (pc.gameObject.scene.IsValid() && !IsRuntimeClone(pc.gameObject))
                byPath[PathOf(pc.transform)] = pc;

        foreach (PotSave s in f.pots)
        {
            PotContents pc = null;
            if (s.dynamic)
            {
                PotData data = r != null ? r.FindPot(s.pot) : null;
                if (data != null && placement != null)
                    pc = placement.RestorePot(data, s.surface, s.origin, s.size, s.rotation);
                if (pc == null) Debug.LogWarning($"[SaveWorld] Couldn't re-place pot '{s.pot}' at {s.origin} (surface {s.surface}).");
            }
            else if (!byPath.TryGetValue(s.id ?? "", out pc))
            {
                Debug.LogWarning($"[SaveWorld] Saved pot '{s.id}' no longer exists in the scene — skipped.");
            }
            if (pc == null) continue;

            GameObject plantPrefab = (!string.IsNullOrEmpty(s.plant) && r != null) ? r.FindPlant(s.plant) : null;
            var cond = new PlantCondition { isPermanentlyDead = s.plantDead, startingHealth01 = s.plantHealth01 };
            pc.RestoreState(s.hasSoil, (SoilKind)s.soil, s.water, plantPrefab, cond, s.progress01, s.complete);
        }
    }

    private static void RestoreHarvested(SaveFile f)
    {
        if (f.harvestedNodes.Count == 0) return;
        var wanted = new HashSet<string>(f.harvestedNodes);

        foreach (HarvestNodeContainer c in All<HarvestNodeContainer>())
        {
            for (int i = 0; i < c.transform.childCount; i++)
            {
                Transform child = c.transform.GetChild(i);
                if (child.GetComponent<HarvestNodeTypeGroup>() != null)
                {
                    for (int j = 0; j < child.childCount; j++) TryHarvest(child.GetChild(j), wanted);
                }
                else TryHarvest(child, wanted);
            }
        }
    }

    private static void TryHarvest(Transform node, HashSet<string> wanted)
    {
        if (wanted.Contains(PathOf(node))) HarvestNodeContainer.MarkHarvestedSilently(node);
    }

    private static void RestoreWalls(SaveFile f, SaveRegistry r)
    {
        WallPlacementSystem wall = UnityEngine.Object.FindAnyObjectByType<WallPlacementSystem>();
        if (wall == null || r == null) return;
        foreach (WallSave w in f.walls)
        {
            WallMushroomData data = r.FindMushroom(w.mushroom);
            if (data != null) wall.RestoreMushroom(data, w.surface, w.origin);
        }
    }

    private static void RestorePlaceables(SaveFile f, SaveRegistry r)
    {
        AbilityPlacementSystem aps = UnityEngine.Object.FindAnyObjectByType<AbilityPlacementSystem>();
        if (aps == null || r == null) return;
        foreach (PlaceableSave p in f.placeables)
        {
            AbilityItemData data = r.FindAbility(p.item);
            if (data != null) aps.RestorePlaceable(data, p.surface, p.origin, p.size, p.position, p.rotation);
        }
    }

    private static void RestoreCrystals(SaveFile f)
    {
        var byPath = new Dictionary<string, WorldTreeCrystals>();
        foreach (WorldTreeCrystals c in All<WorldTreeCrystals>()) byPath[PathOf(c.transform)] = c;
        foreach (CrystalSave s in f.crystals)
            if (byPath.TryGetValue(s.id ?? "", out WorldTreeCrystals c)) c.RestoreRevealed(s.revealed);
    }

    private static void RestorePlayer(SaveFile f, SaveRegistry r)
    {
        PlayerInventory inv = UnityEngine.Object.FindAnyObjectByType<PlayerInventory>();
        if (inv == null) return;

        inv.RestoreSave(f.player, r);

        PlayerController pc = inv.GetComponent<PlayerController>();
        if (pc == null) pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (pc != null) pc.TeleportTo(f.player.position, Quaternion.Euler(0f, f.player.yaw, 0f));
        else inv.transform.SetPositionAndRotation(f.player.position, Quaternion.Euler(0f, f.player.yaw, 0f));
    }

    private static void RestoreHotbar(SaveFile f, SaveRegistry r)
    {
        AbilityHotbarSystem hb = UnityEngine.Object.FindAnyObjectByType<AbilityHotbarSystem>();
        if (hb == null || r == null) return;
        for (int i = 0; i < f.hotbar.Count && i < hb.SlotCount; i++)
            hb.SetSlotSilently(i, r.FindAbility(f.hotbar[i]));
    }
}

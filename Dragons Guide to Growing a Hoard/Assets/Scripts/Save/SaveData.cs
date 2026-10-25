using System;
using System.Collections.Generic;
using UnityEngine;

// =============================================================
// SaveData.cs
// -------------------------------------------------------------
// Plain serialisable data for the single save slot (JsonUtility-friendly:
// no dictionaries, no nullables, no object references — everything that
// points at an asset is stored as that asset's NAME and looked up again
// through SaveRegistry on load).
// =============================================================
[Serializable]
public class SaveFile
{
    public int version = 1;
    public string savedAtUtc;
    public string reason;          // "manual", "autosave:journal", ...
    public string scene;           // scene the save was taken in (the main scene)
    public float gameClock;

    public string missionJson;     // MissionProgressManager.CaptureJson()
    public List<PrefEntry> prefs = new List<PrefEntry>();
    public List<string> knownPrefKeys = new List<string>(); // keys found on tutorial controllers etc. (so New Game can wipe them from the title screen)

    public PlayerSave player = new PlayerSave();
    public List<PotSave> pots = new List<PotSave>();
    public List<string> harvestedNodes = new List<string>();
    public List<WallSave> walls = new List<WallSave>();
    public List<PlaceableSave> placeables = new List<PlaceableSave>();
    public List<CrystalSave> crystals = new List<CrystalSave>();
    public List<string> hotbar = new List<string>();   // asset name per slot, "" = empty
}

[Serializable]
public class PrefEntry
{
    public string key;
    public int type;       // 0 = int, 1 = string, 2 = float
    public int i;
    public string s;
    public float f;
}

[Serializable]
public class PlayerSave
{
    public Vector3 position;
    public float yaw;
    public float water;
    public int gridW, gridH;
    public bool hasUnlockedWallPlacement;
    public bool hasHarvestedFirstPlant;
    public bool hasHarvestedFirstAbilityItem;
    public bool hasHarvestedFirstHotbarItem;
    public List<PlantItemSave> plants = new List<PlantItemSave>();
    public List<AbilityStackSave> abilities = new List<AbilityStackSave>();
}

[Serializable]
public class PlantItemSave
{
    public string prefab;
    public bool dead;
    public float health01;
    public bool setGrowth;
    public float growth01;
    public int gx = -1, gy = -1;
}

[Serializable]
public class AbilityStackSave
{
    public string item;
    public int count;
    public int gx = -1, gy = -1;
}

[Serializable]
public class PotSave
{
    public string id;              // hierarchy path (scene-authored pots)
    public bool dynamic;           // true = placed by the player at runtime
    public string pot;             // PotData asset name (dynamic only)
    public int surface;            // index in PlacementSystem.Surfaces (dynamic only)
    public Vector3Int origin;
    public Vector2Int size;
    public Quaternion rotation = Quaternion.identity;

    public bool hasSoil;
    public int soil;
    public float water;

    public string plant;           // plant prefab name, "" = empty
    public bool plantDead;
    public float plantHealth01;
    public float progress01;
    public bool complete;
}

[Serializable]
public class WallSave
{
    public string mushroom;        // WallMushroomData asset name
    public int surface;            // index in WallPlacementSystem's wall surfaces
    public Vector3Int origin;
}

[Serializable]
public class PlaceableSave
{
    public string item;            // AbilityItemData asset name
    public int surface;            // index in PlacementSystem.Surfaces
    public Vector3Int origin;
    public Vector2Int size;
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;
}

[Serializable]
public class CrystalSave
{
    public string id;
    public List<bool> revealed = new List<bool>();
}

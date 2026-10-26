using System;
using System.Collections.Generic;
using UnityEngine;

// =============================================================
// SoundBank.cs
// -------------------------------------------------------------
// One asset that holds every game sound. Lives at  Assets/Resources/SoundBank.asset  (create it with the
// menu  Neglect > Audio > Create Sound Bank  - it also offers to auto-fill the clips from a folder by
// matching the Pixabay file names).  Scripts ask for sounds by SoundId, so swapping a sound later is just
// dragging a different clip into this asset - no code or scene changes.
// =============================================================
public enum SoundId
{
    // UI
    SwitchTab, MenuOpenClose, ClickItem,
    // Water
    Swimming, JumpIntoWater, WaterbellActive,
    // Music / ambience
    TreeMusic1, TreeMusic2, SparkmintAmbient, Windmill, TutorialWind,
    // Gameplay
    TaskComplete, NewThingUnlocked, OpenJournal, OpenBookmarks, FlipPage, UseBubble, CircuitActivate
}

public enum SurfaceType { Default, Wood, Wet, Stone, Carpet, Leaves }

public enum AudioCategory { Sfx, Music, Ambience }

[CreateAssetMenu(menuName = "Neglect/Sound Bank", fileName = "SoundBank")]
public class SoundBank : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public SoundId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Which mixer slider controls this sound.")]
        public AudioCategory category = AudioCategory.Sfx;
        [Tooltip("Used by the auto-fill tool: parts of the file name (lower-case, dashes) that identify this sound.")]
        public string[] filenameHints;
    }

    [Serializable]
    public class Step
    {
        public SurfaceType surface;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        public string[] filenameHints;
    }

    public List<Entry> sounds = new List<Entry>();
    public List<Step> footsteps = new List<Step>();

    [Tooltip("Footstep clips are walking loops. While running they are played at this pitch/speed.")]
    public float runPitch = 1.35f;

    private Dictionary<SoundId, Entry> soundLookup;
    private Dictionary<SurfaceType, Step> stepLookup;

    public Entry Get(SoundId id)
    {
        if (soundLookup == null)
        {
            soundLookup = new Dictionary<SoundId, Entry>();
            foreach (Entry e in sounds) if (e != null) soundLookup[e.id] = e;
        }
        soundLookup.TryGetValue(id, out Entry found);
        return found;
    }

    /// <summary>Finds the clip for a surface; falls back to the Default surface if that one is empty.</summary>
    public bool TryGetStep(SurfaceType surface, out AudioClip clip, out float volume)
    {
        if (stepLookup == null)
        {
            stepLookup = new Dictionary<SurfaceType, Step>();
            foreach (Step s in footsteps) if (s != null) stepLookup[s.surface] = s;
        }

        if (stepLookup.TryGetValue(surface, out Step st) && st.clip != null) { clip = st.clip; volume = st.volume; return true; }
        if (stepLookup.TryGetValue(SurfaceType.Default, out st) && st.clip != null) { clip = st.clip; volume = st.volume; return true; }
        clip = null; volume = 1f; return false;
    }

    void OnValidate()
    {
        soundLookup = null;
        stepLookup = null;
    }

    /// <summary>Adds a row for every SoundId / SurfaceType that doesn't have one yet (keeps existing clips).</summary>
    public void EnsureDefaults()
    {
        void Add(SoundId id, AudioCategory cat, params string[] hints)
        {
            if (sounds.Exists(e => e != null && e.id == id)) return;
            sounds.Add(new Entry { id = id, category = cat, filenameHints = hints });
        }

        Add(SoundId.SwitchTab, AudioCategory.Sfx, "shutter-click");
        Add(SoundId.MenuOpenClose, AudioCategory.Sfx, "click-sound");
        Add(SoundId.ClickItem, AudioCategory.Sfx, "click-sfx");
        Add(SoundId.Swimming, AudioCategory.Sfx, "swimming-sounds");
        Add(SoundId.JumpIntoWater, AudioCategory.Sfx, "jumping-into");
        Add(SoundId.WaterbellActive, AudioCategory.Sfx, "running-water");
        Add(SoundId.TreeMusic1, AudioCategory.Music);                       // your credits list this as the same file as the splash - assign by hand
        Add(SoundId.TreeMusic2, AudioCategory.Music, "horror-ambience");
        Add(SoundId.SparkmintAmbient, AudioCategory.Ambience, "static-electricity");
        Add(SoundId.Windmill, AudioCategory.Ambience, "fan-blowing");
        Add(SoundId.TutorialWind, AudioCategory.Ambience, "wind-blowing");
        Add(SoundId.TaskComplete, AudioCategory.Sfx, "snd-fragment", "retrieve");
        Add(SoundId.NewThingUnlocked, AudioCategory.Sfx, "news-ting");
        Add(SoundId.OpenJournal, AudioCategory.Sfx, "book-opening");
        Add(SoundId.OpenBookmarks, AudioCategory.Sfx, "page-turn");
        Add(SoundId.FlipPage, AudioCategory.Sfx, "turning-page");
        Add(SoundId.UseBubble, AudioCategory.Sfx, "bubble-pop");
        Add(SoundId.CircuitActivate, AudioCategory.Sfx, "high-voltage", "spark");

        void AddStep(SurfaceType s, params string[] hints)
        {
            if (footsteps.Exists(e => e != null && e.surface == s)) return;
            footsteps.Add(new Step { surface = s, filenameHints = hints });
        }
        AddStep(SurfaceType.Default, "people-walking");
        AddStep(SurfaceType.Wood, "wooden-floor");
        AddStep(SurfaceType.Wet, "stone-beach");
        AddStep(SurfaceType.Stone, "stone-path");
        AddStep(SurfaceType.Carpet, "stone-path");   // your credits use the same file for carpet
        AddStep(SurfaceType.Leaves, "walking-on-leaves");
        OnValidate();
    }

    void Reset() { EnsureDefaults(); }
}

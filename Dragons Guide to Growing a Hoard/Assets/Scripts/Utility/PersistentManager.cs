using System.Collections.Generic;
using UnityEngine;

// =============================================================
// PersistentManager.cs
// -------------------------------------------------------------
// Shared "scene-separated singleton" rule used by the manager scripts that live on a DontDestroyOnLoad
// object (GameInputModeManager, CursorManager, PlantJournalManager, NewItemTracker, MissionProgressManager,
// LoadingScreenController).
//
//   - A copy left over from an EARLIER frame/scene is destroyed; the copy in the scene that just loaded
//     takes over, so its inspector references stay valid.
//   - A second copy that wakes up in the SAME frame (an accidental duplicate in one scene) is destroyed
//     instead, as before.
//
// Several managers can share ONE GameObject (e.g. "GameManager"). The first of them to wake up moves the
// whole object to DontDestroyOnLoad - so we remember WHICH frame each object was claimed in, rather than
// comparing scenes (a sibling may already have changed this object's scene).
// =============================================================
public static class PersistentManager
{
    private static readonly Dictionary<int, int> claimFrame = new Dictionary<int, int>(); // GameObject id -> frame

    /// <summary>Call from Awake. Returns false if `self` was a same-frame duplicate and has been destroyed
    /// (the caller should return). Otherwise any stale leftover is destroyed, this object is made
    /// persistent, and the caller should set its Instance to itself.</summary>
    public static bool Claim(Component self, Component existing)
    {
        GameObject mine = self.gameObject;
        int frame = Time.frameCount;

        if (existing != null && existing != self && existing.gameObject != mine)
        {
            GameObject theirs = existing.gameObject;
            bool theirsIsLeftover = claimFrame.TryGetValue(theirs.GetInstanceID(), out int theirFrame)
                ? theirFrame < frame
                : theirs.scene != mine.scene;

            if (!theirsIsLeftover)
            {
                Object.Destroy(mine); // true duplicate in this load
                return false;
            }

            Debug.Log($"[SceneSeparate] {self.GetType().Name} on '{mine.name}' replaces the leftover on '{theirs.name}' " +
                      $"(from an earlier scene) - destroying the leftover object.");
            // (keep its claim record: sibling managers on the same object still need to see it as a leftover)
            Object.Destroy(theirs);
        }

        if (!claimFrame.ContainsKey(mine.GetInstanceID()))
        {
            claimFrame[mine.GetInstanceID()] = frame;
            Object.DontDestroyOnLoad(mine);
        }
        return true;
    }
}

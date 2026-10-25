using System.Collections.Generic;
using UnityEngine;

// =============================================================
// TutorialTargets.cs
// -------------------------------------------------------------
// A tiny registry of "named UI things the tutorial may want to point at", for targets that only exist
// at runtime (a journal plant slot, an inventory consumable) so a hand-placed prompt can't be
// positioned on them ahead of time.
//
// A script that builds the UI calls TutorialTargets.Set("some_key", rectTransform);
// a TutorialPromptBox with Follow Target Key = "some_key" then snaps its outline onto that rect
// whenever it's showing. Keys currently registered by the game:
//   "journal_plant"         - the first COMPLETED plant's slot in the journal (falls back to the first discovered)
//   "inventory_consumable"  - the first consumable's slot in the inventory grid (text: {Consumable Name})
//   "journal_passive_plant" - the first discovered species ticked "Has Passive Ability" (text: {Passive Plant Name})
// =============================================================
public static class TutorialTargets
{
    private static readonly Dictionary<string, RectTransform> targets = new Dictionary<string, RectTransform>();

    /// <summary>Registers a target. Existing live targets are kept unless overwrite is true, so the "first" match wins.</summary>
    public static void Set(string key, RectTransform rect, bool overwrite = false)
    {
        if (string.IsNullOrEmpty(key) || rect == null) return;
        if (!overwrite && targets.TryGetValue(key, out RectTransform cur) && cur != null) return;
        targets[key] = rect;
    }

    // Plain text values (e.g. "plant_name"), substituted into tutorial messages as {Plant Name}.
    private static readonly Dictionary<string, string> texts = new Dictionary<string, string>();
    public static void SetText(string key, string value) { if (!string.IsNullOrEmpty(key)) texts[key] = value; }
    public static string GetText(string key, string fallback = "") =>
        !string.IsNullOrEmpty(key) && texts.TryGetValue(key, out string v) && !string.IsNullOrEmpty(v) ? v : fallback;

    public static void Clear(string key)
    {
        if (!string.IsNullOrEmpty(key)) targets.Remove(key);
    }

    public static RectTransform Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        return targets.TryGetValue(key, out RectTransform r) && r != null && r.gameObject.activeInHierarchy ? r : null;
    }
}
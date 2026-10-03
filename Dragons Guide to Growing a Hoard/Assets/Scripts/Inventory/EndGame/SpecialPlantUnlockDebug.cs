using UnityEngine;

// =============================================================
// SpecialPlantUnlockDebug.cs
// -------------------------------------------------------------
// Drop this on any GameObject in the scene (a "Debug" object is fine) and
// tick the checkbox below to skip the room-completion/happiness checks in
// SpecialPlantUnlockGate entirely, so the three crystal-heart plants (or
// anything else with requiresRoomUnlock on) are plantable immediately —
// no need to 100% a room for real every time you're testing.
//
// Works live in Play mode: tick/untick the checkbox at any time, no
// restart needed. If you'd rather have an actual on-screen button instead
// of an Inspector checkbox, wire a Button's onClick to ToggleFromButton()
// below — same static flag either way.
// =============================================================
public class SpecialPlantUnlockDebug : MonoBehaviour
{
    [Tooltip("ON = every requiresRoomUnlock plant species is treated as plantable, ignoring room " +
             "completion and happiness entirely. Safe to leave this component in the scene with " +
             "this OFF for normal play — it only has an effect while ticked.")]
    [SerializeField] private bool forceUnlockAllSpecialPlants = false;

    private void OnValidate() => Apply();
    private void Awake() => Apply();
    private void OnEnable() => Apply();

    private void Apply()
    {
        SpecialPlantUnlockGate.DebugForceUnlocked = forceUnlockAllSpecialPlants;
    }

    /// <summary>Call from a UI Button's onClick if you'd rather have an actual in-game button than
    /// an Inspector checkbox — flips the same flag the checkbox above controls.</summary>
    public void ToggleFromButton()
    {
        forceUnlockAllSpecialPlants = !forceUnlockAllSpecialPlants;
        Apply();
    }
}

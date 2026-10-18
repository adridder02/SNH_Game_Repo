using UnityEngine;

// =============================================================
// GameClock.cs
// -------------------------------------------------------------
// The in-game "world clock" speed. Everything that represents the WORLD simulating itself - plant
// growth/decay, water drain, miasma growth and damage, ability timers - reads its time from here
// instead of Unity's Time, so the player can speed it up or slow it down (see GameTimeController)
// without also speeding up the player's own movement, the camera, animations or UI. Unity's
// Time.timeScale is deliberately NOT used for that reason.
//
//   GameClock.DeltaTime            replaces Time.deltaTime in simulation code
//   GameClock.Now                  replaces Time.time (a clock that only advances at game speed)
//   new WaitForGameSeconds(x)      replaces new WaitForSeconds(x) in simulation coroutines
//
// Needs no scene setup: a hidden driver object creates itself on load and advances Now each frame.
// =============================================================
public static class GameClock
{
    /// <summary>Current speed multiplier. 1 = normal.</summary>
    public static float Scale { get; private set; } = 1f;

    /// <summary>Seconds elapsed this frame, at game speed.</summary>
    public static float DeltaTime => Time.deltaTime * Scale;

    /// <summary>Total game-speed seconds since the game started (advances by DeltaTime each frame).</summary>
    public static float Now { get; internal set; }

    public static event System.Action<float> OnScaleChanged;

    public static void SetScale(float scale)
    {
        scale = Mathf.Max(0f, scale);
        if (Mathf.Approximately(scale, Scale)) return;
        Scale = scale;
        OnScaleChanged?.Invoke(Scale);
    }

    public static void ResetScale() => SetScale(1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Scale = 1f;
        Now = 0f;
        OnScaleChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateDriver()
    {
        var go = new GameObject("GameClockDriver") { hideFlags = HideFlags.HideInHierarchy };
        Object.DontDestroyOnLoad(go);
        go.AddComponent<GameClockDriver>();
    }
}

/// <summary>Advances GameClock.Now. Runs before everything else so Now is current for the whole frame.</summary>
[DefaultExecutionOrder(-10000)]
public class GameClockDriver : MonoBehaviour
{
    private void Update()
    {
        GameClock.Now += GameClock.DeltaTime;
    }
}

/// <summary>Coroutine yield that waits in GAME seconds (affected by GameClock.Scale), like WaitForSeconds
/// is for real seconds.</summary>
public class WaitForGameSeconds : CustomYieldInstruction
{
    private readonly float endTime;
    public WaitForGameSeconds(float seconds) { endTime = GameClock.Now + seconds; }
    public override bool keepWaiting => GameClock.Now < endTime;
}

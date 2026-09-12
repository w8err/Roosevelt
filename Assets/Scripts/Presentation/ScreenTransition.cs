using System;
using System.Collections;
using UnityEngine;

// Shared screen-black transition: input lock -> fade to black -> whileBlack -> hold -> fade
// back in -> unlock -> onDone. TransitionDoor uses Teleport; the dream bed/wake flow (later)
// is expected to use LoadScene. Only one transition runs at a time.
public static class ScreenTransition
{
    const float FadeOutSeconds = 0.35f;
    const float HoldSeconds = 0.15f;
    const float FadeInSeconds = 0.35f;

    static readonly object LockKey = new object();
    static Runner runner;

    public static bool IsRunning { get; private set; }

    // Play mode starts without a domain reload in this project, so a run left over from the
    // previous session (and the lock it held) must not survive into the next one.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        IsRunning = false;
        runner = null;
        InputLock.Release(LockKey);
    }

    // Locks input, fades to black, runs whileBlack while the screen is black (may be null),
    // holds briefly, fades back in, unlocks, then calls onDone. Returns false without doing
    // anything if a transition is already running.
    public static bool Run(Func<IEnumerator> whileBlack, Action onDone = null)
    {
        if (IsRunning) return false;
        IsRunning = true;
        EnsureRunner().StartCoroutine(RunRoutine(whileBlack, onDone));
        return true;
    }

    public static bool Teleport(FirstPersonController player, Transform destination)
    {
        if (player == null || destination == null) return false;
        return Run(() => TeleportRoutine(player, destination));
    }

    // Loads sceneName while the screen is black; afterLoad runs once it's in, then the screen fades back in.
    public static bool LoadScene(string sceneName, Action afterLoad = null)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        return Run(() => LoadSceneRoutine(sceneName, afterLoad));
    }

    static IEnumerator TeleportRoutine(FirstPersonController player, Transform destination)
    {
        player.Warp(destination.position, destination.rotation);
        yield return null;
    }

    static IEnumerator LoadSceneRoutine(string sceneName, Action afterLoad)
    {
        var op = SceneLoader.LoadAsync(sceneName);
        // SceneLoader already logged Unity's own error; just fade back in without afterLoad.
        if (op == null) yield break;
        yield return op;
        afterLoad?.Invoke();
    }

    static IEnumerator RunRoutine(Func<IEnumerator> whileBlack, Action onDone)
    {
        InputLock.Acquire(LockKey);
        var fader = ScreenFader.Instance;
        yield return fader.FadeTo(1f, FadeOutSeconds);

        var inner = whileBlack?.Invoke();
        if (inner != null) yield return inner;

        if (HoldSeconds > 0f) yield return new WaitForSeconds(HoldSeconds);
        yield return fader.FadeTo(0f, FadeInSeconds);

        InputLock.Release(LockKey);
        IsRunning = false;
        onDone?.Invoke();
    }

    static Runner EnsureRunner()
    {
        if (runner != null) return runner;
        var go = new GameObject("ScreenTransitionRunner");
        UnityEngine.Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();
        return runner;
    }

    // A MonoBehaviour to host the coroutine; ScreenTransition itself is static and can't.
    class Runner : MonoBehaviour { }
}

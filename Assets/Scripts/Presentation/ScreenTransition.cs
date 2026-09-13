using System;
using System.Collections;
using UnityEngine;

// Which timing block of TransitionSettings a transition uses. Door stays quick (room-to-room
// teleport); Sleep and Wake are the slower bed/dream beats.
public enum TransitionKind { Door, Sleep, Wake }

// Shared screen-black transition: input lock -> fade to black -> whileBlack -> optional caption
// -> hold -> fade back in -> unlock -> onDone. Timing per TransitionKind comes from
// TransitionSettings.Current, re-read every time a transition starts so Inspector edits in Play
// mode take effect on the next one. TransitionDoor uses Teleport (Door); the dream bed/wake flow
// uses LoadScene with Sleep/Wake. Only one transition runs at a time.
public static class ScreenTransition
{
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
    // optionally holds on a caption title card, holds briefly, fades back in, unlocks, then
    // calls onDone. Timing comes from TransitionSettings.Current for the given kind. Returns
    // false without doing anything if a transition is already running.
    public static bool Run(TransitionKind kind, Func<IEnumerator> whileBlack, Action onDone = null, string caption = null)
    {
        if (IsRunning) return false;
        IsRunning = true;
        EnsureRunner().StartCoroutine(RunRoutine(kind, whileBlack, onDone, caption));
        return true;
    }

    public static bool Teleport(FirstPersonController player, Transform destination)
    {
        if (player == null || destination == null) return false;
        return Run(TransitionKind.Door, () => TeleportRoutine(player, destination));
    }

    // Loads sceneName while the screen is black; afterLoad runs once it's in. If caption is set,
    // it shows as a title card on the black screen before the screen fades back in. onDone runs
    // after the screen is fully visible again and this transition's own input lock is released
    // (a caller that needs to keep control past that point, like WakeUp, takes its own lock).
    public static bool LoadScene(TransitionKind kind, string sceneName, Action afterLoad = null, string caption = null, Action onDone = null)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        return Run(kind, () => LoadSceneRoutine(sceneName, afterLoad), onDone, caption);
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

    static IEnumerator RunRoutine(TransitionKind kind, Func<IEnumerator> whileBlack, Action onDone, string caption)
    {
        // Read fresh every time a transition starts, so an Inspector edit made mid-Play takes
        // effect on the very next transition rather than the one already running.
        var (fadeOut, hold, fadeIn, captionSeconds) = TransitionSettings.Current.For(kind);

        InputLock.Acquire(LockKey);
        var fader = ScreenFader.Instance;
        yield return fader.FadeTo(1f, fadeOut);

        var inner = whileBlack?.Invoke();
        if (inner != null) yield return inner;

        if (!string.IsNullOrEmpty(caption))
        {
            fader.Caption.text = caption;
            fader.Caption.gameObject.SetActive(true);
            yield return new WaitForSeconds(captionSeconds);
            fader.Caption.gameObject.SetActive(false);
        }

        if (hold > 0f) yield return new WaitForSeconds(hold);
        yield return fader.FadeTo(0f, fadeIn);

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

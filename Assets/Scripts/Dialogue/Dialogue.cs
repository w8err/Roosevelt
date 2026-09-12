using System;
using UnityEngine;

// Runs one DialogueData through a DialogueRunner and DialogueView. Holds InputLock for the
// whole conversation, so nothing else can move or interact until it ends.
public static class Dialogue
{
    static readonly object LockKey = new object();
    static Action onFinished;

    public static bool IsPlaying { get; private set; }

    // Play mode starts without a domain reload in this project, so a conversation left over
    // from the previous session (and the lock it held) must not survive into the next one.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        IsPlaying = false;
        onFinished = null;
        InputLock.Release(LockKey);
    }

    // Ignored if a conversation is already playing or data is null.
    public static bool Play(DialogueData data, Action onFinished = null)
    {
        if (IsPlaying || data == null) return false;
        IsPlaying = true;
        Dialogue.onFinished = onFinished;
        InputLock.Acquire(LockKey);

        var runner = new DialogueRunner(data, GameState.HasFlag, GameState.SetFlag);
        runner.Start();
        if (runner.IsFinished) NotifyFinished();
        else DialogueView.Instance.Show(runner);
        return true;
    }

    // Called by DialogueView once the runner has no more nodes to show.
    internal static void NotifyFinished()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        InputLock.Release(LockKey);
        DialogueView.Instance.Hide();
        var callback = onFinished;
        onFinished = null;
        callback?.Invoke();
    }
}

using System.Collections;
using UnityEngine;

// A single global volume multiplier that every AmbientLoop and RandomSoundEmitter reads each
// time it sets its own AudioSource volume: Volume = Duck * AmbienceSettings.Current.masterVolume.
// Duck (0..1) is this class's own fade/snap state -- ScreenTransition ducks it to 0 across every
// screen-black transition (and back to 1 on fade-in); ChairInteractable ducks it partway during
// the ending contemplation. masterVolume is the separate, Inspector-tunable overall level
// (AmbienceSettings.asset), read fresh on every Volume access, so ducking always acts as a
// fraction of whatever the master is currently set to (e.g. duck 0.3 at master 1.5 plays at
// 0.45, not a fixed 0.3) and a Play-mode Inspector edit to the master is audible immediately.
// No AudioMixer: a plain scripted multiplier is enough for one global duck plus one master
// level, and keeps this dependency-free for whoever places sounds via LabSceneBuilder.
public static class Ambience
{
    public static float Volume => duck * AmbienceSettings.Current.masterVolume;

    static float duck = 1f;
    static Runner runner;
    static Coroutine active;

    // Play mode starts without a domain reload in this project, so a fade left running from
    // the previous session must not survive into the next one.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        duck = 1f;
        runner = null;
        active = null;
    }

    public static void FadeTo(float target, float seconds)
    {
        EnsureRunner();
        if (active != null) runner.StopCoroutine(active);
        active = runner.StartCoroutine(FadeRoutine(target, seconds));
    }

    // Sets the duck instantly, with no fade. Used to match ScreenFader.SnapToBlack() at the
    // 0-day boot, so ambience doesn't play at full volume during the snapped-black first frame.
    public static void SnapTo(float value)
    {
        if (active != null && runner != null) runner.StopCoroutine(active);
        active = null;
        duck = value;
    }

    static IEnumerator FadeRoutine(float target, float seconds)
    {
        var start = duck;
        if (seconds <= 0f)
        {
            duck = target;
            yield break;
        }
        var elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            duck = Mathf.Lerp(start, target, elapsed / seconds);
            yield return null;
        }
        duck = target;
    }

    static void EnsureRunner()
    {
        if (runner != null) return;
        var go = new GameObject("AmbienceRunner");
        Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();
    }

    // A MonoBehaviour to host the coroutine; Ambience itself is static and can't.
    class Runner : MonoBehaviour { }
}

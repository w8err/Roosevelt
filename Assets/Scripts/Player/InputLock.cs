using System.Collections.Generic;
using UnityEngine;

// Takes moving, looking and interacting away from the player while dialogue, transitions or
// cutscenes run. Each holder locks with itself as the key, so overlapping holders stack
// (control returns only once all of them release) and releasing twice is harmless.
public static class InputLock
{
    static readonly HashSet<object> holders = new HashSet<object>();

    public static bool IsLocked
    {
        get
        {
            // A holder destroyed without releasing (say, by a scene unload) must not freeze the player.
            holders.RemoveWhere(h => h is Object o && o == null);
            return holders.Count > 0;
        }
    }

    public static void Acquire(object holder) => holders.Add(holder);
    public static void Release(object holder) => holders.Remove(holder);

    // Play mode starts without a domain reload in this project, so statics survive between runs.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => holders.Clear();
}

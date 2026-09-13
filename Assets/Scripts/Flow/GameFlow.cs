using UnityEngine;
using UnityEngine.SceneManagement;

// Moves the player between Reality and each night's dream. It builds itself after the
// first scene loads, so Play works from any scene; that run is a new game at Day 0 and
// never loads the save on its own.
public sealed class GameFlow : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        var go = new GameObject("GameFlow");
        DontDestroyOnLoad(go);
        go.AddComponent<GameFlow>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        go.AddComponent<DebugPanel>();
#endif
        // Starting Play directly in Reality (0-day, or any test) skips the usual scene-load
        // path, so nothing else would trigger the "waking up" beat. WakeUp no-ops if the scene
        // has no bed.
        if (SceneManager.GetActiveScene().name == SceneNames.Reality) WakeUp.PlayOnBoot();
    }

    static bool Dreaming => SceneNames.IsDream(SceneManager.GetActiveScene().name);

    public static void EnterDream()
    {
        if (Busy()) return;
        if (Dreaming)
        {
            Debug.Log("[GameFlow] Ignored EnterDream: already in a dream.");
            return;
        }
        Go(SceneNames.DreamFor(GameState.Day));
    }

    // Waking up starts the next day, and that is the moment the game saves.
    public static void EndDream()
    {
        if (Busy()) return;
        if (!Dreaming)
        {
            Debug.Log("[GameFlow] Ignored EndDream: not in a dream.");
            return;
        }
        if (GameState.Day == GameState.LastDay)
        {
            Debug.Log("[GameFlow] The last dream ended. The ending is not built yet.");
            return;
        }
        GameState.AdvanceDay();
        SaveSystem.Save();
        Go(SceneNames.Reality, $"Day {GameState.Day}", wakeUp: true);
    }

    public static void ContinueFromSave()
    {
        if (Busy()) return;
        if (!SaveSystem.Load())
        {
            Debug.LogWarning("[GameFlow] There is no save to continue from.");
            return;
        }
        Go(SceneNames.Reality, wakeUp: true);
    }

    // Checked before touching Day or the save, so a request made mid-transition changes nothing.
    static bool Busy()
    {
        if (!ScreenTransition.IsRunning) return false;
        Debug.Log("[GameFlow] Ignored: a screen transition is already running.");
        return true;
    }

    static void Go(string sceneName, string caption = null, bool wakeUp = false)
    {
        var afterLoad = wakeUp ? (System.Action)WakeUp.PoseLyingInstant : null;
        var onDone = wakeUp ? (System.Action)WakeUp.StartStandingUp : null;
        if (ScreenTransition.LoadScene(sceneName, afterLoad, caption, onDone)) Debug.Log($"[GameFlow] Day {GameState.Day}: loading {sceneName}");
        else Debug.Log($"[GameFlow] Could not start loading {sceneName}.");
    }
}

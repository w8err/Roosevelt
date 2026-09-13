using UnityEngine;

// Screen-transition timing, tunable in the Inspector via Assets/Resources/TransitionSettings.asset
// (auto-created by Editor/TransitionSettingsCreator.cs if missing). ScreenTransition reads
// Current fresh every time a transition starts, so editing the asset's values in Play mode
// takes effect starting with the very next transition, not the one already running.
[CreateAssetMenu(fileName = "TransitionSettings", menuName = "Roosevelt/Transition Settings")]
public class TransitionSettings : ScriptableObject
{
    const string ResourcePath = "TransitionSettings";

    [Header("Door (room-to-room teleport, no scene load — stays quick)")]
    [Tooltip("Fade to black before teleporting, in seconds.")]
    public float doorFadeOut = 0.35f;
    [Tooltip("Seconds the screen stays fully black after teleporting, before fading back in.")]
    public float doorHold = 0.15f;
    [Tooltip("Fade back in after teleporting, in seconds.")]
    public float doorFadeIn = 0.35f;

    [Header("Sleep (lying down in bed -> entering tonight's dream)")]
    [Tooltip("Fade to black as the player falls asleep, in seconds.")]
    public float sleepFadeOut = 1.75f;
    [Tooltip("Seconds the screen stays fully black once the dream scene has loaded, before fading in.")]
    public float sleepBlackHold = 1.25f;
    [Tooltip("Fade in as the dream scene is revealed, in seconds.")]
    public float sleepFadeIn = 1.75f;

    [Header("Wake (a dream ends -> waking up in Reality; also the 0-day boot)")]
    [Tooltip("Fade to black as the dream ends, in seconds.")]
    public float wakeFadeOut = 1.75f;
    [Tooltip("Seconds the \"Day N\" caption sits on the black screen. Only shown when EndDream advances the day; skipped for ContinueFromSave and the 0-day boot.")]
    public float wakeCaptionSeconds = 2f;
    [Tooltip("Seconds the screen stays fully black (after the caption, if any) before fading in. Also used to hold black at the very start of the 0-day boot.")]
    public float wakeBlackHold = 1.25f;
    [Tooltip("Fade in as the player wakes up (lying, eyes toward the ceiling), in seconds.")]
    public float wakeFadeIn = 1.75f;

    static TransitionSettings fallback;
    static bool warnedMissing;

    // Resources.Load caches the same instance internally, so repeated calls are cheap and all
    // return the one asset whose fields the Inspector is editing live during Play.
    public static TransitionSettings Current
    {
        get
        {
            var asset = Resources.Load<TransitionSettings>(ResourcePath);
            if (asset != null) return asset;

            if (!warnedMissing)
            {
                warnedMissing = true;
                Debug.LogWarning($"[TransitionSettings] Assets/Resources/{ResourcePath}.asset not found; using built-in default timings.");
            }
            if (fallback == null) fallback = CreateInstance<TransitionSettings>();
            return fallback;
        }
    }

    public (float fadeOut, float hold, float fadeIn, float captionSeconds) For(TransitionKind kind) => kind switch
    {
        TransitionKind.Door => (doorFadeOut, doorHold, doorFadeIn, 0f),
        TransitionKind.Sleep => (sleepFadeOut, sleepBlackHold, sleepFadeIn, 0f),
        TransitionKind.Wake => (wakeFadeOut, wakeBlackHold, wakeFadeIn, wakeCaptionSeconds),
        _ => (0f, 0f, 0f, 0f),
    };
}

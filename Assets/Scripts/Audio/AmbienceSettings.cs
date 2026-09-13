using UnityEngine;

// Overall ambience loudness, tunable in the Inspector via Assets/Resources/AmbienceSettings.asset
// (auto-created by Editor/AmbienceSettingsCreator.cs if missing). Ambience.Volume reads Current
// fresh on every access (AmbientLoop/RandomSoundEmitter already refresh their own volume every
// frame), so a Play-mode edit here is audible immediately -- unlike TransitionSettings, which is
// only re-read when a transition starts, there's no equivalent "next transition" moment to wait
// for here.
[CreateAssetMenu(fileName = "AmbienceSettings", menuName = "Roosevelt/Ambience Settings")]
public class AmbienceSettings : ScriptableObject
{
    const string ResourcePath = "AmbienceSettings";

    [Tooltip("Overall ambience loudness, multiplied on top of each sound's own volume field from the layout JSON. 1 = as authored; this defaults above 1 because the authored levels read a bit quiet.")]
    [Range(0f, 3f)]
    public float masterVolume = 1.5f;

    static AmbienceSettings fallback;
    static bool warnedMissing;

    // Resources.Load caches the same instance internally, so repeated calls are cheap and all
    // return the one asset whose fields the Inspector is editing live during Play.
    public static AmbienceSettings Current
    {
        get
        {
            var asset = Resources.Load<AmbienceSettings>(ResourcePath);
            if (asset != null) return asset;

            if (!warnedMissing)
            {
                warnedMissing = true;
                Debug.LogWarning($"[AmbienceSettings] Assets/Resources/{ResourcePath}.asset not found; using built-in default (masterVolume 1.5).");
            }
            if (fallback == null) fallback = CreateInstance<AmbienceSettings>();
            return fallback;
        }
    }
}

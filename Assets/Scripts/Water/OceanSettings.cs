using UnityEngine;

// Ocean wave parameters, tunable in the Inspector via Assets/Resources/OceanSettings.asset
// (auto-created by Editor/OceanSettingsCreator.cs if missing). This is the single place these
// numbers live: OceanTestBuilder reads Current and pushes the same values onto Ocean.shader's
// material, and BoatBuoyancy reads Current and evaluates the identical formula (OceanWaves) in C#
// to float a boat. Edit this asset, not a material or a script constant, to retune the sea.
[CreateAssetMenu(fileName = "OceanSettings", menuName = "Roosevelt/Ocean Settings")]
public class OceanSettings : ScriptableObject
{
    const string ResourcePath = "OceanSettings";

    [System.Serializable]
    public struct Wave
    {
        public Vector2 direction;
        public float wavelength;
        public float amplitude;
        public float speed;
    }

    // Wavelengths and direction angles are deliberately irregular relative to each other: round
    // wavelengths or simple angles (0/45/90 degrees apart) make the sine sum's peaks line up into a
    // visible repeating lattice once a whole patch is in view. See Ocean.shader's header comment for
    // the same note and the reasoning behind these specific numbers.
    //
    // The shortest wavelength (7.9m) is deliberately kept well above the rowboat's 2.42m length (see
    // BoatBuoyancy): a hull that spans a large fraction of the shortest wavelength sits on visibly
    // curved water, which no amount of sampling at the hull's own two ends can represent — the fix for
    // "the boat pokes through the surface" is a longer shortest wavelength, not less amplitude (that
    // amplitude is what makes the horizon read as anything at all).
    public Wave[] waves =
    {
        new Wave { direction = new Vector2(1f, 0f), wavelength = 19.7f, amplitude = 0.35f, speed = 2.0f },
        new Wave { direction = new Vector2(0.7986f, 0.6018f), wavelength = 13.7f, amplitude = 0.20f, speed = 1.3f },
        new Wave { direction = new Vector2(-0.1908f, 0.9816f), wavelength = 10.3f, amplitude = 0.09f, speed = 1.7f },
        new Wave { direction = new Vector2(-0.8572f, 0.5150f), wavelength = 7.9f, amplitude = 0.05f, speed = 2.6f },
    };

    static OceanSettings fallback;
    static bool warnedMissing;

    // Resources.Load caches the same instance internally, so repeated calls are cheap and all
    // return the one asset whose fields the Inspector is editing live during Play.
    public static OceanSettings Current
    {
        get
        {
            var asset = Resources.Load<OceanSettings>(ResourcePath);
            if (asset != null) return asset;

            if (!warnedMissing)
            {
                warnedMissing = true;
                Debug.LogWarning($"[OceanSettings] Assets/Resources/{ResourcePath}.asset not found; using built-in default waves.");
            }
            if (fallback == null) fallback = CreateInstance<OceanSettings>();
            return fallback;
        }
    }
}

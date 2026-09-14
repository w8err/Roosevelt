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

    // Whitecap foam. Visual only — nothing in C# reads these (BoatBuoyancy floats on the height above
    // and neither knows nor cares about foam); they live here anyway so the sea has one asset to tune
    // rather than "waves here, foam on whichever material". OceanTestBuilder pushes them onto the
    // material next to the wave values. See Ocean.shader's header for why the noise exists at all: foam
    // keyed on height alone paints the wave lattice, and these are the numbers that stop it.
    [Header("Foam (visual only)")]
    public Color foamColor = new Color(0.76f, 0.86f, 0.84f, 1f);

    [Tooltip("How much of the sea foams. Drives how high a crest must reach before it can foam. Too " +
        "high a bar is not just 'less foam': foam shrinks to isolated dots sitting exactly on the wave " +
        "lattice, which is what made it read as a grid. Real whitecaps are broken streaks running " +
        "along a crest, so keep this generous enough that foam stretches rather than beads.")]
    [Range(0f, 1f)] public float foamAmount = 0.7f;

    [Tooltip("Width of the water-to-foam fade, in normalized wave height. Small = hard pixel-art edge.")]
    [Range(0.005f, 0.5f)] public float foamSoftness = 0.05f;

    [Tooltip("Foam patch size as 1/metres. Must be fine enough that neighbouring crests get " +
        "uncorrelated values — the interference peaks sit about 14m apart, and a first attempt at " +
        "0.08 (12.5m patches) let the lattice survive at full strength. 0.22 is ~4.5m patches.")]
    public float foamPatchScale = 0.18f;

    [Tooltip("How far the noise raises the foam threshold. THE ANTI-LATTICE KNOB: at 0 every crest of " +
        "equal height foams and the interference grid comes back. Has to be large, because the lattice " +
        "peaks all reach nearly the same height.")]
    [Range(0f, 1.5f)] public float foamPatchStrength = 0.55f;

    [Tooltip("Drift of the foam patches across the water, m/s, so foam isn't pinned to world spots.")]
    public float foamDrift = 0.03f;

    [Tooltip("How far, in metres, foam's view of the surface is bent away from the real one. This is " +
        "what gets foam off the interference lattice rather than only thinning it — suppression alone " +
        "leaves the survivors standing on the same grid. Small against the 19.7m dominant wavelength, " +
        "or foam stops looking attached to the waves.")]
    public float foamWarp = 2.0f;

    [Tooltip("Size of the bending, 1/metres. Coarse on purpose: it should distort whole stretches of " +
        "crest, not jitter each fragment.")]
    public float foamWarpScale = 0.05f;

    [Tooltip("How hard a fine noise chews the foam edges. At 0 foam comes out as large smooth lobes " +
        "that read as drifting ice up close, and whose identical clean outlines are most of what lets " +
        "the eye find the wave lattice again.")]
    [Range(0f, 1f)] public float foamErosion = 0.4f;

    [Tooltip("Size of that chewing, 1/metres. At 0.35 (~3m) the erosion was coarser than the foam a " +
        "few metres from the boat, so near patches stayed flat unbroken sheets and read as ice rather " +
        "than foam. Sub-metre gives them visible grain where the player actually sees them; it does " +
        "sparkle in the distance, which the project treats as intended Point-filter texture.")]
    public float foamErosionScale = 0.9f;

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

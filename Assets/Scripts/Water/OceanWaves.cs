using UnityEngine;

// The single C# reimplementation of Ocean.shader's vertex displacement (see that shader's header
// comment for the formula itself, and why _Time.y is Time.timeSinceLevelLoad, not Time.time). Anything
// in C# that needs the ocean's height — BoatBuoyancy today, a future oar/swim system — calls this
// instead of writing its own copy, so the two implementations (HLSL and C#) can only ever drift apart
// at the shared numbers in OceanSettings, never in the math itself.
public static class OceanWaves
{
    public static float Height(OceanSettings settings, Vector2 worldXZ, float time)
    {
        var height = 0f;
        var waves = settings.waves;
        for (var i = 0; i < waves.Length; i++) height += WaveHeight(waves[i], worldXZ, time);
        return height;
    }

    // A rotation that tilts world-up to match the water's slope through four explicit world-space
    // points (bow/stern/port/starboard) and their heights, leaving yaw untouched (FromToRotation's
    // shortest-arc rotation between two near-vertical vectors has no component around world-up) —
    // multiply it onto a boat's own fixed heading to get its roll/pitch.
    //
    // Takes the points themselves rather than a center + distances: an earlier version built the
    // sample points by adding (0, distance) / (distance, 0) to worldXZ directly, i.e. assuming bow is
    // always toward world +Z and starboard always toward world +X. That happens to be right for a boat
    // at yaw 0 (this project's Water_Test boat), but is wrong the moment a boat is placed facing any
    // other direction. The caller (BoatBuoyancy) now builds each point from its own transform.forward/
    // right, so this stays correct at any yaw.
    public static Quaternion Slope(Vector2 bowPoint, float bowHeight, Vector2 sternPoint, float sternHeight,
        Vector2 portPoint, float portHeight, Vector2 starboardPoint, float starboardHeight)
    {
        var tangentBowStern = new Vector3(bowPoint.x - sternPoint.x, bowHeight - sternHeight, bowPoint.y - sternPoint.y);
        var tangentPortStarboard = new Vector3(starboardPoint.x - portPoint.x, starboardHeight - portHeight, starboardPoint.y - portPoint.y);
        var normal = Vector3.Cross(tangentBowStern, tangentPortStarboard).normalized;
        return Quaternion.FromToRotation(Vector3.up, normal);
    }

    static float WaveHeight(OceanSettings.Wave wave, Vector2 worldXZ, float time)
    {
        var wavelength = Mathf.Max(wave.wavelength, 0.001f);
        var k = 2f * Mathf.PI / wavelength;
        var phase = Vector2.Dot(wave.direction.normalized, worldXZ) * k + time * wave.speed * k;
        return wave.amplitude * Mathf.Sin(phase);
    }
}

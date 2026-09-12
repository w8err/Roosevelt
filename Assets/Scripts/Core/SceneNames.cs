using System;

// Scenes the game flow moves between. Reality holds every waking area;
// each day has its own dream scene, Dream_00 to Dream_05.
public static class SceneNames
{
    public const string Reality = "Reality";
    const string DreamPrefix = "Dream_";

    public static string DreamFor(int day)
    {
        if (day < GameState.FirstDay || day > GameState.LastDay)
            throw new ArgumentOutOfRangeException(nameof(day), $"Day {day} is outside {GameState.FirstDay}..{GameState.LastDay}.");
        return DreamPrefix + day.ToString("00");
    }

    // Awake or dreaming is read from the loaded scene rather than stored in GameState.
    public static bool IsDream(string sceneName) =>
        sceneName != null && sceneName.StartsWith(DreamPrefix, StringComparison.Ordinal);
}

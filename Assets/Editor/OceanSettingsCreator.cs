using UnityEditor;
using UnityEngine;

// Creates Assets/Resources/OceanSettings.asset with its default wave parameters the first time the
// editor loads, if it doesn't exist yet. Same shape as TransitionSettingsCreator.cs/AmbienceSettingsCreator.cs
// — except TransitionSettings/AmbienceSettings only ever needed "exists by the time the game plays",
// while OceanTestBuilder needs it to exist before an editor build even starts. delayCall alone isn't
// enough for that: OceanTestBuilder's own request-file poll runs off EditorApplication.update from the
// very first tick, which can run before this class's delayCall fires, especially right after a domain
// reload with a request file already waiting. So EnsureExists() is also called directly from
// OceanTestBuilder.Build(), synchronously, before it reads OceanSettings.Current.
[InitializeOnLoad]
static class OceanSettingsCreator
{
    const string ResourcesDir = "Assets/Resources";
    const string AssetPath = ResourcesDir + "/OceanSettings.asset";

    static OceanSettingsCreator()
    {
        EditorApplication.delayCall += () => EnsureExists();
    }

    internal static OceanSettings EnsureExists()
    {
        var existing = AssetDatabase.LoadAssetAtPath<OceanSettings>(AssetPath);
        if (existing != null) return existing;
        if (!AssetDatabase.IsValidFolder(ResourcesDir))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var settings = ScriptableObject.CreateInstance<OceanSettings>();
        AssetDatabase.CreateAsset(settings, AssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[OceanSettingsCreator] Created {AssetPath} with default wave parameters.");
        return settings;
    }
}

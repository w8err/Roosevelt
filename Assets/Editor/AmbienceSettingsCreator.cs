using UnityEditor;
using UnityEngine;

// Creates Assets/Resources/AmbienceSettings.asset with its default masterVolume the first time
// the editor loads, if it doesn't exist yet. Same shape as TransitionSettingsCreator.cs.
[InitializeOnLoad]
static class AmbienceSettingsCreator
{
    const string ResourcesDir = "Assets/Resources";
    const string AssetPath = ResourcesDir + "/AmbienceSettings.asset";

    static AmbienceSettingsCreator()
    {
        EditorApplication.delayCall += CreateIfMissing;
    }

    static void CreateIfMissing()
    {
        if (AssetDatabase.LoadAssetAtPath<AmbienceSettings>(AssetPath) != null) return;
        if (!AssetDatabase.IsValidFolder(ResourcesDir))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var settings = ScriptableObject.CreateInstance<AmbienceSettings>();
        AssetDatabase.CreateAsset(settings, AssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AmbienceSettingsCreator] Created {AssetPath} with default masterVolume.");
    }
}

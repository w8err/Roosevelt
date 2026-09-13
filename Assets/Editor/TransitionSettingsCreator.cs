using UnityEditor;
using UnityEngine;

// Creates Assets/Resources/TransitionSettings.asset with default timing values the first time
// the editor loads, if it doesn't exist yet, so ScreenTransition (via Resources.Load) and the
// Inspector both always have something to work with.
[InitializeOnLoad]
static class TransitionSettingsCreator
{
    const string ResourcesDir = "Assets/Resources";
    const string AssetPath = ResourcesDir + "/TransitionSettings.asset";

    static TransitionSettingsCreator()
    {
        EditorApplication.delayCall += CreateIfMissing;
    }

    static void CreateIfMissing()
    {
        if (AssetDatabase.LoadAssetAtPath<TransitionSettings>(AssetPath) != null) return;
        if (!AssetDatabase.IsValidFolder(ResourcesDir))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var settings = ScriptableObject.CreateInstance<TransitionSettings>();
        AssetDatabase.CreateAsset(settings, AssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[TransitionSettingsCreator] Created {AssetPath} with default timing values.");
    }
}

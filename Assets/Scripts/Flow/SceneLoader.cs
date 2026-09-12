using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Loads a scene by name. In the editor it also finds scenes missing from Build Settings
// by their path under Assets/Scenes/, so a new scene can be tried before it is listed.
public static class SceneLoader
{
    const string SceneFolder = "Assets/Scenes/";

    // Null, with one warning, when the scene doesn't exist yet. Checking first keeps
    // Unity's red "couldn't be loaded" error out of the console.
    public static AsyncOperation LoadAsync(string sceneName)
    {
        if (Application.CanStreamedLevelBeLoaded(sceneName)) return SceneManager.LoadSceneAsync(sceneName);
#if UNITY_EDITOR
        var path = SceneFolder + sceneName + ".unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
        Debug.LogWarning($"[SceneLoader] 씬이 아직 없음: {sceneName}");
        return null;
    }
}

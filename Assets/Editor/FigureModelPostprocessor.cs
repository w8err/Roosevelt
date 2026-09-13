using UnityEditor;

public class FigureModelPostprocessor : AssetPostprocessor
{
    // Target paths: Lab researcher and Dream seated figures
    static bool IsFigureModel(string path) =>
        path.Contains("Assets/Art/Environment/Lab/Figures/Meshes/") ||
        (path.Contains("Assets/Art/Environment/Forest/Meshes/") && path.Contains("Figure_"));

    void OnPreprocessModel()
    {
        if (!IsFigureModel(assetPath)) return;

        var importer = (ModelImporter)assetImporter;
        // Generic rig with self as avatar for animation playback
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        // Import animations
        importer.importAnimation = true;
    }

    void OnPreprocessAnimation()
    {
        if (!IsFigureModel(assetPath)) return;

        var importer = (ModelImporter)assetImporter;
        var clips = importer.clipAnimations;
        // If no explicit clips, use default clips from FBX
        if (clips.Length == 0)
            clips = importer.defaultClipAnimations;

        // Configure animation clips: Idle loops, others play once
        foreach (var clip in clips)
            clip.loopTime = clip.name == "Idle";

        // Re-assign array for changes to persist
        importer.clipAnimations = clips;
    }
}

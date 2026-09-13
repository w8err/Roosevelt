using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds a standalone player from Build Settings' scene list, so the game can be checked outside
// the editor. SceneLoader falls back to EditorSceneManager for scenes missing from that list, which
// only works in the editor — a build is the only way to catch a scene that was never registered.
//
// Batch mode: Unity.exe -batchmode -nographics -quit -projectPath <proj>
//   -executeMethod GameBuilder.BuildWindows -logFile <log>
public static class GameBuilder
{
    const string OutputDir = "Build/Windows";
    const string ExeName = "Roosevelt.exe";

    [MenuItem("Roosevelt/Build/Windows (x64)")]
    public static void BuildWindows()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("[GameBuilder] Build Settings has no enabled scenes.");
            EditorApplication.Exit(1);
            return;
        }
        Debug.Log($"[GameBuilder] building {scenes.Length} scene(s): {string.Join(", ", scenes)}");

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = $"{OutputDir}/{ExeName}",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });

        var summary = report.summary;
        Debug.Log($"[GameBuilder] {summary.result}: {summary.totalErrors} error(s), "
            + $"{summary.totalWarnings} warning(s), {summary.totalSize / 1048576} MB, {summary.totalTime}");
        if (summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}

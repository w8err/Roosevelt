using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Settings for ProjectWindowDecorator, kept in Assets/Settings/EditorTools/ProjectWindowSettings.asset (created
// with the defaults below on first load). Editing the asset repaints the Project window immediately.
public class ProjectWindowSettings : ScriptableObject
{
    public const string AssetPath = "Assets/Settings/EditorTools/ProjectWindowSettings.asset";

    [Serializable]
    public class FolderRule
    {
        [Tooltip("Folder path this rule applies to, e.g. Assets/Art. Matches the folder itself and, if inherited, its subfolders.")]
        public string pathPrefix;
        public Color color = Color.white;
        [Tooltip("Built-in editor icon name (EditorGUIUtility.FindTexture), e.g. \"Prefab Icon\". Empty = no icon.")]
        public string iconName;
        [Tooltip("Subfolders get the color too (not the icon).")]
        public bool inheritToChildren = true;
    }

    public bool enabled = true;
    public bool zebraStripes = true;
    public bool treeLines = true;
    [Tooltip("Folders without a rule icon show a small icon when all their direct files are the same kind.")]
    public bool contentIcons = true;
    [Tooltip("Row tint for folders matched by a rule (list/tree view).")]
    public bool rowTint = true;
    [Range(0f, 1f)] public float tintAlpha = 0.12f;
    [Tooltip("Tree indent per depth level in pixels. Adjust if lines don't sit under the foldout arrows.")]
    public float indentWidth = 14f;
    public Color lineColor = new(0.5f, 0.5f, 0.5f, 0.5f);
    [Tooltip("Longest matching path prefix wins.")]
    public List<FolderRule> rules = new();

    public static List<FolderRule> DefaultRules() => new()
    {
        new FolderRule { pathPrefix = "Assets/Art", color = new Color(0.95f, 0.6f, 0.3f), iconName = "Texture Icon" },
        new FolderRule { pathPrefix = "Assets/Prefabs", color = new Color(0.4f, 0.65f, 1f), iconName = "Prefab Icon" },
        new FolderRule { pathPrefix = "Assets/Scenes", color = new Color(0.45f, 0.85f, 0.45f), iconName = "SceneAsset Icon" },
        new FolderRule { pathPrefix = "Assets/Scripts", color = new Color(0.7f, 0.5f, 1f), iconName = "cs Script Icon" },
        new FolderRule { pathPrefix = "Assets/Editor", color = new Color(0.55f, 0.65f, 0.8f), iconName = "UnityEditor.InspectorWindow" },
        new FolderRule { pathPrefix = "Assets/Settings", color = new Color(0.65f, 0.65f, 0.65f), iconName = "SettingsIcon" },
        new FolderRule { pathPrefix = "Assets/Resources", color = new Color(1f, 0.85f, 0.3f), iconName = "Linked" },
        new FolderRule { pathPrefix = "Assets/Audio", color = new Color(0.3f, 0.85f, 0.8f), iconName = "AudioClip Icon" },
        new FolderRule { pathPrefix = "Assets/Ambience", color = new Color(0.3f, 0.85f, 0.8f), iconName = "AudioClip Icon" },
        new FolderRule { pathPrefix = "Assets/Shaders", color = new Color(0.95f, 0.45f, 0.7f), iconName = "Shader Icon" },
        new FolderRule { pathPrefix = "Assets/Tests", color = new Color(0.5f, 0.8f, 0.6f), iconName = "" },
        new FolderRule { pathPrefix = "Packages", color = new Color(0.55f, 0.55f, 0.6f), iconName = "", inheritToChildren = false },
    };

    void OnValidate()
    {
        ProjectWindowDecorator.ClearCaches();
        EditorApplication.RepaintProjectWindow();
    }
}

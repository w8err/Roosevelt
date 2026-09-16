using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Settings for HierarchyDecorator, kept in Assets/Settings/EditorTools/HierarchySettings.asset (created with
// the defaults below on first load). Editing the asset repaints the Hierarchy immediately.
public class HierarchySettings : ScriptableObject
{
    public const string AssetPath = "Assets/Settings/EditorTools/HierarchySettings.asset";

    [Serializable]
    public class NameRule
    {
        [Tooltip("Matches when the GameObject name starts with this text (case sensitive).")]
        public string prefix;
        public Color color = Color.white;
    }

    public bool enabled = true;
    public bool zebraStripes = true;
    public bool treeLines = true;
    public bool activeToggle = true;
    public bool componentIcons = true;
    [Range(1, 8)] public int maxComponentIcons = 4;
    [Tooltip("Tints the whole row of any object whose name starts with a rule's prefix. First match wins.")]
    public List<NameRule> nameRules = new();

    // Scene builder group names (LabSceneBuilder) and the player rig.
    public static List<NameRule> DefaultRules() => new()
    {
        new NameRule { prefix = "PF_Player", color = new Color(0.35f, 0.75f, 0.45f) },
        new NameRule { prefix = "Spawn", color = new Color(0.45f, 0.65f, 0.95f) },
        new NameRule { prefix = "Door", color = new Color(0.85f, 0.6f, 0.35f) },
        new NameRule { prefix = "Light", color = new Color(0.95f, 0.85f, 0.35f) },
        new NameRule { prefix = "Sun", color = new Color(0.95f, 0.85f, 0.35f) },
        new NameRule { prefix = "Sound", color = new Color(0.7f, 0.5f, 0.9f) },
        new NameRule { prefix = "Creatures", color = new Color(0.9f, 0.4f, 0.4f) },
        new NameRule { prefix = "Props", color = new Color(0.55f, 0.8f, 0.8f) },
        new NameRule { prefix = "Boxes", color = new Color(0.6f, 0.6f, 0.6f) },
        new NameRule { prefix = "Camera", color = new Color(0.5f, 0.85f, 0.95f) },
    };

    void OnValidate() => HierarchyDecorator.Refresh();
}

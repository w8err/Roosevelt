using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// vFolders-style Project window decorations, built only on the public item callback (no reflection into the
// Project window): zebra stripes, folder color/icon rules by path, content-based folder icons and tree lines.
// Settings: ProjectWindowSettings. Menus: Roosevelt > Tools > Project Window.
[InitializeOnLoad]
public static class ProjectWindowDecorator
{
    // Grid (icon) view items are taller than a list row.
    const float GridThreshold = 20f;

    static ProjectWindowSettings settings;

    // Folder path -> representative icon for its content (null = mixed or empty).
    static readonly Dictionary<string, Texture> contentIcons = new();
    // Folder path -> child listing used for tree lines.
    static readonly Dictionary<string, Children> children = new();
    static readonly Dictionary<string, Texture2D> namedIcons = new();

    // x of the "Assets"/"Packages" rows in a tree; learned when seen, guessed until then.
    static float rootX = 16f;
    // Two-column's left tree shows folders only; one-column also shows files. Detected from recent rows.
    static double lastTreeFileTime = -10;

    class Children
    {
        public bool hasFolders;
        public bool hasFiles;
        public string lastFolder;   // full asset path
        public string lastAny;      // full asset path, files sort after folders
    }

    enum Kind { None, Mixed, Script, Scene, Prefab, Texture, Material, Audio }

    static ProjectWindowDecorator()
    {
        EditorApplication.projectWindowItemByEntityIdOnGUI += OnItem;
        EditorApplication.projectChanged += ClearCaches;
        EditorApplication.delayCall += () => Load();
    }

    public static void ClearCaches()
    {
        contentIcons.Clear();
        children.Clear();
        namedIcons.Clear();
    }

    [MenuItem("Roosevelt/Tools/Project Window/Settings")]
    static void SelectSettings() => Selection.activeObject = Load();

    [MenuItem("Roosevelt/Tools/Project Window/Toggle")]
    static void Toggle()
    {
        var s = Load();
        if (s == null) return;
        s.enabled = !s.enabled;
        EditorUtility.SetDirty(s);
        AssetDatabase.SaveAssetIfDirty(s);
        EditorApplication.RepaintProjectWindow();
    }

    static ProjectWindowSettings Load()
    {
        if (settings != null) return settings;
        settings = AssetDatabase.LoadAssetAtPath<ProjectWindowSettings>(ProjectWindowSettings.AssetPath);
        if (settings != null) return settings;
        if (EditorApplication.isUpdating || EditorApplication.isCompiling) return null;

        LabSceneBuilder.EnsureFolder("Assets/Settings/EditorTools");
        settings = ScriptableObject.CreateInstance<ProjectWindowSettings>();
        settings.rules = ProjectWindowSettings.DefaultRules();
        AssetDatabase.CreateAsset(settings, ProjectWindowSettings.AssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ProjectWindowDecorator] created {ProjectWindowSettings.AssetPath}");
        return settings;
    }

    // Draw-only callback (no controls), so skipping non-repaint events can't shift control IDs.
    static void OnItem(EntityId id, Rect rect)
    {
        if (Event.current.type != EventType.Repaint) return;
        var s = settings != null ? settings : AssetDatabase.LoadAssetAtPath<ProjectWindowSettings>(ProjectWindowSettings.AssetPath);
        settings = s;
        if (s == null || !s.enabled) return;

        var path = AssetDatabase.GetAssetPath(id);
        if (string.IsNullOrEmpty(path)) return;
        // Sub-assets (e.g. FBX meshes) share the main asset's path; skip them.
        if (AssetDatabase.IsSubAsset(id)) return;

        bool grid = rect.height > GridThreshold;
        bool isFolder = AssetDatabase.IsValidFolder(path);
        int depth = Depth(path);

        if (!grid && depth == 0) rootX = rect.x;
        bool tree = !grid && Mathf.Abs(rect.x - (rootX + depth * s.indentWidth)) < 1.5f;
        if (tree && !isFolder) lastTreeFileTime = EditorApplication.timeSinceStartup;

        var iconRect = grid
            ? new Rect(rect.x, rect.y, rect.width, rect.width)
            : new Rect(rect.x, rect.y, rect.height, rect.height);
        var row = new Rect(tree ? rootX - s.indentWidth : rect.x, rect.y, 0f, rect.height);
        row.xMax = rect.xMax;

        if (!grid && s.zebraStripes && Mathf.RoundToInt(rect.y / rect.height) % 2 == 1)
            EditorGUI.DrawRect(row, new Color(0f, 0f, 0f, EditorGUIUtility.isProSkin ? 0.08f : 0.04f));

        if (!grid && tree && s.treeLines && depth >= 1)
            DrawTreeLines(s, path, depth, rect);

        if (!isFolder) return;

        var rule = Match(s, path, out bool exact);
        Texture badge = null;
        if (rule != null)
        {
            var c = rule.color;
            if (!grid && s.rowTint)
            {
                EditorGUI.DrawRect(row, new Color(c.r, c.g, c.b, exact ? s.tintAlpha : s.tintAlpha * 0.5f));
                EditorGUI.DrawRect(new Rect(row.x, rect.y, 2f, rect.height), new Color(c.r, c.g, c.b, 0.9f));
            }
            var folderIcon = AssetDatabase.GetCachedIcon(path);
            if (folderIcon != null)
            {
                var old = GUI.color;
                GUI.color = Color.Lerp(Color.white, new Color(c.r, c.g, c.b, 1f), exact ? 0.85f : 0.6f);
                GUI.DrawTexture(iconRect, folderIcon, ScaleMode.ScaleToFit);
                GUI.color = old;
            }
            if (exact) badge = NamedIcon(rule.iconName);
        }
        if (badge == null && s.contentIcons) badge = ContentIcon(path);

        if (badge != null)
        {
            float size = Mathf.Round(iconRect.width * 0.6f);
            var r = new Rect(iconRect.xMax - size, iconRect.yMax - size, size, size);
            GUI.DrawTexture(r, badge, ScaleMode.ScaleToFit);
        }
    }

    static int Depth(string path)
    {
        int n = 0;
        foreach (var ch in path) if (ch == '/') n++;
        return n;
    }

    static ProjectWindowSettings.FolderRule Match(ProjectWindowSettings s, string path, out bool exact)
    {
        exact = false;
        ProjectWindowSettings.FolderRule best = null;
        int bestLen = -1;
        foreach (var rule in s.rules)
        {
            if (rule == null || string.IsNullOrEmpty(rule.pathPrefix)) continue;
            var prefix = rule.pathPrefix.TrimEnd('/');
            bool isExact = path == prefix;
            bool isChild = path.StartsWith(prefix + "/");
            if (!isExact && !(isChild && rule.inheritToChildren)) continue;
            if (prefix.Length <= bestLen) continue;
            best = rule;
            bestLen = prefix.Length;
            exact = isExact;
        }
        return best;
    }

    // Built-in editor icon by name; dark-skin variant first. Cached, including misses.
    static Texture2D NamedIcon(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (namedIcons.TryGetValue(name, out var tex)) return tex;
        if (EditorGUIUtility.isProSkin) tex = EditorGUIUtility.FindTexture("d_" + name);
        if (tex == null) tex = EditorGUIUtility.FindTexture(name);
        namedIcons[name] = tex;
        return tex;
    }

    static Texture ContentIcon(string folder)
    {
        if (contentIcons.TryGetValue(folder, out var cached)) return cached;

        var kind = Kind.None;
        foreach (var name in ListEntries(folder, files: true))
        {
            var k = Classify(folder + "/" + name);
            if (kind == Kind.None) kind = k;
            else if (k != kind) { kind = Kind.Mixed; break; }
            if (kind == Kind.Mixed) break;
        }
        Texture icon = kind switch
        {
            Kind.Script => EditorGUIUtility.ObjectContent(null, typeof(MonoScript)).image,
            Kind.Scene => EditorGUIUtility.ObjectContent(null, typeof(SceneAsset)).image,
            Kind.Prefab => NamedIcon("Prefab Icon") ?? EditorGUIUtility.ObjectContent(null, typeof(GameObject)).image,
            Kind.Texture => EditorGUIUtility.ObjectContent(null, typeof(Texture2D)).image,
            Kind.Material => EditorGUIUtility.ObjectContent(null, typeof(Material)).image,
            Kind.Audio => EditorGUIUtility.ObjectContent(null, typeof(AudioClip)).image,
            _ => null,
        };
        contentIcons[folder] = icon;
        return icon;
    }

    static Kind Classify(string assetPath)
    {
        if (assetPath.EndsWith(".prefab")) return Kind.Prefab;
        var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
        if (type == null) return Kind.Mixed;
        if (type == typeof(MonoScript)) return Kind.Script;
        if (type == typeof(SceneAsset)) return Kind.Scene;
        if (typeof(Texture).IsAssignableFrom(type)) return Kind.Texture;
        if (type == typeof(Material)) return Kind.Material;
        if (type == typeof(AudioClip)) return Kind.Audio;
        return Kind.Mixed;
    }

    // Visible entry names (not full paths) directly inside an asset folder, sorted like the Project window.
    static List<string> ListEntries(string folder, bool files)
    {
        var result = new List<string>();
        var physical = FileUtil.GetPhysicalPath(folder);
        if (string.IsNullOrEmpty(physical) || !Directory.Exists(physical)) return result;
        var entries = files ? Directory.GetFiles(physical) : Directory.GetDirectories(physical);
        foreach (var e in entries)
        {
            var name = Path.GetFileName(e);
            if (name.StartsWith(".") || name.EndsWith("~")) continue;
            if (files && name.EndsWith(".meta")) continue;
            result.Add(name);
        }
        result.Sort((a, b) =>
        {
            int c = EditorUtility.NaturalCompare(Path.GetFileNameWithoutExtension(a), Path.GetFileNameWithoutExtension(b));
            return c != 0 ? c : string.CompareOrdinal(a, b);
        });
        return result;
    }

    static Children GetChildren(string folder)
    {
        if (children.TryGetValue(folder, out var c)) return c;
        c = new Children();
        var folders = ListEntries(folder, files: false);
        var files = ListEntries(folder, files: true);
        c.hasFolders = folders.Count > 0;
        c.hasFiles = files.Count > 0;
        if (c.hasFolders) c.lastFolder = folder + "/" + folders[^1];
        c.lastAny = c.hasFiles ? folder + "/" + files[^1] : c.lastFolder;
        children[folder] = c;
        return c;
    }

    static bool FilesShownInTree => EditorApplication.timeSinceStartup - lastTreeFileTime < 1.0;

    static bool IsLast(string path)
    {
        int slash = path.LastIndexOf('/');
        if (slash <= 0) return true;
        var parent = path.Substring(0, slash);
        // Package roots live outside the Packages folder on disk; keep their line running.
        if (parent == "Packages") return false;
        var c = GetChildren(parent);
        return path == (FilesShownInTree ? c.lastAny : c.lastFolder);
    }

    static void DrawTreeLines(ProjectWindowSettings s, string path, int depth, Rect rect)
    {
        float indent = s.indentWidth;
        float midY = Mathf.Round(rect.y + rect.height * 0.5f);
        var col = s.lineColor;

        // Own branch: vertical under the parent's foldout, then across to this item.
        float x = Mathf.Round(rect.x - indent * 1.5f);
        bool last = IsLast(path);
        EditorGUI.DrawRect(new Rect(x, rect.y, 1f, (last ? midY : rect.yMax) - rect.y), col);

        bool hasArrow = false;
        if (AssetDatabase.IsValidFolder(path))
        {
            var c = GetChildren(path);
            hasArrow = c.hasFolders || (c.hasFiles && FilesShownInTree);
        }
        float xEnd = hasArrow ? rect.x - indent + 1f : rect.x - 2f;
        if (xEnd > x + 1f) EditorGUI.DrawRect(new Rect(x + 1f, midY, xEnd - x - 1f, 1f), col);

        // Ancestors: continue their verticals while they still have siblings below.
        var ancestor = path;
        for (int k = 2; k <= depth; k++)
        {
            ancestor = ancestor.Substring(0, ancestor.LastIndexOf('/'));
            if (IsLast(ancestor)) continue;
            float ax = Mathf.Round(rect.x - indent * (k + 0.5f));
            EditorGUI.DrawRect(new Rect(ax, rect.y, 1f, rect.height), col);
        }
    }
}

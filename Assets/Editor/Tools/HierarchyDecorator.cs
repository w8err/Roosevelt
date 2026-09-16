using System.Collections.Generic;
using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// vHierarchy-style row decorations for the Unity 6 UI Toolkit Hierarchy window, built on its public
// HierarchyWindow.BindViewItem event (the IMGUI hierarchyWindowItemOnGUI callback is not raised by it):
// zebra stripes, name-prefix row tints, tree lines, component icons and an active toggle on the right.
// Settings: HierarchySettings. Menus: Roosevelt > Tools > Hierarchy.
[InitializeOnLoad]
public static class HierarchyDecorator
{
    const string OverlayName = "roosevelt-row-overlay";
    const string RightName = "roosevelt-row-right";
    const float IconSize = 16f;
    const float FallbackIndent = 15f;

    static HierarchySettings settings;
    static readonly List<Component> components = new();
    // Arrow x (row space) of a scene-root GameObject, learned while binding; lets tree lines use the real indent.
    static float rootArrowX = -1f;

    static HierarchyDecorator()
    {
        HierarchyWindow.BindViewItem += OnBindItem;
        HierarchyWindow.UnbindViewItem += OnUnbindItem;
        EditorApplication.delayCall += () => Load();
    }

    [MenuItem("Roosevelt/Tools/Hierarchy/Settings")]
    static void SelectSettings() => Selection.activeObject = Load();

    [MenuItem("Roosevelt/Tools/Hierarchy/Toggle")]
    static void Toggle()
    {
        var s = Load();
        s.enabled = !s.enabled;
        EditorUtility.SetDirty(s);
        AssetDatabase.SaveAssetIfDirty(s);
        Refresh();
    }

    // Rebinding every visible row is the simplest way to apply new settings.
    public static void Refresh() => EditorApplication.RepaintHierarchyWindow();

    static HierarchySettings Load()
    {
        if (settings != null) return settings;
        settings = AssetDatabase.LoadAssetAtPath<HierarchySettings>(HierarchySettings.AssetPath);
        if (settings != null) return settings;
        if (EditorApplication.isUpdating || EditorApplication.isCompiling) return null;

        LabSceneBuilder.EnsureFolder("Assets/Settings/EditorTools");
        settings = ScriptableObject.CreateInstance<HierarchySettings>();
        settings.nameRules = HierarchySettings.DefaultRules();
        AssetDatabase.CreateAsset(settings, HierarchySettings.AssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[HierarchyDecorator] created {HierarchySettings.AssetPath}");
        return settings;
    }

    static void OnUnbindItem(HierarchyWindow window, HierarchyView view, HierarchyViewItem item) => Clear(item);

    // Items are recycled, so every bind starts from a clean row.
    static void Clear(HierarchyViewItem item)
    {
        item.RowContainer.Q(OverlayName)?.RemoveFromHierarchy();
        item.RightCustomContainer.Q(RightName)?.RemoveFromHierarchy();
    }

    static void OnBindItem(HierarchyWindow window, HierarchyView view, HierarchyViewItem item)
    {
        Clear(item);
        var s = settings != null ? settings : AssetDatabase.LoadAssetAtPath<HierarchySettings>(HierarchySettings.AssetPath);
        settings = s;
        if (s == null || !s.enabled) return;
        if (item.Handler is not HierarchyGameObjectHandler handler) return;
        var node = item.Node;
        var go = handler.GetGameObject(in node);
        if (go == null) return;

        var row = view.ViewModel.IndexOf(in node);
        var rule = Match(s, go.name);
        var overlay = new VisualElement { name = OverlayName, pickingMode = PickingMode.Ignore };
        overlay.style.position = Position.Absolute;
        overlay.style.left = overlay.style.right = overlay.style.top = overlay.style.bottom = 0;
        overlay.generateVisualContent += ctx => Paint(ctx, overlay, item, go, row, rule, s);
        item.RowContainer.Insert(0, overlay);
        // Arrow positions are only known after layout. The overlay is rebuilt on every bind, so hooking it (not
        // the recycled toggle) never piles up callbacks.
        overlay.RegisterCallback<GeometryChangedEvent>(_ => overlay.MarkDirtyRepaint());

        if (s.componentIcons || s.activeToggle)
            item.RightCustomContainer.Add(BuildRight(go, s));
    }

    static HierarchySettings.NameRule Match(HierarchySettings s, string name)
    {
        foreach (var rule in s.nameRules)
            if (!string.IsNullOrEmpty(rule.prefix) && name.StartsWith(rule.prefix))
                return rule;
        return null;
    }

    static void Paint(MeshGenerationContext ctx, VisualElement overlay, HierarchyViewItem item, GameObject go, int row,
        HierarchySettings.NameRule rule, HierarchySettings s)
    {
        if (go == null) return;
        var r = overlay.contentRect;
        var p = ctx.painter2D;
        if (s.zebraStripes && row % 2 == 1)
            Fill(p, r, new Color(0f, 0f, 0f, EditorGUIUtility.isProSkin ? 0.08f : 0.04f));
        if (rule != null)
        {
            var c = rule.color;
            Fill(p, r, new Color(c.r, c.g, c.b, 0.12f));
            Fill(p, new Rect(r.x, r.y, 2f, r.height), new Color(c.r, c.g, c.b, 0.9f));
        }
        if (s.treeLines) PaintTreeLines(p, overlay, item, go.transform, r);
    }

    static void Fill(Painter2D p, Rect r, Color color)
    {
        p.fillColor = color;
        p.BeginPath();
        p.MoveTo(new Vector2(r.xMin, r.yMin));
        p.LineTo(new Vector2(r.xMax, r.yMin));
        p.LineTo(new Vector2(r.xMax, r.yMax));
        p.LineTo(new Vector2(r.xMin, r.yMax));
        p.ClosePath();
        p.Fill();
    }

    // Each depth sits one indent right of its parent; a line runs down the parent's arrow column. For every
    // ancestor that still has siblings below it, its column continues through this row.
    static void PaintTreeLines(Painter2D p, VisualElement overlay, HierarchyViewItem item, Transform t, Rect r)
    {
        var toggle = item.Toggle.worldBound;
        if (float.IsNaN(toggle.x) || toggle.width <= 0f) return;
        var arrowX = toggle.center.x - overlay.worldBound.x;
        var depth = 0;
        for (var a = t.parent; a != null; a = a.parent) depth++;
        if (depth == 0)
        {
            rootArrowX = arrowX;
            return;
        }
        var indent = rootArrowX > 0f ? (arrowX - rootArrowX) / depth : FallbackIndent;
        if (indent <= 1f) indent = FallbackIndent;

        p.strokeColor = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.18f) : new Color(0f, 0f, 0f, 0.22f);
        p.lineWidth = 1f;
        var mid = r.center.y;
        var column = arrowX - indent;
        var last = t.GetSiblingIndex() == t.parent.childCount - 1;
        Line(p, column, r.yMin, column, last ? mid : r.yMax);
        Line(p, column, mid, t.childCount > 0 ? arrowX - toggle.width * 0.5f : arrowX + 2f, mid);

        var ancestorColumn = column - indent;
        for (var a = t.parent; a.parent != null; a = a.parent, ancestorColumn -= indent)
            if (a.GetSiblingIndex() < a.parent.childCount - 1)
                Line(p, ancestorColumn, r.yMin, ancestorColumn, r.yMax);
    }

    static void Line(Painter2D p, float x0, float y0, float x1, float y1)
    {
        p.BeginPath();
        p.MoveTo(new Vector2(Mathf.Round(x0) + 0.5f, y0));
        p.LineTo(new Vector2(Mathf.Round(x1) + 0.5f, y1));
        p.Stroke();
    }

    // Component icons (newest nearest the toggle, Transform skipped, dimmed when disabled, warning icon for a
    // missing script) followed by the active toggle.
    static VisualElement BuildRight(GameObject go, HierarchySettings s)
    {
        var right = new VisualElement { name = RightName };
        right.style.flexDirection = FlexDirection.Row;
        right.style.alignItems = Align.Center;

        if (s.componentIcons)
        {
            components.Clear();
            go.GetComponents(components);
            var icons = new List<VisualElement>();
            for (var i = components.Count - 1; i >= 0 && icons.Count < s.maxComponentIcons; i--)
            {
                var c = components[i];
                if (c is Transform) continue;
                Texture tex;
                var enabled = true;
                if (c == null)
                    tex = EditorGUIUtility.IconContent("console.warnicon.sml").image;
                else
                {
                    tex = AssetPreview.GetMiniThumbnail(c);
                    enabled = c switch
                    {
                        Behaviour b => b.enabled,
                        Renderer ren => ren.enabled,
                        Collider col => col.enabled,
                        _ => true,
                    };
                }
                if (tex == null) continue;
                var image = new Image { image = tex, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore,
                                        tooltip = c == null ? "Missing script" : c.GetType().Name };
                image.style.width = image.style.height = IconSize;
                image.style.opacity = enabled && go.activeInHierarchy ? 1f : 0.35f;
                icons.Add(image);
            }
            for (var i = icons.Count - 1; i >= 0; i--) right.Add(icons[i]);
        }

        if (s.activeToggle)
        {
            var toggle = new Toggle { value = go.activeSelf, tooltip = "Active" };
            toggle.style.marginLeft = 2f;
            toggle.style.marginRight = 2f;
            toggle.RegisterValueChangedCallback(e =>
            {
                if (go == null) return;
                Undo.RecordObject(go, e.newValue ? "Activate" : "Deactivate");
                go.SetActive(e.newValue);
                EditorUtility.SetDirty(go);
            });
            // Keep row selection from reacting to the click.
            toggle.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            right.Add(toggle);
        }
        return right;
    }
}

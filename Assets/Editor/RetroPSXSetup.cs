using System.IO;
using System.Linq;
using RetroPSX;
using RetroPSX.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Wires the embedded RetroPSX-URP package (Packages/com.natteens.retropsxurp, MIT) into both URP renderers
// and captures before/after renders from the player camera. The look is screen-space only for now: low
// internal resolution + RGB555 final-image quantization with PSX dither. Vertex snapping and affine
// textures need the RetroPSX/Lit shader on materials and are not applied here.
//
// Menus: Roosevelt > Rendering > Setup RetroPSX / Capture RetroPSX Comparison.
// Request files (polled once a second, like LabSceneBuilder): Temp/RetroPSXSetup.request,
// Temp/RetroPSXCapture.request (one scene name per line; empty = Reality and Dream_00).
[InitializeOnLoad]
public static class RetroPSXSetup
{
    const string SetupRequestPath = "Temp/RetroPSXSetup.request";
    const string CaptureRequestPath = "Temp/RetroPSXCapture.request";
    const string ProfileDir = "Assets/Settings/RetroPSX";
    const string Prefix = "Roosevelt";
    static readonly string[] RendererPaths = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" };
    static readonly string[] DefaultCaptureScenes = { "Reality", "Dream_00" };

    static double nextPoll;

    static RetroPSXSetup()
    {
        EditorApplication.update += PollRequest;
    }

    static void PollRequest()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1.0;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(SetupRequestPath))
        {
            File.Delete(SetupRequestPath);
            Setup();
            return;
        }
        if (File.Exists(CaptureRequestPath))
        {
            var scenes = File.ReadAllLines(CaptureRequestPath).Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
            File.Delete(CaptureRequestPath);
            Capture(scenes.Length > 0 ? scenes : DefaultCaptureScenes);
        }
    }

    [MenuItem("Roosevelt/Rendering/Setup RetroPSX")]
    public static void Setup()
    {
        var root = CreateProfiles();
        foreach (var path in RendererPaths)
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
            if (data == null)
            {
                Debug.LogWarning($"[RetroPSXSetup] renderer not found: {path}");
                continue;
            }
            AddFeature(data, root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[RetroPSXSetup] done: profile {ProfileDir}/{Prefix} Pipeline.asset on {RendererPaths.Length} renderers");
    }

    static RetroPSXPipelineProfile CreateProfiles()
    {
        LabSceneBuilder.EnsureFolder(ProfileDir);
        var root = GetOrCreate<RetroPSXPipelineProfile>("Pipeline");
        var raster = GetOrCreate<RetroRasterProfile>("Raster");
        var geometry = GetOrCreate<RetroGeometryProfile>("Geometry");
        var color = GetOrCreate<RetroColorProfile>("Color");
        var lighting = GetOrCreate<RetroLightingProfile>("Lighting");
        var fog = GetOrCreate<RetroFogProfile>("Fog");
        var volumetrics = GetOrCreate<RetroVolumetricProfile>("Volumetrics");
        var display = GetOrCreate<RetroDisplayProfile>("Display");
        var debug = GetOrCreate<RetroDebugProfile>("Debug");
        var ui = GetOrCreate<RetroUIProfile>("UI");

        Set(root, so =>
        {
            so.FindProperty("enabled").boolValue = true;
            so.FindProperty("raster").objectReferenceValue = raster;
            so.FindProperty("geometry").objectReferenceValue = geometry;
            so.FindProperty("color").objectReferenceValue = color;
            so.FindProperty("lighting").objectReferenceValue = lighting;
            so.FindProperty("fog").objectReferenceValue = fog;
            so.FindProperty("volumetrics").objectReferenceValue = volumetrics;
            so.FindProperty("display").objectReferenceValue = display;
            so.FindProperty("debug").objectReferenceValue = debug;
            so.FindProperty("ui").objectReferenceValue = ui;
        });
        // 240 lines tall, width follows the screen aspect (graphic reference: Fatum Betula at 320x240).
        Set(raster, so =>
        {
            so.FindProperty("mode").enumValueIndex = (int)RetroRasterMode.InternalHeight;
            so.FindProperty("internalHeight").intValue = 240;
            so.FindProperty("presentation").enumValueIndex = (int)RetroPresentationMode.Stretch;
        });
        // Ordinary URP materials only get final-image processing, so quantize there.
        Set(color, so =>
        {
            so.FindProperty("mode").enumValueIndex = (int)RetroColorMode.RGB555;
            so.FindProperty("quantizeFinalImage").boolValue = true;
            so.FindProperty("finalImageDither").enumValueIndex = (int)RetroDitherMode.PSX;
            so.FindProperty("finalImageDitherStrength").floatValue = 0.5f;
        });
        // LabSceneBuilder already sets RenderSettings fog per layout; a second fog would double it.
        Set(fog, so => so.FindProperty("mode").enumValueIndex = (int)RetroFogMode.Off);
        Set(display, so => so.FindProperty("enabled").boolValue = false);
        return root;
    }

    static T GetOrCreate<T>(string suffix) where T : ScriptableObject
    {
        var path = $"{ProfileDir}/{Prefix} {suffix}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static void Set(Object target, System.Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    static void AddFeature(ScriptableRendererData data, RetroPSXPipelineProfile profile)
    {
        var feature = data.rendererFeatures.OfType<RetroPSXRendererFeature>().FirstOrDefault();
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<RetroPSXRendererFeature>();
            feature.name = "RetroPSX";
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        Set(feature, so => so.FindProperty("profile").objectReferenceValue = profile);
        data.SetDirty();
        EditorUtility.SetDirty(data);
    }

    [MenuItem("Roosevelt/Rendering/Capture RetroPSX Comparison")]
    public static void CaptureFromMenu() => Capture(DefaultCaptureScenes);

    // Renders each scene's MainCamera with the profile off and on to Logs/PSX_<scene>_off.png / _on.png.
    // Never saves scenes; restores the scene that was open.
    static void Capture(string[] sceneNames)
    {
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                Debug.LogWarning("[RetroPSXSetup] capture skipped: save the open scene first");
                return;
            }
        }
        var profile = AssetDatabase.LoadAssetAtPath<RetroPSXPipelineProfile>($"{ProfileDir}/{Prefix} Pipeline.asset");
        if (profile == null)
        {
            Debug.LogWarning("[RetroPSXSetup] capture skipped: run Setup RetroPSX first");
            return;
        }
        var previous = SceneManager.GetActiveScene().path;
        var wasEnabled = new SerializedObject(profile).FindProperty("enabled").boolValue;
        try
        {
            foreach (var name in sceneNames)
            {
                var scenePath = $"Assets/Scenes/{name}.unity";
                if (!File.Exists(scenePath))
                {
                    Debug.LogWarning($"[RetroPSXSetup] no scene {scenePath}");
                    continue;
                }
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var cam = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude)
                    .FirstOrDefault(c => c.CompareTag("MainCamera"));
                if (cam == null)
                {
                    Debug.LogWarning($"[RetroPSXSetup] {name}: no MainCamera");
                    continue;
                }
                foreach (var on in new[] { false, true })
                {
                    Set(profile, so => so.FindProperty("enabled").boolValue = on);
                    var path = Render(cam, $"PSX_{name}_{(on ? "on" : "off")}");
                    Debug.Log($"[RetroPSXSetup] saved {path}");
                }
            }
        }
        finally
        {
            Set(profile, so => so.FindProperty("enabled").boolValue = wasEnabled);
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(previous) && File.Exists(previous))
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        }
    }

    static string Render(Camera cam, string name)
    {
        const int w = 1280, h = 720;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        var request = new RenderPipeline.StandardRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(cam, request))
            RenderPipeline.SubmitRenderRequest(cam, request);
        else
        {
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
        }
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        var dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name + ".png");
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
        return path;
    }
}

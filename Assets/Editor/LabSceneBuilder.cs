using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class LabSceneBuilder
{
    const string Root = "Assets/Art/Environment/Lab";
    const string MeshDir = Root + "/Meshes";
    const string PrefabDir = Root + "/Prefabs";
    const string MaterialPath = Root + "/Materials/M_Lab_Atlas.mat";
    const string AtlasPath = Root + "/Textures/T_Lab_Atlas.png";
    const string LayoutPath = Root + "/Layouts/Lab_TestRoom.json";
    const string ScenePath = "Assets/Scenes/Lab_TestRoom.unity";

    [Serializable] class Layout { public Module[] modules; public LightDef[] lights; public CameraDef camera; }
    [Serializable] class Module { public string mesh; public Vector3 pos; public float rotZ; }
    [Serializable] class LightDef { public Vector3 pos; public Color color; public float intensity; public float range; }
    [Serializable] class CameraDef { public Vector3 pos; public Vector3 target; public float fov; }

    // Layout is authored in Blender space; FBX export (Forward -Z, Up Y, Apply Transform) maps it this way.
    static Vector3 ToUnity(Vector3 b) => new Vector3(-b.x, b.z, -b.y);

    [MenuItem("Roosevelt/Lab/Build Test Room")]
    public static void BuildTestRoom()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var json = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
        if (json == null) { Debug.LogError($"[LabSceneBuilder] layout not found: {LayoutPath}"); return; }
        var layout = JsonUtility.FromJson<Layout>(json.text);

        var material = GetOrCreateMaterial();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var room = new GameObject("Lab_TestRoom").transform;
        var prefabs = new Dictionary<string, GameObject>();

        foreach (var m in layout.modules)
        {
            if (!prefabs.TryGetValue(m.mesh, out var prefab))
                prefabs[m.mesh] = prefab = CreatePrefab(m.mesh, material);
            if (prefab == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, room);
            go.transform.SetPositionAndRotation(ToUnity(m.pos), Quaternion.Euler(0f, -m.rotZ, 0f));
        }

        var lights = new GameObject("Lights").transform;
        foreach (var l in layout.lights)
        {
            var go = new GameObject("Light_Point");
            go.transform.SetParent(lights, false);
            go.transform.position = ToUnity(l.pos);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = l.color;
            light.intensity = l.intensity;
            light.range = l.range;
            light.shadows = LightShadows.Hard;
        }

        var cam = CreateCamera(layout.camera);
        SetupEnvironment();
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, ScenePath);

        var capture = Capture(cam);
        Debug.Log($"[LabSceneBuilder] built {layout.modules.Length} modules, scene {ScenePath}, capture {capture}");
    }

    static Material GetOrCreateMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            EnsureFolder(Path.GetDirectoryName(MaterialPath).Replace('\\', '/'));
            mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static GameObject CreatePrefab(string meshName, Material material)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{MeshDir}/{meshName}.fbx");
        if (model == null) { Debug.LogError($"[LabSceneBuilder] mesh not found: {meshName}"); return null; }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        var bounds = new Bounds();
        var first = true;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
        {
            r.sharedMaterial = material;
            if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);
        }
        if (meshName.Contains("_Door_"))
        {
            // A single box would seal the doorway.
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }
        else
        {
            var box = go.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
        }

        EnsureFolder(PrefabDir);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{meshName}.prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    static Camera CreateCamera(CameraDef def)
    {
        var go = new GameObject("Camera_Review") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>();
        go.transform.position = ToUnity(def.pos);
        go.transform.LookAt(ToUnity(def.target));
        cam.fieldOfView = def.fov;
        cam.nearClipPlane = 0.05f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        return cam;
    }

    static void SetupEnvironment()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.06f, 0.08f, 0.08f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.02f, 0.03f, 0.03f);
        RenderSettings.fogDensity = 0.06f;
    }

    static string Capture(Camera cam)
    {
        const int w = 960, h = 540;
        var rt = new RenderTexture(w, h, 24);
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
        var path = Path.Combine(dir, "Lab_TestRoom.png");
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        return path;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}

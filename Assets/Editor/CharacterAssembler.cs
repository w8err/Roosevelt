using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Assembles modular example characters: skinned parts share the base skeleton (remapped by bone name),
// static accessories hang off the head bone.
[InitializeOnLoad]
public static class CharacterAssembler
{
    const string SourceDir = "Assets/Art/Props/Example Character";
    const string PalettePath = SourceDir + "/Textures/T_Char_Example_Palette.png";
    const string MaterialPath = SourceDir + "/Materials/M_Char_Example_Palette.mat";
    const string PrefabDir = "Assets/Prefabs/Characters";
    const string ScenePath = "Assets/Scenes/Character_Test.unity";
    // Creating this file (e.g. from an external tool) requests an assembly without opening the menu.
    const string RequestPath = "Temp/CharacterAssembler.request";
    const string DefaultBaseModel = "PolyMate_MaleV1";
    const string HeadBone = "head.x";

    // Accessories are authored around their own origin. Offsets from the head joint (bind pose, character
    // facing +Z) were fitted in Blender: cap crown just above the scalp, glasses bridge in front of the eyes.
    static readonly Dictionary<string, Vector3> AccessoryOffsets = new Dictionary<string, Vector3>
    {
        { "CapV1", new Vector3(0f, 0.1008f, 0.06f) },
        { "GlassesV1", new Vector3(0f, 0.0649f, 0.147f) },
        { "SunglassesV1", new Vector3(0f, 0.0649f, 0.147f) },
    };

    class Outfit
    {
        public string Name;
        public string BaseModel = DefaultBaseModel;
        public string[] Remove = new string[0];     // skinned parts of the base to drop
        public string[] Add = new string[0];        // part files whose skinned meshes join the base skeleton
        public string[] Accessories = new string[0];
    }

    static readonly Outfit[] Outfits =
    {
        new Outfit { Name = "PF_Char_Example_CapGlasses", Accessories = new[] { "CapV1", "GlassesV1" } },
        new Outfit
        {
            Name = "PF_Char_Example_HoodSunglasses",
            Remove = new[] { "Torso_HoodieV1_Down" },
            Add = new[] { "Torso_HoodieV1_Up" },
            Accessories = new[] { "SunglassesV1" },
        },
        new Outfit
        {
            Name = "PF_Char_Researcher_CapGlasses",
            BaseModel = "PolyMate_ResearcherV1",
            Accessories = new[] { "CapV1", "GlassesV1" },
        },
        new Outfit
        {
            Name = "PF_Char_Researcher_Sunglasses",
            BaseModel = "PolyMate_ResearcherV1",
            Accessories = new[] { "SunglassesV1" },
        },
    };

    static double nextPoll;

    static CharacterAssembler()
    {
        EditorApplication.update += PollRequest;
    }

    [MenuItem("Roosevelt/Characters/Assemble Examples")]
    public static void AssembleFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[CharacterAssembler] cancelled: the open scene was not saved");
            return;
        }
        AssembleAll();
    }

    static void PollRequest()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1.0;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists("Temp/ResearcherReview.request"))
        {
            File.Delete("Temp/ResearcherReview.request");
            RebuildResearchers();
            return;
        }
        if (!File.Exists(RequestPath)) return;

        File.Delete(RequestPath);
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                Debug.LogWarning("[CharacterAssembler] request skipped: save the open scene first");
                return;
            }
        }
        AssetDatabase.Refresh();
        AssembleAll();
    }

    [MenuItem("Roosevelt/Characters/Rebuild Researchers")]
    public static void RebuildResearchers()
    {
        AssetDatabase.ImportAsset(SourceDir + "/PolyMate_ResearcherV1.fbx", ImportAssetOptions.ForceSynchronousImport);
        var previous = SceneManager.GetActiveScene();
        var review = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(review);
        try
        {
            var palette = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var researchers = Outfits.Where(o => o.BaseModel == "PolyMate_ResearcherV1").ToArray();
            var instances = new List<GameObject>();
            for (int i = 0; i < researchers.Length; i++)
            {
                var prefab = Assemble(researchers[i], palette);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, review);
                instance.transform.position = new Vector3((i - .5f) * 1.45f, 0, 0);
                foreach (var t in instance.GetComponentsInChildren<Transform>()) t.gameObject.layer = 31;
                instances.Add(instance);
                int tris = instance.GetComponentsInChildren<SkinnedMeshRenderer>().Sum(r => r.sharedMesh.triangles.Length / 3)
                    + instance.GetComponentsInChildren<MeshFilter>().Sum(r => r.sharedMesh.triangles.Length / 3);
                Debug.Log($"[ResearcherReview] {prefab.name}: {tris} triangles");
            }
            var sun = new GameObject("ResearcherReviewLight").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.4f; sun.cullingMask = 1 << 31;
            sun.transform.rotation = Quaternion.Euler(35, 155, 0);
            var cam = new GameObject("ResearcherReviewCamera").AddComponent<Camera>();
            cam.cullingMask = 1 << 31; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.21f, .23f, .24f); cam.nearClipPlane = .05f;
            Aim(cam, new Vector3(0, 1.15f, 4.8f), new Vector3(0, .95f, 0), 34);
            LabSceneBuilder.Capture(cam, "Researcher_v2_Unity");
            for (int i = 0; i < instances.Count; i++)
            {
                var p = instances[i].transform.position;
                Aim(cam, p + new Vector3(.9f, 1.15f, 2.9f), p + new Vector3(0, .95f, 0), 40);
                LabSceneBuilder.Capture(cam, "Researcher_v2_Unity_" + i);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[ResearcherReview] COMPLETE: two prefabs rebuilt and captured");
        }
        finally
        {
            EditorSceneManager.CloseScene(review, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
    }

    static void AssembleAll()
    {
        Debug.Log("[CharacterAssembler] build started");
        var palette = GetOrCreatePaletteMaterial();
        var prefabs = Outfits.Select(o => Assemble(o, palette)).ToArray();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        var xs = new float[prefabs.Length];
        for (var i = 0; i < prefabs.Length; i++)
        {
            xs[i] = (i - (prefabs.Length - 1) / 2f) * 1.4f;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i]);
            go.transform.position = new Vector3(xs[i], 0f, 0f);
        }

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.2f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.45f);

        var cam = new GameObject("Camera_Review") { tag = "MainCamera" }.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.5f, 0.52f, 0.5f);
        cam.nearClipPlane = 0.05f;

        var captures = new List<string>();
        for (var i = 0; i < xs.Length; i++)
        {
            Aim(cam, new Vector3(xs[i], 1.72f, 1.1f), new Vector3(xs[i], 1.70f, 0f), 30f);
            captures.Add(LabSceneBuilder.Capture(cam, $"Character_Test_Head_{i}"));
        }
        Aim(cam, new Vector3(0f, 1.1f, 4.2f), new Vector3(0f, 0.95f, 0f), 40f);
        captures.Insert(0, LabSceneBuilder.Capture(cam, "Character_Test"));
        EditorSceneManager.SaveScene(scene, ScenePath);

        Debug.Log($"[CharacterAssembler] built {prefabs.Length} characters, scene {ScenePath}, capture {string.Join(" ", captures)}");
    }

    static void Aim(Camera cam, Vector3 from, Vector3 to, float fov)
    {
        cam.transform.position = from;
        cam.transform.LookAt(to);
        cam.fieldOfView = fov;
    }

    // The FBX files point at the author's local texture, so every part shares a palette extracted from the base FBX.
    static Material GetOrCreatePaletteMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            LabSceneBuilder.EnsureFolder(Path.GetDirectoryName(MaterialPath).Replace('\\', '/'));
            mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath);
        if (palette == null) Debug.LogError($"[CharacterAssembler] palette not found: {PalettePath}");
        mat.mainTexture = palette;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static GameObject Load(string file)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{SourceDir}/{file}.fbx");
        if (asset == null) throw new FileNotFoundException($"[CharacterAssembler] model not found: {SourceDir}/{file}.fbx");
        return asset;
    }

    static GameObject Assemble(Outfit outfit, Material palette)
    {
        var character = (GameObject)PrefabUtility.InstantiatePrefab(Load(outfit.BaseModel));
        PrefabUtility.UnpackPrefabInstance(character, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        character.name = outfit.Name;
        character.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        var bones = character.GetComponentsInChildren<Transform>(true)
            .GroupBy(t => t.name)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var part in outfit.Remove)
            foreach (var smr in character.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.StartsWith(part)).ToArray())
                Object.DestroyImmediate(smr.gameObject);

        foreach (var file in outfit.Add)
        {
            // A plain copy of the part file: its skinned meshes move over, its own skeleton is discarded.
            var copy = Object.Instantiate(Load(file));
            foreach (var smr in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.bones = smr.bones.Select(b => bones[b.name]).ToArray();
                smr.rootBone = bones[smr.rootBone.name];
                smr.transform.SetParent(character.transform, false);
            }
            Object.DestroyImmediate(copy);
        }

        var head = bones[HeadBone];
        foreach (var file in outfit.Accessories)
        {
            var accessory = Object.Instantiate(Load(file));
            accessory.name = file;
            accessory.transform.position = head.position + AccessoryOffsets[file];
            accessory.transform.SetParent(head, true);
        }

        foreach (var r in character.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = palette;

        LabSceneBuilder.EnsureFolder(PrefabDir);
        var prefab = PrefabUtility.SaveAsPrefabAsset(character, $"{PrefabDir}/{outfit.Name}.prefab");
        Object.DestroyImmediate(character);
        return prefab;
    }
}

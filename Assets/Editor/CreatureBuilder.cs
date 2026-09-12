using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Builds the Stalk creature prefabs from the skinned FBX files of Art/tools/make_stalk.py, a test scene to walk
// among them, and review captures in Logs/: the rest pose, and a few seconds of walking simulated in edit mode
// (StalkWalker.Tick) so the gait can be checked without entering play mode. A second review lets the creatures
// placed by Forest_Test.json roam the real forest for a while and photographs them from player height.
[InitializeOnLoad]
public static class CreatureBuilder
{
    const string KitDir = "Assets/Art/Creatures/Stalk";
    const string ModelPrefix = "SK_Creature_Stalk_";
    const string AtlasPath = KitDir + "/Textures/T_Creature_Stalk_Atlas.png";
    const string EmissionPath = KitDir + "/Textures/T_Creature_Stalk_Emission.png";
    const string MaterialPath = KitDir + "/Materials/M_Creature_Stalk.mat";
    const string GroundMaterialPath = KitDir + "/Materials/M_Creature_Test_Ground.mat";
    public const string PrefabDir = "Assets/Prefabs/Creatures";
    const string ScenePath = "Assets/Scenes/Creature_Stalk_Test.unity";
    const string ForestScenePath = "Assets/Scenes/Forest_Test.unity";
    const string PlayerPrefabPath = "Assets/Prefabs/Player/PF_Player.prefab";
    // Creating these files (e.g. from an external tool) requests a build or a forest review without the menu.
    const string RequestPath = "Temp/CreatureBuilder.request";
    const string ForestRequestPath = "Temp/CreatureForestReview.request";

    // Tall legs step slowly and high, and the tripod turns slowly because it lifts one foot at a time
    // (a fast turn leaves the rear foot behind and the body sinks to reach it). C trots with two feet up.
    static readonly Dictionary<string, (float speed, float turn, float stepTime, float stepHeight, float trigger)> Gaits =
        new Dictionary<string, (float, float, float, float, float)>
        {
            { "A", (0.5f, 11f, 0.95f, 0.55f, 0.5f) },
            { "B", (0.55f, 14f, 1.1f, 0.6f, 0.6f) },
            { "C", (0.6f, 22f, 0.75f, 0.4f, 0.45f) },
        };

    // Test scene spots (Unity space): far enough from the player start that they roam before they notice you.
    static readonly Dictionary<string, Vector3> TestSpots = new Dictionary<string, Vector3>
    {
        { "A", new Vector3(-9f, 0f, 34f) }, { "B", new Vector3(0f, 0f, 40f) }, { "C", new Vector3(9f, 0f, 32f) },
    };

    static double nextPoll;

    static CreatureBuilder()
    {
        EditorApplication.update += PollRequest;
    }

    [MenuItem("Roosevelt/Creatures/Build Stalks")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[CreatureBuilder] cancelled: the open scene was not saved");
            return;
        }
        Build();
    }

    [MenuItem("Roosevelt/Creatures/Review In Forest")]
    public static void ReviewForestFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ReviewForest();
    }

    static void PollRequest()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1.0;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var build = File.Exists(RequestPath);
        var review = File.Exists(ForestRequestPath);
        if (!build && !review) return;

        File.Delete(build ? RequestPath : ForestRequestPath);
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                Debug.LogWarning("[CreatureBuilder] request skipped: save the open scene first");
                return;
            }
        }
        AssetDatabase.Refresh();
        if (build) Build();
        else ReviewForest();
    }

    static void Build()
    {
        Debug.Log("[CreatureBuilder] build started");
        var models = AssetDatabase.FindAssets(ModelPrefix + " t:Model", new[] { KitDir + "/Meshes" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
        if (models.Length == 0)
        {
            Debug.LogError($"[CreatureBuilder] no {ModelPrefix}*.fbx in {KitDir}/Meshes (run Art/tools/make_stalk.py)");
            return;
        }
        var material = GetOrCreateMaterial(MaterialPath);
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        if (material.mainTexture == null) Debug.LogError($"[CreatureBuilder] atlas not found: {AtlasPath}");
        // The eye: an emission mask on the atlas layout. StalkWalker sets _EmissionColor per creature.
        var glow = AssetDatabase.LoadAssetAtPath<Texture2D>(EmissionPath);
        if (glow == null) Debug.LogError($"[CreatureBuilder] emission mask not found: {EmissionPath}");
        material.SetTexture("_EmissionMap", glow);
        material.SetColor("_EmissionColor", Color.white * 0.5f);
        // The string EnableKeyword did not stick on Simple Lit; the LocalKeyword form does. Real-time emissive:
        // the glow changes at run time, nothing is baked.
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        material.SetKeyword(new LocalKeyword(material.shader, "_EMISSION"), true);
        EditorUtility.SetDirty(material);
        Debug.Log($"[CreatureBuilder] {material.name}: emission keyword {(material.IsKeywordEnabled("_EMISSION") ? "on" : "OFF")}");
        var prefabs = models.Select(p => BuildPrefab(p, material)).Where(p => p != null).ToArray();
        AssetDatabase.SaveAssets();
        BuildTestScene(prefabs);
    }

    static GameObject BuildPrefab(string modelPath, Material material)
    {
        // A Generic rig keeps the skin (with no rig Unity imports a plain mesh). No avatar, clips or imported
        // material: the walker poses the bones and the atlas material replaces the slot.
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        if (importer.animationType != ModelImporterAnimationType.Generic || importer.avatarSetup != ModelImporterAvatarSetup.NoAvatar
            || importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.SaveAndReimport();
        }

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var key = model.name.Substring(ModelPrefix.Length);
        // Unpacked like CharacterAssembler: the prefab is rebuilt after every export anyway.
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "PF_Creature_Stalk_" + key;
        foreach (var animator in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);

        var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
        if (skin == null)
        {
            Debug.LogError($"[CreatureBuilder] {modelPath}: no skinned mesh");
            Object.DestroyImmediate(go);
            return null;
        }
        skin.sharedMaterial = material;
        // The legs reach far from the hip bone and the body walks away from its bind pose: keep culling generous.
        var bounds = skin.localBounds;
        bounds.Expand(4f);
        skin.localBounds = bounds;

        var walker = go.AddComponent<StalkWalker>();
        var so = new SerializedObject(walker);
        var gait = Gaits.TryGetValue(key, out var g) ? g : Gaits["A"];
        so.FindProperty("speed").floatValue = gait.speed;
        so.FindProperty("turnSpeed").floatValue = gait.turn;
        so.FindProperty("stepTime").floatValue = gait.stepTime;
        so.FindProperty("stepHeight").floatValue = gait.stepHeight;
        so.FindProperty("stepTrigger").floatValue = gait.trigger;
        so.FindProperty("seed").intValue = key[0] - 'A' + 1;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Thin shin colliders so the player bumps into the legs. Ignore Raycast keeps them out of the walker's
        // ground and obstacle queries (it skips its own colliders anyway).
        var shins = 0;
        foreach (var lower in go.GetComponentsInChildren<Transform>(true).Where(t => t.name.EndsWith("_Lower")).ToArray())
        {
            var foot = lower.Find(lower.name.Replace("_Lower", "_Foot"));
            if (foot == null) continue;
            var c = new GameObject("ShinCollider") { layer = 2 };
            c.transform.SetParent(lower, false);
            c.transform.SetPositionAndRotation(Vector3.Lerp(lower.position, foot.position, 0.5f),
                Quaternion.LookRotation(foot.position - lower.position));
            var capsule = c.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.radius = 0.09f;
            capsule.height = Vector3.Distance(lower.position, foot.position) + 0.1f;
            shins++;
        }

        var stats = $"{skin.sharedMesh.triangles.Length / 3} tris, {skin.bones.Length} bones, {shins} shin colliders";
        LabSceneBuilder.EnsureFolder(PrefabDir);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{go.name}.prefab");
        Object.DestroyImmediate(go);
        Debug.Log($"[CreatureBuilder] {prefab.name}: {stats}");
        return prefab;
    }

    static void BuildTestScene(GameObject[] prefabs)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var background = new Color(0.30f, 0.34f, 0.32f);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = Vector3.one * 16f;
        var groundMat = GetOrCreateMaterial(GroundMaterialPath);
        groundMat.color = new Color(0.16f, 0.19f, 0.16f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

        // forest light (make_forest_test.py SUN_DIR, fog pushed back so the review shots stay readable)
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(0.85f, 0.87f, 0.80f);
        sun.shadows = LightShadows.Hard;
        sun.transform.rotation = Quaternion.LookRotation(new Vector3(-0.3f, -0.74f, -0.6f));
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.20f, 0.20f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.36f, 0.40f, 0.37f);
        RenderSettings.fogStartDistance = 40f;
        RenderSettings.fogEndDistance = 110f;

        var creatures = new GameObject("Creatures").transform;
        foreach (var prefab in prefabs)
        {
            var key = prefab.name.Substring("PF_Creature_Stalk_".Length);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, creatures);
            go.transform.SetPositionAndRotation(TestSpots.TryGetValue(key, out var p) ? p : Vector3.forward * 30f, Quaternion.Euler(0f, 180f, 0f));
        }

        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (player != null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(player);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var c in go.GetComponentsInChildren<Camera>()) c.backgroundColor = background;
        }
        else Debug.LogWarning($"[CreatureBuilder] {PlayerPrefabPath} not found; build a lab layout once to create it");

        var cam = new GameObject("Camera_Review").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = background;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 300f;
        Aim(cam, new Vector3(0f, 3f, 12f), new Vector3(0f, 4.5f, 36f), 50f);
        cam.gameObject.SetActive(false);
        EditorSceneManager.SaveScene(scene, ScenePath);

        cam.gameObject.SetActive(true);
        var captures = new List<string> { LabSceneBuilder.Capture(cam, "Creature_Stalk_Rest") };

        // Walk review: A and B head toward the camera side, C stares at the camera. Six seconds at 30 fps.
        Physics.SyncTransforms();
        var walkers = creatures.GetComponentsInChildren<StalkWalker>();
        foreach (var w in walkers)
        {
            if (w.name.EndsWith("_C")) w.ReviewWatch(cam.transform);
            else w.ReviewWalkTo(w.transform.position + new Vector3(w.name.EndsWith("_A") ? 3f : -2f, 0f, -6f));
        }
        Simulate(walkers, 6f);
        captures.Add(LabSceneBuilder.Capture(cam, "Creature_Stalk_Walk"));
        foreach (var w in walkers)
        {
            var p = w.transform.position;
            Aim(cam, p + new Vector3(4.5f, 1.6f, -7.5f), p + new Vector3(0f, 3.2f, 0f), 60f);
            captures.Add(LabSceneBuilder.Capture(cam, "Creature_Stalk_Walk_" + w.name.Substring(w.name.Length - 1)));
            Debug.Log($"[CreatureBuilder] {w.name}: {w.ReviewStats()}");
        }

        // Drop the simulated pose: the saved scene keeps the rest pose.
        EditorSceneManager.OpenScene(ScenePath);
        Debug.Log($"[CreatureBuilder] built {prefabs.Length} creatures, scene {ScenePath}, capture {string.Join(" ", captures)}");
    }

    // Forty seconds of free roaming in Forest_Test (watching off), then each creature from player height.
    static void ReviewForest()
    {
        EditorSceneManager.OpenScene(ForestScenePath);
        var walkers = Object.FindObjectsByType<StalkWalker>(FindObjectsSortMode.None).OrderBy(w => w.name).ToArray();
        if (walkers.Length == 0)
        {
            Debug.LogWarning("[CreatureBuilder] forest review: no creatures in Forest_Test");
            return;
        }
        Physics.SyncTransforms();
        foreach (var w in walkers) w.ReviewRoam();
        Simulate(walkers, 40f);

        var cam = new GameObject("Camera_CreatureReview").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 300f;
        var captures = new List<string>();
        foreach (var w in walkers)
        {
            var p = w.transform.position;
            var eye = p + w.transform.forward * 11f + w.transform.right * 3f;
            eye.y = (Physics.Raycast(eye + Vector3.up * 50f, Vector3.down, out var hit, 100f) ? hit.point.y : p.y) + 1.6f;
            Aim(cam, eye, p + Vector3.up * 4.5f, 62f);
            captures.Add(LabSceneBuilder.Capture(cam, "Creature_Forest_" + w.name.Substring(w.name.Length - 1)));
            Debug.Log($"[CreatureBuilder] forest {w.name}: {w.ReviewStats()}");
        }
        EditorSceneManager.OpenScene(ForestScenePath);   // discard the simulated state
        Debug.Log($"[CreatureBuilder] forest review done, capture {string.Join(" ", captures)}");
    }

    static void Simulate(StalkWalker[] walkers, float seconds)
    {
        for (var f = 0; f < seconds * 30f; f++)
        {
            foreach (var w in walkers) w.Tick(1f / 30f);
            Physics.SyncTransforms();
        }
    }

    static void Aim(Camera cam, Vector3 from, Vector3 to, float fov)
    {
        cam.transform.position = from;
        cam.transform.LookAt(to);
        cam.fieldOfView = fov;
    }

    static Material GetOrCreateMaterial(string path)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            LabSceneBuilder.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }
}

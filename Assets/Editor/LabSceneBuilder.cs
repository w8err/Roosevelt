using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class LabSceneBuilder
{
    // Each kit keeps layouts in <kit root>/Layouts, meshes in Meshes/, atlas in Textures/, materials in Materials/.
    static readonly string[] LayoutDirs = { "Assets/Art/Environment/Lab/Layouts", "Assets/Art/Environment/Forest/Layouts" };
    const string DefaultMaterial = "M_Lab_Atlas";
    const string DefaultAtlas = "T_Lab_Atlas";
    const string DefaultPrefabDir = "Assets/Prefabs/Environment/Lab/Modules";
    const string PlayerPrefabPath = "Assets/Prefabs/Player/PF_Player.prefab";
    const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
    const string HandheldNoisePath = "Packages/com.unity.cinemachine/Presets/Noise/Handheld_normal_mild.asset";
    const string WalkNoisePath = "Assets/Settings/Cinemachine/Noise_WalkBob.asset";
    const string SceneDir = "Assets/Scenes";
    // Creating this file (e.g. from an external tool) requests a build without opening the menu.
    const string RequestPath = "Temp/LabSceneBuilder.request";
    const string ForestRequestPath = "Temp/ForestSkyboxReview.request";
    const string ForestSkyboxTexturePath = "Assets/Art/Environment/Forest/Textures/T_Forest_Skybox_Dusk_V1.png";
    const string ForestSkyboxMaterialPath = "Assets/Art/Environment/Forest/Materials/M_Forest_Skybox_Dusk_V1.mat";
    // Hinge in Unity leaf space: the leaf's hinge edge (Blender -X) on its front face (Blender -Y).
    static readonly Vector3 HingeOffset = new Vector3(0.49f, 0f, 0.025f);
    static readonly Vector3 EyeHeight = new Vector3(0f, 1.6f, 0f);

    // Collision by name tag. Ground and lumpy solids collide with their own mesh (a bounding box would seal a
    // doorway or wall off a whole tree crown), thin or see-through dressing gets nothing, everything else a box.
    static readonly string[] MeshColliderTags = { "_Wall_Door", "_Terrain_", "_Tree_", "_ChoirTotem_", "_Rock_", "_Log_", "_Stump_",
                                                  "_RootCluster_", "_ServiceDoor_", "_Horizon_" };
    static readonly string[] NoColliderTags = { "_DoorPart_", "_Shrub_", "_Grass_", "_Backdrop_", "_Path_", "_WarningLight_", "_CCTV_" };

    [Serializable] class Layout
    {
        public string scene;
        public string material;   // in <kit>/Materials, default M_Lab_Atlas
        public string atlas;      // in <kit>/Textures, default T_Lab_Atlas
        public string prefabDir;  // default Assets/Prefabs/Environment/Lab/Modules
        public MaterialDef[] materials; // FBX material slots by name; unlisted slots get `material`
        public Module[] modules;
        public DoorDef[] doors;
        public LightDef[] lights;
        public ViewDef camera;
        public ViewDef[] playerStarts;
        public GroundDef ground;
        public EnvDef environment;
        public CreatureDef[] creatures;
    }
    // Prefab name in CreatureBuilder.PrefabDir; walkers get the layout's terrain as their ground.
    [Serializable] class CreatureDef { public string prefab; public Vector3 pos; public float rotZ; public float scale; }
    [Serializable] class MaterialDef { public string name; public string atlas; public bool cutout; public string shader; } // shader empty = Simple Lit
    [Serializable] class Module { public string mesh; public Vector3 pos; public float rotZ; public float scale; } // scale 0 means 1
    [Serializable] class DoorDef { public string leaf; public string[] parts; public Vector3 pos; public float rotZ; public bool locked; }
    [Serializable] class LightDef { public Vector3 pos; public Color color; public float intensity; public float range; }
    [Serializable] class ViewDef { public Vector3 pos; public Vector3 target; public float fov; }
    [Serializable] class GroundDef { public float size; public Color color; }
    // Outdoor settings. An empty fogMode keeps the dark indoor defaults. sunDirection is in Blender space.
    [Serializable] class EnvDef
    {
        public string fogMode;
        public float fogStart, fogEnd, fogDensity;
        public Color fogColor, ambient, background;
        public Vector3 sunDirection;
        public Color sunColor;
        public float sunIntensity;
    }

    class Kit { public string MeshDir, PrefabDir; public Material Material; public Dictionary<string, Material> Materials; }

    static double nextPoll;

    static LabSceneBuilder()
    {
        EditorApplication.update += PollRequest;
    }

    // Layouts are authored in Blender space; FBX export (Forward -Z, Up Y, Apply Transform) maps it this way.
    static Vector3 ToUnity(Vector3 b) => new Vector3(-b.x, b.z, -b.y);
    static Quaternion Yaw(float rotZ) => Quaternion.Euler(0f, -rotZ, 0f);
    static bool IsOutdoor(EnvDef env) => env != null && !string.IsNullOrEmpty(env.fogMode);

    [MenuItem("Roosevelt/Lab/Build Layouts")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[LabSceneBuilder] cancelled: the open scene was not saved");
            return;
        }
        BuildAll();
    }

    [MenuItem("Roosevelt/Forest/Rebuild Forest Scene")]
    public static void BuildForestFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildForest();
    }

    static void PollRequest()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1.0;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(ForestRequestPath))
        {
            File.Delete(ForestRequestPath);
            BuildForest();
            return;
        }
        if (!File.Exists(RequestPath)) return;

        File.Delete(RequestPath);
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                Debug.LogWarning("[LabSceneBuilder] request skipped: save the open scene first");
                return;
            }
        }
        // Pick up files exported while the editor was in the background.
        AssetDatabase.Refresh();
        BuildAll();
    }

    static void BuildAll()
    {
        var dirs = LayoutDirs.Where(AssetDatabase.IsValidFolder).ToArray();
        var paths = dirs.Length == 0 ? Array.Empty<string>() : AssetDatabase.FindAssets("t:TextAsset", dirs)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".json"))
            .OrderBy(p => p)
            .ToArray();
        if (paths.Length == 0)
        {
            Debug.LogError($"[LabSceneBuilder] layout not found: no .json in {string.Join(", ", LayoutDirs)}");
            return;
        }

        var prefabs = new Dictionary<string, GameObject>();
        string reopen = null;
        var newest = DateTime.MinValue;
        foreach (var path in paths)
        {
            var scenePath = BuildLayout(path, prefabs);
            var written = File.GetLastWriteTimeUtc(path);
            if (written > newest) { newest = written; reopen = scenePath; }
        }
        // Leave the scene of the most recently exported layout open.
        EditorSceneManager.OpenScene(reopen);
    }

    static void BuildForest()
    {
        const string forestLayout = "Assets/Art/Environment/Forest/Layouts/Forest_Test.json";
        if (EditorSceneManager.sceneCount > 0 && SceneManager.GetActiveScene().isDirty)
        {
            Debug.LogWarning("[LabSceneBuilder] forest build skipped: save the open scene first");
            return;
        }
        AssetDatabase.Refresh();
        if (AssetDatabase.LoadAssetAtPath<TextAsset>(forestLayout) == null)
        {
            Debug.LogError($"[LabSceneBuilder] forest layout not found: {forestLayout}");
            return;
        }
        var path = BuildLayout(forestLayout, new Dictionary<string, GameObject>());
        EditorSceneManager.OpenScene(path);
        Debug.Log("[LabSceneBuilder] forest scene rebuilt with skybox");
    }

    static string BuildLayout(string path, Dictionary<string, GameObject> prefabs)
    {
        var layout = JsonUtility.FromJson<Layout>(AssetDatabase.LoadAssetAtPath<TextAsset>(path).text);
        var sceneName = string.IsNullOrEmpty(layout.scene) ? Path.GetFileNameWithoutExtension(path) : layout.scene;
        var scenePath = $"{SceneDir}/{sceneName}.unity";
        var kitRoot = Path.GetDirectoryName(Path.GetDirectoryName(path)).Replace('\\', '/');
        var kit = new Kit
        {
            MeshDir = kitRoot + "/Meshes",
            PrefabDir = string.IsNullOrEmpty(layout.prefabDir) ? DefaultPrefabDir : layout.prefabDir,
            Material = GetOrCreateMaterial(
                $"{kitRoot}/Materials/{(string.IsNullOrEmpty(layout.material) ? DefaultMaterial : layout.material)}.mat",
                $"{kitRoot}/Textures/{(string.IsNullOrEmpty(layout.atlas) ? DefaultAtlas : layout.atlas)}.png"),
            Materials = (layout.materials ?? Array.Empty<MaterialDef>()).ToDictionary(d => d.name, d =>
                GetOrCreateMaterial($"{kitRoot}/Materials/{d.name}.mat", $"{kitRoot}/Textures/{d.atlas}.png", d.cutout, d.shader)),
        };
        Debug.Log($"[LabSceneBuilder] build started: {sceneName}");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var room = new GameObject(sceneName).transform;
        var modules = layout.modules ?? Array.Empty<Module>();
        foreach (var m in modules)
        {
            var prefab = GetPrefab(m.mesh, kit, prefabs);
            if (prefab == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, room);
            go.transform.SetPositionAndRotation(ToUnity(m.pos), Yaw(m.rotZ));
            if (m.scale > 0f) go.transform.localScale = Vector3.one * m.scale;
        }

        var doors = layout.doors ?? Array.Empty<DoorDef>();
        var doorRoot = new GameObject("Doors").transform;
        foreach (var d in doors) BuildDoor(d, doorRoot, kit, prefabs);

        var lights = new GameObject("Lights").transform;
        foreach (var l in layout.lights ?? Array.Empty<LightDef>())
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

        var env = layout.environment;
        var background = IsOutdoor(env) ? env.background : Color.black;
        if (IsOutdoor(env) && env.sunIntensity > 0f)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(lights, false);
            sun.type = LightType.Directional;
            sun.color = env.sunColor;
            sun.intensity = env.sunIntensity;
            sun.shadows = LightShadows.Hard;
            sun.transform.rotation = Quaternion.LookRotation(ToUnity(env.sunDirection).normalized);
        }
        if (layout.ground != null && layout.ground.size > 0f)
            CreateGround(layout.ground, $"{kitRoot}/Materials/M_{sceneName}_Ground.mat");

        var creatures = layout.creatures ?? Array.Empty<CreatureDef>();
        if (creatures.Length > 0) SpawnCreatures(creatures, room);

        var starts = layout.playerStarts ?? Array.Empty<ViewDef>();
        var cam = CreateReviewCamera(layout.camera, background);
        if (starts.Length > 0) SpawnPlayer(starts[0], background);
        else cam.gameObject.tag = "MainCamera";

        SetupEnvironment(env);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, scenePath);

        var capture = Capture(cam, sceneName);
        // The review camera only exists for the capture; play mode uses the player's camera.
        if (starts.Length > 0)
        {
            cam.gameObject.SetActive(false);
            EditorSceneManager.SaveScene(scene, scenePath);
        }
        Debug.Log($"[LabSceneBuilder] built {sceneName}: {modules.Length} modules, {doors.Length} doors, capture {capture}");
        return scenePath;
    }

    // Walking creatures (CreatureBuilder prefabs). Their feet stand on the layout's terrain collider.
    static void SpawnCreatures(CreatureDef[] creatures, Transform room)
    {
        var ground = room.GetComponentsInChildren<Collider>().FirstOrDefault(c => c.name.Contains("_Terrain_"));
        var root = new GameObject("Creatures").transform;
        foreach (var c in creatures)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CreatureBuilder.PrefabDir}/{c.prefab}.prefab");
            if (prefab == null)
            {
                Debug.LogError($"[LabSceneBuilder] creature not found: {c.prefab} (Roosevelt > Creatures > Build Stalks)");
                continue;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.transform.SetPositionAndRotation(ToUnity(c.pos), Yaw(c.rotZ));
            if (c.scale > 0f) go.transform.localScale = Vector3.one * c.scale;
            var walker = go.GetComponent<StalkWalker>();
            if (walker == null || ground == null) continue;
            var so = new SerializedObject(walker);
            so.FindProperty("ground").objectReferenceValue = ground;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        Debug.Log($"[LabSceneBuilder] {creatures.Length} creatures, ground {(ground != null ? ground.name : "none")}");
    }

    static void BuildDoor(DoorDef d, Transform parent, Kit kit, Dictionary<string, GameObject> prefabs)
    {
        var root = new GameObject("Door_" + d.leaf.Replace("SM_Lab_DoorLeaf_", "")).transform;
        root.SetParent(parent, false);
        root.SetPositionAndRotation(ToUnity(d.pos), Yaw(d.rotZ));

        var hinge = new GameObject("Hinge").transform;
        hinge.SetParent(root, false);
        hinge.localPosition = HingeOffset;
        var door = hinge.gameObject.AddComponent<LabDoor>();
        var so = new SerializedObject(door);
        so.FindProperty("locked").boolValue = d.locked;
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (var mesh in new[] { d.leaf }.Concat(d.parts ?? Array.Empty<string>()))
        {
            var prefab = GetPrefab(mesh, kit, prefabs);
            if (prefab == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hinge);
            go.transform.localPosition = -HingeOffset;
            go.transform.localRotation = Quaternion.identity;
        }
    }

    static void CreateGround(GroundDef def, string materialPath)
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = Vector3.one * (def.size / 10f);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (mat == null)
        {
            EnsureFolder(Path.GetDirectoryName(materialPath).Replace('\\', '/'));
            mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            AssetDatabase.CreateAsset(mat, materialPath);
        }
        mat.color = def.color;
        EditorUtility.SetDirty(mat);
        ground.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void SpawnPlayer(ViewDef start, Color background)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(GetOrCreatePlayerPrefab());
        var pos = ToUnity(start.pos);
        var dir = ToUnity(start.target) - pos;
        dir.y = 0f;
        go.transform.SetPositionAndRotation(pos, dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir) : Quaternion.identity);
        // Scene-level override so outdoor layouts show their sky color instead of black.
        go.GetComponentInChildren<Camera>().backgroundColor = background;
    }

    // Built once; later edits to the prefab are kept. Older generated versions are upgraded in place (same GUID).
    static GameObject GetOrCreatePlayerPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (existing != null && existing.GetComponentInChildren<CinemachineCamera>(true) != null)
        {
            UpgradeToWalkNoise(existing);
            return existing;
        }

        var root = new GameObject("PF_Player");
        var body = root.AddComponent<CharacterController>();
        body.height = 1.7f;
        body.radius = 0.3f;
        body.center = new Vector3(0f, 0.85f, 0f);
        body.stepOffset = 0.3f;

        var target = new GameObject("CameraTarget").transform;
        target.SetParent(root.transform, false);
        target.localPosition = EyeHeight;

        var vcamGo = new GameObject("PlayerCamera");
        vcamGo.transform.SetParent(root.transform, false);
        vcamGo.transform.localPosition = EyeHeight;
        var vcam = vcamGo.AddComponent<CinemachineCamera>();
        vcam.Target.TrackingTarget = target;
        var lens = LensSettings.Default;
        lens.FieldOfView = 70f;
        lens.NearClipPlane = 0.05f;
        vcam.Lens = lens;
        vcamGo.AddComponent<CinemachineHardLockToTarget>();
        vcamGo.AddComponent<CinemachineRotateWithFollowTarget>();
        var noise = vcamGo.AddComponent<CinemachineBasicMultiChannelPerlin>();
        noise.NoiseProfile = GetOrCreateWalkNoise();
        // Receives impulses from scene events (door slams and the like).
        vcamGo.AddComponent<CinemachineImpulseListener>();

        var camGo = new GameObject("MainCamera") { tag = "MainCamera" };
        camGo.transform.SetParent(root.transform, false);
        camGo.transform.localPosition = EyeHeight;
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.AddComponent<CinemachineBrain>();
        camGo.AddComponent<AudioListener>();

        var controller = root.AddComponent<FirstPersonController>();
        var so = new SerializedObject(controller);
        so.FindProperty("actions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        so.FindProperty("cameraTarget").objectReferenceValue = target;
        so.ApplyModifiedPropertiesWithoutUndo();
        var feel = root.AddComponent<PlayerCameraFeel>();
        so = new SerializedObject(feel);
        so.FindProperty("noise").objectReferenceValue = noise;
        so.ApplyModifiedPropertiesWithoutUndo();
        root.AddComponent<StaminaBar>();
        root.AddComponent<PlayerInteraction>();

        EnsureFolder(Path.GetDirectoryName(PlayerPrefabPath).Replace('\\', '/'));
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    // The first Cinemachine rig used the handheld preset plus footstep impulses, which read as jolts.
    // Only that untouched setup is replaced, so a profile picked by hand is left alone.
    static void UpgradeToWalkNoise(GameObject prefab)
    {
        var perlin = prefab.GetComponentInChildren<CinemachineBasicMultiChannelPerlin>(true);
        if (perlin == null || AssetDatabase.GetAssetPath(perlin.NoiseProfile) != HandheldNoisePath) return;

        var contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        contents.GetComponentInChildren<CinemachineBasicMultiChannelPerlin>(true).NoiseProfile = GetOrCreateWalkNoise();
        var footsteps = contents.GetComponent<CinemachineImpulseSource>();
        if (footsteps != null) UnityEngine.Object.DestroyImmediate(footsteps);
        var so = new SerializedObject(contents.GetComponent<PlayerCameraFeel>());
        so.FindProperty("amplitude").vector2Value = new Vector2(0.4f, 1f);
        so.FindProperty("frequency").vector2Value = new Vector2(0.35f, 1f);
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
        PrefabUtility.UnloadPrefabContents(contents);
        Debug.Log("[LabSceneBuilder] player camera switched to walk bob noise");
    }

    static NoiseSettings GetOrCreateWalkNoise()
    {
        var noise = AssetDatabase.LoadAssetAtPath<NoiseSettings>(WalkNoisePath);
        if (noise != null) return noise;

        noise = ScriptableObject.CreateInstance<NoiseSettings>();
        var handheld = AssetDatabase.LoadAssetAtPath<NoiseSettings>(HandheldNoisePath);
        if (handheld != null) noise.OrientationNoise = (NoiseSettings.TransformNoiseParams[])handheld.OrientationNoise.Clone();
        noise.PositionNoise = new[]
        {
            new NoiseSettings.TransformNoiseParams
            {
                // Smooth side sway only. PlayerCameraFeel does the up-down bob so sprinting can deepen it on its own.
                X = new NoiseSettings.NoiseParams { Frequency = 1f, Amplitude = 0.012f, Constant = true },
            },
        };
        EnsureFolder(Path.GetDirectoryName(WalkNoisePath).Replace('\\', '/'));
        AssetDatabase.CreateAsset(noise, WalkNoisePath);
        return noise;
    }

    static Material GetOrCreateMaterial(string materialPath, string atlasPath, bool cutout = false, string shaderName = null)
    {
        var custom = !string.IsNullOrEmpty(shaderName);
        var shader = Shader.Find(custom ? shaderName : "Universal Render Pipeline/Simple Lit");
        if (shader == null) Debug.LogError($"[LabSceneBuilder] shader not found: {shaderName}");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (mat == null)
        {
            EnsureFolder(Path.GetDirectoryName(materialPath).Replace('\\', '/'));
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, materialPath);
        }
        else if (custom && shader != null && mat.shader != shader)
            mat.shader = shader;
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
        if (atlas == null) Debug.LogError($"[LabSceneBuilder] atlas not found: {atlasPath}");
        mat.mainTexture = atlas;
        if (cutout)
        {
            // Simple Lit alpha clipping: the atlas alpha cuts the holes and both faces draw (cards, chainlink).
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // FBX slots keep their Blender material names; slots the layout does not list get the kit material.
    static Material SlotMaterial(Material imported, Kit kit, string meshName)
    {
        if (kit.Materials.Count == 0) return kit.Material;
        if (imported != null && kit.Materials.TryGetValue(imported.name, out var mat)) return mat;
        Debug.LogWarning($"[LabSceneBuilder] {meshName}: slot '{imported?.name}' is not in the layout materials, using {kit.Material.name}");
        return kit.Material;
    }

    static GameObject GetPrefab(string meshName, Kit kit, Dictionary<string, GameObject> cache)
    {
        var key = $"{kit.MeshDir}/{meshName}";
        if (!cache.TryGetValue(key, out var prefab))
            cache[key] = prefab = CreatePrefab(meshName, kit);
        return prefab;
    }

    static GameObject CreatePrefab(string meshName, Kit kit)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{kit.MeshDir}/{meshName}.fbx");
        if (model == null) { Debug.LogError($"[LabSceneBuilder] mesh not found: {kit.MeshDir}/{meshName}"); return null; }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        var bounds = new Bounds();
        var first = true;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
        {
            r.sharedMaterials = r.sharedMaterials.Select(m => SlotMaterial(m, kit, meshName)).ToArray();
            if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);
        }
        if (MeshColliderTags.Any(meshName.Contains))
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }
        else if (!NoColliderTags.Any(meshName.Contains))
        {
            var box = go.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
        }

        EnsureFolder(kit.PrefabDir);
        var prefabName = meshName.StartsWith("SM_") ? "PF_" + meshName.Substring(3) : meshName;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{kit.PrefabDir}/{prefabName}.prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    static Camera CreateReviewCamera(ViewDef def, Color background)
    {
        var go = new GameObject("Camera_Review");
        var cam = go.AddComponent<Camera>();
        go.transform.position = ToUnity(def.pos);
        go.transform.LookAt(ToUnity(def.target));
        cam.fieldOfView = def.fov;
        cam.nearClipPlane = 0.05f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = background;
        return cam;
    }

    static void SetupEnvironment(EnvDef env)
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.fog = true;
        if (!IsOutdoor(env))
        {
            RenderSettings.ambientLight = new Color(0.06f, 0.08f, 0.08f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.02f, 0.03f, 0.03f);
            RenderSettings.fogDensity = 0.06f;
            return;
        }
        RenderSettings.skybox = GetOrCreateForestSkybox();
        RenderSettings.ambientLight = env.ambient;
        RenderSettings.fogMode = Enum.TryParse<FogMode>(env.fogMode, out var mode) ? mode : FogMode.Linear;
        RenderSettings.fogColor = env.fogColor;
        RenderSettings.fogStartDistance = env.fogStart;
        RenderSettings.fogEndDistance = env.fogEnd;
        RenderSettings.fogDensity = env.fogDensity;

        foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            camera.clearFlags = CameraClearFlags.Skybox;
    }

    static Material GetOrCreateForestSkybox()
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ForestSkyboxTexturePath);
        if (texture == null)
        {
            Debug.LogError($"[LabSceneBuilder] skybox texture not found: {ForestSkyboxTexturePath}");
            return null;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(ForestSkyboxMaterialPath);
        if (material == null)
        {
            EnsureFolder(Path.GetDirectoryName(ForestSkyboxMaterialPath).Replace('\\', '/'));
            material = new Material(Shader.Find("Skybox/Panoramic"));
            AssetDatabase.CreateAsset(material, ForestSkyboxMaterialPath);
        }
        material.SetTexture("_MainTex", texture);
        // Skybox/Panoramic uses 0 for six frames and 1 for a 2:1 latitude-longitude panorama.
        material.SetFloat("_Mapping", 1f);
        material.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
        material.SetFloat("_ImageType", 0f);
        material.SetFloat("_MirrorOnBack", 0f);
        material.SetFloat("_Exposure", 0.82f);
        material.SetColor("_Tint", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    // Renders the camera into Logs/<name>.png so results can be checked without looking at the editor.
    internal static string Capture(Camera cam, string name)
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
        var path = Path.Combine(dir, name + ".png");
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        return path;
    }

    internal static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}

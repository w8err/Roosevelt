using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
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
    // _Stair_ climbs on its own treads: a box over a staircase is a ramp the CharacterController cannot step onto,
    // and a ramp collider would make the machine room's catwalk stair feel like an escalator.
    // _Console_ takes a mesh collider because its face is sloped: a box around the whole cabinet
    // reaches out past the panel, and the ray would hit that box before the knob standing on the
    // slope, making every dial and lever unaimable.
    static readonly string[] MeshColliderTags = { "_Wall_Door", "_Terrain_", "_Tree_", "_ChoirTotem_", "_Rock_", "_Log_", "_Stump_",
                                                  "_RootCluster_", "_ServiceDoor_", "_Horizon_", "_Stair_", "_Console_",
                                                  // A cabinet is hollow and holds sockets: a box around it would
                                                  // wall off its own interior from the crosshair.
                                                  "_LabCold_Body_" };
    // _CableTray_ hangs at 2.4 m and above -- overhead dressing the player can never reach, so a box
    // there is collision the physics system pays for and nobody ever touches. Pipe runs keep theirs:
    // they cross the walkway at knee height and are meant to be walked around, not through.
    // Shelves and racks live inside a cabinet the player never walks into, and their boxes would sit
    // between the crosshair and the slots moulded into them.
    static readonly string[] NoColliderTags = { "_DoorPart_", "_Shrub_", "_Grass_", "_Backdrop_", "_Path_", "_WarningLight_", "_CCTV_",
                                                "_CableTray_", "_Shelf_Wire_", "_Rack_Vial_" };

    [Serializable] class Layout
    {
        public string scene;
        public string material;   // in <kit>/Materials, default M_Lab_Atlas
        public string atlas;      // in <kit>/Textures, default T_Lab_Atlas
        public string prefabDir;  // default Assets/Prefabs/Environment/Lab/Modules
        public MaterialDef[] materials; // FBX material slots by name; unlisted slots get `material`
        public Module[] modules;
        public DoorDef[] doors;
        public SpawnDef[] spawns;
        public BoxDef[] boxes;       // temporary furniture: colored cube primitives
        public PrefabDef[] prefabs;  // dressing placed as-is (NPCs, props)
        public LightDef[] lights;
        public ViewDef camera;
        public ViewDef[] playerStarts;
        public ViewDef[] views;   // extra review captures, written as <scene>_view1.png, _view2.png, ...
        public GroundDef ground;
        public EnvDef environment;
        public CreatureDef[] creatures;
        public SoundDef[] sounds;    // ambient loops and random one-shot emitters
        public ConsoleDef[] consoles; // LabPanel cabinets with their knobs, levers and screens
        public MachineDef[] machines; // ties panel parts built above into working machines
    }
    // Prefab name in CreatureBuilder.PrefabDir; walkers get the layout's terrain as their ground.
    [Serializable] class CreatureDef { public string prefab; public Vector3 pos; public float rotZ; public float scale; }
    // alpha below 1 makes the slot see-through (the fridge glass), which cutout cannot do: cutout is
    // all-or-nothing per texel and a window needs a partial tint.
    [Serializable] class MaterialDef { public string name; public string atlas; public bool cutout; public float alpha; public string shader; public string kit; } // shader empty = Simple Lit, kit empty = use kitRoot
    // Conditional object gate: exists only if all conditions are met (minDay/maxDay, requiredFlag, requiredItem, etc.)
    [Serializable] class ConditionalObjectWhen
    {
        public int minDay = -1, maxDay = -1;
        public string requiredFlag = "", forbiddenFlag = "", requiredItem = "", forbiddenItem = "";

        // Returns true if any condition was explicitly specified in JSON
        public bool IsSpecified() =>
            minDay != -1 || maxDay != -1 ||
            !string.IsNullOrEmpty(requiredFlag) || !string.IsNullOrEmpty(forbiddenFlag) ||
            !string.IsNullOrEmpty(requiredItem) || !string.IsNullOrEmpty(forbiddenItem);
    }
    // name is optional and only matters when something else has to find this object: `machines`
    // resolves its references by name, and a module is otherwise called after its mesh.
    [Serializable] class Module { public string name; public string mesh; public Vector3 pos; public float rotZ; public float scale; public InteractDef interact; public string kit; public ConditionalObjectWhen when; } // scale 0 means 1, kit empty = kitRoot/Meshes
    // Empty target: a door that swings on its hinge. Otherwise it stays shut and teleports the player to that spawn.
    // Doors gate by locking (unlockFlag), not by existence (when). A visible locked door tells the player "that's the goal".
    [Serializable] class DoorDef { public string leaf; public string[] parts; public Vector3 pos; public float rotZ; public bool locked; public string target; public string unlockFlag; } // unlockFlag gates door opening (separate from existence gate)
    // Arrival point for transition doors, turned (yaw only) toward target.
    [Serializable] class SpawnDef { public string name; public Vector3 pos; public Vector3 target; }
    // Temporary furniture. pos is the center of the face touching the floor; size is (width, depth, height) in meters.
    [Serializable] class BoxDef { public string name; public Vector3 pos; public Vector3 size; public float rotZ; public Color color; public InteractDef interact; public ConditionalObjectWhen when; }
    // A prefab placed as-is, e.g. the lobby NPC.
    [Serializable] class PrefabDef { public string name; public string prefab; public Vector3 pos; public float rotZ; public float scale; public InteractDef interact; public ConditionalObjectWhen when; }
    // What using a box or prop does. type "bed" sleeps into tonight's dream; "npc" plays `dialogue` (a .dialogue asset path); "chair" sits with look direction.
    // Machine-procedure types attach one panel component each: "carryable", "socket", "dial", "lever",
    // "button", "gauge", "terminal", "chart". They are wired to each other afterwards by `machines`,
    // because a console's parts are separate objects and only names can cross between them in JSON.
    [Serializable] class InteractDef
    {
        public string type; public string dialogue; public Vector3 seat; public Vector3 look; public Vector3 stand; public Vector3 standLook;
        public string partId;   // carryable: which sockets accept it (MachineParts constants)
        public string label;    // prompt name, e.g. "검체 용기". Falls back to the object's own name
        public string accepts;  // socket: partId it takes; empty accepts anything (a bench)
        public int steps;       // dial: positions, default 10
        public bool startsLocked; // socket: clamped shut until a machine opens it
        public float openAngle;   // door: degrees the leaf swings, default 90
        public Vector3 openOffset;      // drawer: where it sits fully open, in its own local space
        public float range, intensity;  // lamp: point light reach and brightness
        public Color color;             // lamp: light colour
    }
    // A LabPanel cabinet and the parts standing on its sloped face. Part transforms come straight from
    // Art/Lab/Lab_Panel_mounts.json, so nobody retypes a coordinate.
    [Serializable] class ConsoleDef { public string name; public string mesh; public string kit; public Vector3 pos; public float rotZ; public PartDef[] parts; }
    // localPosition / localEulerAngles are Unity-space, relative to the cabinet.
    // mesh may be empty: a socket in a hole moulded into another mesh has nothing of its own to
    // draw, and gets colliderSize so the crosshair still has something to land on. parent nests
    // this part under another part of the same console, which is how a slot is stocked at build
    // time: the vial is a child of the socket, and Socket adopts it on Awake.
    [Serializable] class PartDef { public string name; public string mesh; public string kit; public string parent; public Vector3 localPosition; public Vector3 localEulerAngles; public Vector3 colliderSize; public InteractDef interact; }
    // Logic-only objects that tie panel parts into one machine, resolved by name after everything is built.
    // type: "chart" (the lookup table plus the boards that show it), "analyzer", "mixer", "reactor".
    [Serializable] class MachineDef
    {
        public string type; public string name;
        public string socket, lever, terminal, gauge, button, chart, analyzer, mixer, output, compound;
        public string light;
        public string[] dials, boards, specs, slots, doors, drawers;
        public int indexCount, digits, seed;
        public float duration;
    }
    // name is optional, for a light something else has to find: a cabinet's interior lamp.
    [Serializable] class LightDef { public string name; public Vector3 pos; public Color color; public float intensity; public float range; public ConditionalObjectWhen when; }
    [Serializable] class ViewDef { public Vector3 pos; public Vector3 target; public float fov; }
    [Serializable] class GroundDef { public float size; public Color color; }
    // Audio: ambient loop or random one-shot emitter
    [Serializable] class SoundDef
    {
        public string type;  // "ambient" or "random"
        public string name;
        public Vector3 pos;
        public string clip;  // for ambient, or primary clip name for random (clips generated from name pattern)
        public float volume;
        public bool spatial; // ambient only
        public float minDistance; // both types, default 3 (linear attenuation: full volume until this distance)
        public float maxDistance; // both types, default 15 (linear attenuation: silent beyond this distance)
        public float intervalMin; // random, default 0
        public float intervalMax; // random, default 0
        public float radius; // random, 0=fixed, >0=random in radius
        public ConditionalObjectWhen when; // conditional existence gate
    }
    // Outdoor settings. An empty fogMode keeps the dark indoor defaults. sunDirection is in Blender space.
    [Serializable] class EnvDef
    {
        public string fogMode;
        public float fogStart, fogEnd, fogDensity;
        public Color fogColor, ambient, background;
        public Vector3 sunDirection;
        public Color sunColor;
        public float sunIntensity;
        public bool solidSky;
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

        // One layout name per line (json file name, no extension) builds only those; an empty request builds all.
        // External automation can opt into saving open scenes by adding an explicit @save line.
        var lines = File.ReadAllLines(RequestPath).Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
        var saveOpenScenes = lines.Contains("@save");
        var only = lines.Where(l => l != "@save").ToArray();
        File.Delete(RequestPath);
        if (saveOpenScenes && !EditorSceneManager.SaveOpenScenes())
        {
            Debug.LogWarning("[LabSceneBuilder] request skipped: an open scene could not be saved");
            return;
        }
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
        BuildAll(only);
    }

    // `only` limits the build to those layout names, so a request can't overwrite unrelated scenes.
    static void BuildAll(string[] only = null)
    {
        var dirs = LayoutDirs.Where(AssetDatabase.IsValidFolder).ToArray();
        var paths = dirs.Length == 0 ? Array.Empty<string>() : AssetDatabase.FindAssets("t:TextAsset", dirs)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".json"))
            .Where(p => only == null || only.Length == 0 || only.Contains(Path.GetFileNameWithoutExtension(p)))
            .OrderBy(p => p)
            .ToArray();
        if (paths.Length == 0)
        {
            var wanted = only != null && only.Length > 0 ? $" named {string.Join(", ", only)}" : "";
            Debug.LogError($"[LabSceneBuilder] layout not found: no .json{wanted} in {string.Join(", ", LayoutDirs)}");
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
            {
                var matDir = string.IsNullOrEmpty(d.kit) ? $"{kitRoot}/Materials" : $"{kitRoot}/{d.kit}/Materials";
                var texDir = string.IsNullOrEmpty(d.kit) ? $"{kitRoot}/Textures" : $"{kitRoot}/{d.kit}/Textures";
                return GetOrCreateMaterial($"{matDir}/{d.name}.mat", $"{texDir}/{d.atlas}.png", d.cutout, d.shader, d.alpha);
            }),
        };
        Debug.Log($"[LabSceneBuilder] build started: {sceneName}");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var room = new GameObject(sceneName).transform;
        var modules = layout.modules ?? Array.Empty<Module>();
        // IMPORTANT: Save all objects in ACTIVE state. ConditionalObject subscribes to GameState.Changed in Awake,
        // which is never called if the object is inactive at scene load. Runtime evaluation will then never occur,
        // and objects remain invisible forever even when conditions are met. Never call SetActive(false) during build.
        foreach (var m in modules)
        {
            var prefab = GetPrefab(m.mesh, kit, m.kit, kitRoot, layout.prefabDir, kit.Materials, prefabs);
            if (prefab == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, room);
            if (!string.IsNullOrEmpty(m.name)) go.name = m.name;
            go.transform.SetPositionAndRotation(ToUnity(m.pos), Yaw(m.rotZ));
            if (m.scale > 0f) go.transform.localScale = Vector3.one * m.scale;
            AttachInteract(go, m.interact, m.mesh);
            AttachConditionalObject(go, m.when);

            // Rigged non-NPC figures (e.g. seated figures with Idle animation): auto-detect and attach animator
            if ((m.interact == null || string.IsNullOrEmpty(m.interact?.type)) &&
                go.GetComponent<Animator>() != null)
            {
                var animator = go.GetComponent<Animator>();

                // Attach CharacterAnimator script
                if (go.GetComponent<CharacterAnimator>() == null)
                    go.AddComponent<CharacterAnimator>();

                // Attach controller via b1's API
                var controllerPath = NpcAnimatorControllerCreator.ControllerPathFor(m.mesh);
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller != null)
                {
                    animator.runtimeAnimatorController = controller;
                }
                else
                {
                    Debug.LogWarning($"[LabSceneBuilder] {go.name}: controller not found for rigged figure {m.mesh} at {controllerPath}");
                }
            }
        }

        var spawns = BuildSpawns(layout.spawns ?? Array.Empty<SpawnDef>());
        var doors = layout.doors ?? Array.Empty<DoorDef>();
        var doorRoot = new GameObject("Doors").transform;
        foreach (var d in doors) BuildDoor(d, doorRoot, kit, prefabs, spawns);

        var boxes = layout.boxes ?? Array.Empty<BoxDef>();
        BuildBoxes(boxes, kitRoot);
        var propDefs = layout.prefabs ?? Array.Empty<PrefabDef>();
        BuildProps(propDefs);
        var consoles = layout.consoles ?? Array.Empty<ConsoleDef>();
        BuildConsoles(consoles, kit, kitRoot, layout.prefabDir, prefabs);
        // Last, so a machine can point at any box, prop or console part by name.
        WireMachines(layout.machines ?? Array.Empty<MachineDef>());

        var lights = new GameObject("Lights").transform;
        foreach (var l in layout.lights ?? Array.Empty<LightDef>())
        {
            var go = new GameObject(string.IsNullOrEmpty(l.name) ? "Light_Point" : l.name);
            go.transform.SetParent(lights, false);
            go.transform.position = ToUnity(l.pos);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = l.color;
            light.intensity = l.intensity;
            light.range = l.range;
            light.shadows = LightShadows.Hard;
            AttachConditionalObject(go, l.when);
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

        var sounds = layout.sounds ?? Array.Empty<SoundDef>();
        if (sounds.Length > 0) BuildSounds(sounds, room);

        var starts = layout.playerStarts ?? Array.Empty<ViewDef>();
        var cam = CreateReviewCamera(layout.camera, background);
        if (starts.Length > 0) SpawnPlayer(starts[0], background);
        else cam.gameObject.tag = "MainCamera";

        SetupEnvironment(env);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, scenePath);

        var capture = Capture(cam, sceneName);

        // Extra review vantages. One capture only ever proves the one spot it was pointed at, and a
        // room whose whole idea is a second storey cannot be signed off from the floor: the machine
        // room's catwalk needs its own frame or nothing ever looks at the deck the player walks on.
        // Each view reuses the review camera so it inherits the same clear flags and clip planes.
        var views = layout.views ?? Array.Empty<ViewDef>();
        for (int i = 0; i < views.Length; i++)
        {
            cam.transform.position = ToUnity(views[i].pos);
            cam.transform.LookAt(ToUnity(views[i].target));
            if (views[i].fov > 0) cam.fieldOfView = views[i].fov;
            Capture(cam, $"{sceneName}_view{i + 1}");
        }

        // The review camera only exists for the capture; play mode uses the player's camera.
        if (starts.Length > 0)
        {
            cam.gameObject.SetActive(false);
            EditorSceneManager.SaveScene(scene, scenePath);
        }
        Debug.Log($"[LabSceneBuilder] built {sceneName}: {modules.Length} modules, {doors.Length} doors, " +
                  $"{boxes.Length} boxes, {propDefs.Length} prefabs, {views.Length} extra views, capture {capture}");

        ValidateSceneFlags(sceneName, modules, doors, boxes, propDefs, layout.lights ?? Array.Empty<LightDef>(), sounds);
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

    // Audio sources: ambient loops and random one-shot emitters
    static void BuildSounds(SoundDef[] sounds, Transform room)
    {
        var soundsRoot = new GameObject("Sounds").transform;
        foreach (var s in sounds)
        {
            var go = new GameObject("Sound_" + s.name);
            go.transform.SetParent(soundsRoot, false);
            go.transform.position = ToUnity(s.pos);

            if (s.type == "ambient")
            {
                var ambient = go.AddComponent<AmbientLoop>();
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Ambience/{s.clip}.wav");
                if (clip == null) Debug.LogError($"[LabSceneBuilder] {go.name}: clip not found: Assets/Audio/Ambience/{s.clip}.wav");
                var so = new SerializedObject(ambient);
                so.FindProperty("clip").objectReferenceValue = clip;
                so.FindProperty("volume").floatValue = s.volume;
                so.FindProperty("spatial").boolValue = s.spatial;
                so.FindProperty("minDistance").floatValue = s.minDistance > 0 ? s.minDistance : 3f;
                so.FindProperty("maxDistance").floatValue = s.maxDistance > 0 ? s.maxDistance : 15f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else if (s.type == "random")
            {
                var random = go.AddComponent<RandomSoundEmitter>();
                var clips = new List<AudioClip>();

                // Try numbered clips first: {clip}_01.wav, _02.wav, etc.
                for (int i = 1; i <= 99; i++)
                {
                    var clipPath = $"Assets/Audio/Ambience/{s.clip}_{i:00}.wav";
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                    if (clip == null) break;
                    clips.Add(clip);
                }

                // If no numbered clips, try single file {clip}.wav
                if (clips.Count == 0)
                {
                    var clipPath = $"Assets/Audio/Ambience/{s.clip}.wav";
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                    if (clip != null)
                        clips.Add(clip);
                    else
                        Debug.LogError($"[LabSceneBuilder] {go.name}: no clips found for '{s.clip}' (tried {s.clip}_01, {s.clip}_02, ... and {s.clip}.wav)");
                }

                var so = new SerializedObject(random);
                so.FindProperty("volume").floatValue = s.volume;
                so.FindProperty("intervalMin").floatValue = s.intervalMin;
                so.FindProperty("intervalMax").floatValue = s.intervalMax;
                so.FindProperty("radius").floatValue = s.radius;
                so.FindProperty("minDistance").floatValue = s.minDistance > 0 ? s.minDistance : 3f;
                so.FindProperty("maxDistance").floatValue = s.maxDistance > 0 ? s.maxDistance : 15f;

                // Populate clips array
                var clipsProp = so.FindProperty("clips");
                if (clipsProp != null)
                {
                    clipsProp.arraySize = clips.Count;
                    for (int i = 0; i < clips.Count; i++)
                        clipsProp.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }
            AttachConditionalObject(go, s.when);
        }
        Debug.Log($"[LabSceneBuilder] {sounds.Length} audio sources");
    }

    static Dictionary<string, Transform> BuildSpawns(SpawnDef[] defs)
    {
        var map = new Dictionary<string, Transform>();
        if (defs.Length == 0) return map;
        var root = new GameObject("Spawns").transform;
        foreach (var s in defs)
        {
            var t = new GameObject("Spawn_" + s.name).transform;
            t.SetParent(root, false);
            var pos = ToUnity(s.pos);
            var dir = ToUnity(s.target) - pos;
            dir.y = 0f;
            t.SetPositionAndRotation(pos, dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir) : Quaternion.identity);
            map[s.name] = t;
        }
        return map;
    }

    static void BuildDoor(DoorDef d, Transform parent, Kit kit, Dictionary<string, GameObject> prefabs,
                          Dictionary<string, Transform> spawns)
    {
        var root = new GameObject("Door_" + d.leaf.Replace("SM_Lab_DoorLeaf_", "")).transform;
        root.SetParent(parent, false);
        root.SetPositionAndRotation(ToUnity(d.pos), Yaw(d.rotZ));

        // Swinging doors hang the leaf on a hinge; transition doors keep it where it is.
        var holder = root;
        var leafOffset = Vector3.zero;
        if (string.IsNullOrEmpty(d.target))
        {
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(root, false);
            hinge.localPosition = HingeOffset;
            var door = hinge.gameObject.AddComponent<LabDoor>();
            var so = new SerializedObject(door);
            so.FindProperty("locked").boolValue = d.locked;
            so.ApplyModifiedPropertiesWithoutUndo();
            holder = hinge;
            leafOffset = -HingeOffset;
        }
        else
        {
            spawns.TryGetValue(d.target, out var spawn);
            if (spawn == null) Debug.LogError($"[LabSceneBuilder] {root.name}: spawn '{d.target}' not found");
            root.name += "_To_" + d.target;
            var door = root.gameObject.AddComponent<TransitionDoor>();
            var so = new SerializedObject(door);
            so.FindProperty("destination").objectReferenceValue = spawn;
            so.FindProperty("locked").boolValue = d.locked;
            so.FindProperty("requiredFlag").stringValue = d.unlockFlag ?? "";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach (var mesh in new[] { d.leaf }.Concat(d.parts ?? Array.Empty<string>()))
        {
            var prefab = GetPrefab(mesh, kit, null, "", "", kit.Materials, prefabs);
            if (prefab == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
            go.transform.localPosition = leafOffset;
            go.transform.localRotation = Quaternion.identity;
        }
    }

    // Temporary furniture boxes, e.g. a bed frame before the real model exists.
    static void BuildBoxes(BoxDef[] defs, string kitRoot)
    {
        if (defs.Length == 0) return;
        var root = new GameObject("Boxes").transform;
        foreach (var b in defs)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Box_" + b.name;
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(ToUnity(b.pos) + new Vector3(0f, b.size.z / 2f, 0f), Yaw(b.rotZ));
            go.transform.localScale = new Vector3(b.size.x, b.size.z, b.size.y);
            go.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreatePlaceholderMaterial(kitRoot, b.color);
            AttachInteract(go, b.interact);
            AttachConditionalObject(go, b.when);
        }
    }

    // One material asset per color, reused across boxes and layouts.
    static Material GetOrCreatePlaceholderMaterial(string kitRoot, Color color)
    {
        var path = $"{kitRoot}/Materials/M_Placeholder_{ColorUtility.ToHtmlStringRGB(color)}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Prefabs placed as-is: the lobby NPC and similar dressing that isn't part of the module kit.
    static void BuildProps(PrefabDef[] defs)
    {
        if (defs.Length == 0) return;
        var root = new GameObject("Props").transform;
        foreach (var p in defs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.prefab);
            if (prefab == null)
            {
                Debug.LogError($"[LabSceneBuilder] prop prefab not found: {p.prefab}");
                continue;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.name = p.name;
            go.transform.SetPositionAndRotation(ToUnity(p.pos), Yaw(p.rotZ));
            if (p.scale > 0f) go.transform.localScale = Vector3.one * p.scale;
            AttachInteract(go, p.interact);
            AttachConditionalObject(go, p.when);
        }
    }

    // Attach ConditionalObject component if when conditions are specified
    static void AttachConditionalObject(GameObject go, ConditionalObjectWhen when)
    {
        if (when == null || !when.IsSpecified()) return;

        var conditional = go.AddComponent<ConditionalObject>();
        if (conditional == null)
        {
            Debug.LogWarning($"[LabSceneBuilder] {go.name}: failed to attach ConditionalObject");
            return;
        }

        var so = new SerializedObject(conditional);
        so.FindProperty("minDay").intValue = when.minDay;
        so.FindProperty("maxDay").intValue = when.maxDay;
        so.FindProperty("requiredFlag").stringValue = when.requiredFlag ?? "";
        so.FindProperty("forbiddenFlag").stringValue = when.forbiddenFlag ?? "";
        so.FindProperty("requiredItem").stringValue = when.requiredItem ?? "";
        so.FindProperty("forbiddenItem").stringValue = when.forbiddenItem ?? "";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // JsonUtility fills a missing `interact` with an empty object, so an empty type means none.
    static void AttachInteract(GameObject go, InteractDef def, string meshName = "")
    {
        if (def == null || string.IsNullOrEmpty(def.type)) return;
        switch (def.type)
        {
            case "npc":
            {
                // RequireComponent adds the capsule; size it based on mesh bounds for accurate raycasting.
                // Remove any BoxColliders (module default) and keep only the capsule.
                foreach (var box in go.GetComponentsInChildren<BoxCollider>())
                    UnityEngine.Object.DestroyImmediate(box);

                var npc = go.GetComponent<NpcInteractable>() ?? go.AddComponent<NpcInteractable>();
                var capsule = go.GetComponent<CapsuleCollider>();

                // Size capsule from combined mesh renderer bounds (includes children like CreatePrefab does)
                var renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                        bounds.Encapsulate(renderers[i].bounds);

                    capsule.height = bounds.size.y;
                    capsule.center = new Vector3(0f, bounds.size.y / 2f, 0f);
                    var horizontalRadius = Mathf.Min(bounds.extents.x, bounds.extents.z);
                    capsule.radius = Mathf.Clamp(horizontalRadius, 0.2f, 0.4f);
                }
                else
                {
                    // Fallback to defaults if no renderers found
                    capsule.center = new Vector3(0f, 0.9f, 0f);
                    capsule.height = 1.8f;
                    capsule.radius = 0.35f;
                }

                // Attach Animator and animation controller (or reuse existing)
                var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                if (!string.IsNullOrEmpty(meshName))
                {
                    var controllerPath = NpcAnimatorControllerCreator.ControllerPathFor(meshName);
                    var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                    if (controller != null)
                    {
                        animator.runtimeAnimatorController = controller;
                    }
                    else
                    {
                        Debug.LogWarning($"[LabSceneBuilder] {go.name}: animation controller not found at {controllerPath}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[LabSceneBuilder] {go.name}: meshName not provided for NPC controller");
                }

                // Attach CharacterAnimator script (or reuse existing)
                if (go.GetComponent<CharacterAnimator>() == null) go.AddComponent<CharacterAnimator>();

                var dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(def.dialogue);
                if (dialogue == null) Debug.LogError($"[LabSceneBuilder] {go.name}: dialogue not found: {def.dialogue}");
                var so = new SerializedObject(npc);
                so.FindProperty("dialogue").objectReferenceValue = dialogue;
                so.ApplyModifiedPropertiesWithoutUndo();
                break;
            }
            case "chair":
            {
                var chair = go.GetComponent<ChairInteractable>() ?? go.AddComponent<ChairInteractable>();
                var seatWorldPos = ToUnity(def.seat);
                var lookWorldPos = ToUnity(def.look);
                // SeatPosition: yaw only (horizontal rotation toward look)
                var seatGo = new GameObject("SeatPosition");
                seatGo.transform.SetParent(go.transform, false);
                var lookDir = lookWorldPos - seatWorldPos;
                lookDir.y = 0f;
                var yawRotation = lookDir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(lookDir) : Quaternion.identity;
                seatGo.transform.SetPositionAndRotation(seatWorldPos, yawRotation);
                // CameraTarget: world position
                var camGo = new GameObject("CameraTarget");
                camGo.transform.SetParent(go.transform, false);
                camGo.transform.position = lookWorldPos;
                // Set references via SerializedObject
                var so = new SerializedObject(chair);
                so.FindProperty("seatPosition").objectReferenceValue = seatGo.transform;
                so.FindProperty("cameraLookTarget").objectReferenceValue = camGo.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
                break;
            }
            case "bed":
            {
                // NOTE: requires BedInteractable fields from roosevelt-b1
                if (def.seat == Vector3.zero) return; // Old JSON without lying data
                var bed = go.GetComponent<BedInteractable>() ?? go.AddComponent<BedInteractable>();
                var lieEyeWorldPos = ToUnity(def.seat);
                var lieLookWorldPos = ToUnity(def.look);
                var standWorldPos = ToUnity(def.stand);
                var standLookWorldPos = ToUnity(def.standLook);
                // LieEye: lying camera position, yaw toward look direction
                var lieEyeGo = new GameObject("LieEye");
                lieEyeGo.transform.SetParent(go.transform, false);
                var lieLookDir = lieLookWorldPos - lieEyeWorldPos;
                lieLookDir.y = 0f;
                var lieYaw = lieLookDir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(lieLookDir) : Quaternion.identity;
                lieEyeGo.transform.SetPositionAndRotation(lieEyeWorldPos, lieYaw);
                // LieLook: lying gaze target
                var lieLookGo = new GameObject("LieLook");
                lieLookGo.transform.SetParent(go.transform, false);
                lieLookGo.transform.position = lieLookWorldPos;
                // StandPosition: standing location, yaw toward stand look direction
                var standGo = new GameObject("StandPosition");
                standGo.transform.SetParent(go.transform, false);
                var standDir = standLookWorldPos - standWorldPos;
                standDir.y = 0f;
                var standYaw = standDir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(standDir) : Quaternion.identity;
                standGo.transform.SetPositionAndRotation(standWorldPos, standYaw);
                // Set references via SerializedObject
                var so = new SerializedObject(bed);
                so.FindProperty("lieEye").objectReferenceValue = lieEyeGo.transform;
                so.FindProperty("lieLook").objectReferenceValue = lieLookGo.transform;
                so.FindProperty("standPosition").objectReferenceValue = standGo.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
                break;
            }
            case "carryable":
            {
                var part = go.GetComponent<Carryable>() ?? go.AddComponent<Carryable>();
                var so = new SerializedObject(part);
                so.FindProperty("partId").stringValue = string.IsNullOrEmpty(def.partId) ? MachineParts.Sample : def.partId;
                so.FindProperty("displayName").stringValue = Label(def, go);
                so.ApplyModifiedPropertiesWithoutUndo();
                break;
            }
            case "socket":
            {
                var socket = go.GetComponent<Socket>() ?? go.AddComponent<Socket>();
                // No mount child: the kit puts a socket's origin exactly where the part's own origin
                // belongs, at the floor of the recess, so Socket falls back to this transform and a
                // vial stands in the hole rather than hovering over it. The part inherits the pivot's
                // slope with it, which is what a recess cut into a tilted panel should do.
                var so = new SerializedObject(socket);
                so.FindProperty("acceptedPartId").stringValue = def.accepts ?? "";
                so.FindProperty("displayName").stringValue = Label(def, go);
                so.FindProperty("startsLocked").boolValue = def.startsLocked;
                so.ApplyModifiedPropertiesWithoutUndo();
                break;
            }
            case "dial":
            {
                var dial = go.GetComponent<RotaryDial>() ?? go.AddComponent<RotaryDial>();
                var so = new SerializedObject(dial);
                so.FindProperty("knob").objectReferenceValue = go.transform;
                so.FindProperty("steps").intValue = def.steps > 0 ? def.steps : 10;
                so.FindProperty("displayName").stringValue = Label(def, go);
                so.ApplyModifiedPropertiesWithoutUndo();
                break;
            }
            case "lever":
            {
                var lever = go.GetComponent<ToggleLever>() ?? go.AddComponent<ToggleLever>();
                var so = new SerializedObject(lever);
                so.FindProperty("handle").objectReferenceValue = go.transform;
                so.FindProperty("displayName").stringValue = Label(def, go);
                so.ApplyModifiedPropertiesWithoutUndo();
                break;
            }
            case "button":
            {
                var button = go.GetComponent<PushButton>() ?? go.AddComponent<PushButton>();
                var so = new SerializedObject(button);
                so.FindProperty("plunger").objectReferenceValue = go.transform;
                so.FindProperty("displayName").stringValue = Label(def, go);
                so.ApplyModifiedPropertiesWithoutUndo();
                break;
            }
            case "gauge":
            {
                var gauge = go.GetComponent<NeedleGauge>() ?? go.AddComponent<NeedleGauge>();
                var so = new SerializedObject(gauge);
                so.FindProperty("needle").objectReferenceValue = go.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
                // Asking the component rather than re-reading minAngle off this SerializedObject:
                // after Apply it hands back 0 for untouched fields, and a 0 here produces an identity
                // rotation, which looks exactly like the line never having run.
                gauge.ApplyRestPose();
                break;
            }
            case "lamp":
            {
                // The bulb inside a cabinet, wired to its doors by StorageBay. A part rather than a
                // layout light, so its position comes from the kit's own mount file in the body's
                // frame instead of being converted by hand into room coordinates.
                // Explicit == rather than ??: for a built-in component Unity hands back a fake null
                // that ?? happily accepts, and the next line then throws MissingComponentException.
                // Only Unity's overloaded == sees through it.
                var lamp = go.GetComponent<Light>();
                if (lamp == null) lamp = go.AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.range = def.range > 0f ? def.range : 1.2f;
                lamp.intensity = def.intensity > 0f ? def.intensity : 2f;
                lamp.color = def.color.maxColorComponent > 0f ? def.color : Color.white;
                lamp.shadows = LightShadows.None;
                break;
            }
            case "drawer":
            {
                // The sliding counterpart of "door". The kit leaves the drawer at its shut position
                // and says which way it pulls, so nothing here has to guess an axis.
                var slide = go.GetComponent<Drawer>();
                if (slide == null) slide = go.AddComponent<Drawer>();
                var so = new SerializedObject(slide);
                if (def.openOffset.sqrMagnitude > 0f)
                    so.FindProperty("openOffset").vector3Value = def.openOffset;
                so.FindProperty("locked").boolValue = def.startsLocked;
                so.ApplyModifiedPropertiesWithoutUndo();
                break;
            }
            case "door":
            {
                // The same hinge component the lab's wall doors use, on a cabinet leaf whose origin
                // the kit put on the hinge axis. StorageBay listens to it for what is behind it.
                var hinge = go.GetComponent<LabDoor>() ?? go.AddComponent<LabDoor>();
                var so = new SerializedObject(hinge);
                if (Mathf.Abs(def.openAngle) > 0.01f) so.FindProperty("openAngle").floatValue = def.openAngle;
                so.FindProperty("locked").boolValue = def.startsLocked;
                so.ApplyModifiedPropertiesWithoutUndo();
                break;
            }
            case "terminal":
            {
                // Goes straight on the kit's screen plane, whose origin is the middle of the glass
                // and whose scale is 1, so the world-space canvas MachineTerminal builds lands on
                // the glass at the right size without any compensation.
                var terminal = go.GetComponent<MachineTerminal>() ?? go.AddComponent<MachineTerminal>();
                var so = new SerializedObject(terminal);
                so.FindProperty("displayName").stringValue = Label(def, go);
                so.ApplyModifiedPropertiesWithoutUndo();
                break;
            }
            case "chart":
            {
                var board = go.GetComponent<InspectableChart>() ?? go.AddComponent<InspectableChart>();
                var so = new SerializedObject(board);
                so.FindProperty("displayName").stringValue = Label(def, go);
                so.ApplyModifiedPropertiesWithoutUndo();
                break;
            }
            default:
                Debug.LogError($"[LabSceneBuilder] {go.name}: unknown interact type '{def.type}'");
                break;
        }
    }

    // A cabinet plus the parts on its face. Each part hangs off its own pivot, which carries the
    // console's slope, while the part's own local rotation stays identity. That split is required,
    // not tidy: RotaryDial writes localEulerAngles.z, ToggleLever writes .x and NeedleGauge writes
    // .y, so a tilt stored on the part itself is erased the first time it moves. PushButton travels
    // along its parent's -Z, which is the pivot's, so the cap sinks into the panel rather than
    // straight back.
    //
    // Every moving part turns its own prefab root, never a child found by name. The kit's contract
    // is that a part's root origin sits on its turning axis, and a Blender FBX carries a second node
    // under that root whose name matches just as well ("..._Gauge_Needle" and "..._Gauge_Needle.001")
    // but whose origin and axes are its own: turning that one swings the needle around a point that
    // is not the middle of the dial.
    static void BuildConsoles(ConsoleDef[] defs, Kit kit, string kitRoot, string prefabDir,
                              Dictionary<string, GameObject> prefabs)
    {
        if (defs.Length == 0) return;
        var root = new GameObject("Consoles").transform;
        foreach (var c in defs)
        {
            var bodyPrefab = GetPrefab(c.mesh, kit, c.kit, kitRoot, prefabDir, kit.Materials, prefabs);
            if (bodyPrefab == null) continue;
            var body = (GameObject)PrefabUtility.InstantiatePrefab(bodyPrefab, root);
            body.name = c.name;
            body.transform.SetPositionAndRotation(ToUnity(c.pos), Yaw(c.rotZ));

            // A utility cart keeps its drawer fronts and handle clear of raycasts. The prefab's generic
            // full-bounds box reaches past the drawer fronts and makes the drawers and their slots impossible
            // to target, so the scene instance gets a low compound body and kinematic transport.
            if (c.mesh.Contains("_Cart_")) ConfigureUtilityCart(body);

            // Parts can nest inside earlier parts of the same console, so a slot can be stocked.
            var built = new Dictionary<string, Transform>();

            foreach (var p in c.parts ?? Array.Empty<PartDef>())
            {
                var host = body.transform;
                if (!string.IsNullOrEmpty(p.parent))
                {
                    if (!built.TryGetValue(p.parent, out host))
                    {
                        Debug.LogError($"[LabSceneBuilder] console '{c.name}': part '{p.name}' wants parent '{p.parent}', which is not one of the parts listed before it");
                        continue;
                    }
                }

                var pivot = new GameObject("Pivot_" + p.name).transform;
                pivot.SetParent(host, false);
                pivot.localPosition = p.localPosition;
                pivot.localEulerAngles = p.localEulerAngles;

                GameObject part;
                if (string.IsNullOrEmpty(p.mesh))
                {
                    // Nothing to draw: a socket sitting in a hole that belongs to another mesh.
                    part = new GameObject(p.name);
                    part.transform.SetParent(pivot, false);
                    if (p.colliderSize.sqrMagnitude > 0f)
                    {
                        var hole = part.AddComponent<BoxCollider>();
                        hole.size = p.colliderSize;
                        // Centred on the hole's mouth rather than its floor, so the crosshair finds
                        // the opening the player is actually looking into.
                        hole.center = new Vector3(0f, p.colliderSize.y * 0.5f, 0f);
                    }
                }
                else
                {
                    var partPrefab = GetPrefab(p.mesh, kit, p.kit, kitRoot, prefabDir, kit.Materials, prefabs);
                    if (partPrefab == null) continue;
                    part = (GameObject)PrefabUtility.InstantiatePrefab(partPrefab, pivot);
                    part.name = p.name;
                    part.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                }
                built[p.name] = part.transform;
                AttachInteract(part, p.interact, p.mesh);
                // After AttachInteract and only for things the player aims at. A collider on the
                // gauge needle would be a raycast blocker sweeping across the console face as the
                // needle moves, stealing the lever and the socket from the crosshair at some
                // pressures and not others. Dressing gets no collider at all.
                if (part.GetComponent<IInteractable>() != null) EnsurePartCollider(part);
            }
        }
    }

    static void ConfigureUtilityCart(GameObject body)
    {
        foreach (var collider in body.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(collider);

        // Covers wheels, lower shelf, posts and the upper tray, but ends below the vial sockets.
        // This keeps the cart solid to the player without intercepting a ray aimed at its rack.
        var frame = body.AddComponent<BoxCollider>();
        // Four centimetres of caster clearance keeps horizontal Rigidbody.SweepTest calls from
        // mistaking floor-tile seams for a wall while preserving the full cart footprint.
        frame.center = new Vector3(0f, 0.38f, 0f);
        frame.size = new Vector3(0.9f, 0.68f, 0.5f);

        var rigidbody = body.AddComponent<Rigidbody>();
        rigidbody.mass = 38f;
        rigidbody.useGravity = false;
        rigidbody.isKinematic = true;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.constraints = RigidbodyConstraints.FreezePositionY |
                                RigidbodyConstraints.FreezeRotationX |
                                RigidbodyConstraints.FreezeRotationZ;

        var audio = body.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        audio.rolloffMode = AudioRolloffMode.Linear;
        audio.minDistance = 1f;
        audio.maxDistance = 12f;

        var cart = body.AddComponent<UtilityCart>();
        var so = new SerializedObject(cart);
        // Only the knock: it peaks on its first frame and decays, which is what a bump sounds like.
        // Metal_Groan swells for a second and fades, so as a collision layer it arrived as an
        // unexplained noise rising behind the player rather than as the cart they just pushed.
        so.FindProperty("wheelImpactClip").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Ambience/Pipe_Knock_01.wav");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // The kit ships panel parts without colliders. A part needs one to be aimed at, and it has to
    // stand slightly proud of the cabinet's mesh collider, or a knob set flush into the slope loses
    // the raycast to the panel behind it.
    static void EnsurePartCollider(GameObject part)
    {
        if (part.GetComponentInChildren<Collider>() != null) return;
        var filter = part.GetComponentInChildren<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;

        var bounds = filter.sharedMesh.bounds;
        // Local space of the mesh may not be the part's own if the prefab nests it.
        if (filter.transform != part.transform)
        {
            var offset = part.transform.InverseTransformPoint(filter.transform.TransformPoint(bounds.center));
            bounds = new Bounds(offset, bounds.size);
        }
        var box = part.AddComponent<BoxCollider>();
        box.center = bounds.center;
        // A flat plane (the CRT glass) would otherwise get a zero-thickness box that the ray slips past.
        box.size = bounds.size + Vector3.one * 0.02f;
    }

    // Ties the panel parts into machines. Runs after every object exists, because a console's socket,
    // lever, dials and CRT are separate objects and JSON can only point at them by name.
    static void WireMachines(MachineDef[] defs)
    {
        if (defs.Length == 0) return;
        var root = new GameObject("Machines").transform;
        var charts = new Dictionary<string, ProcedureChart>();

        // Charts first: every other machine points at one.
        foreach (var d in defs)
        {
            if (d.type != "chart") continue;
            var chart = NewMachine<ProcedureChart>(d.name, root);
            var so = new SerializedObject(chart);
            if (d.specs != null && d.specs.Length > 0)
            {
                var labels = so.FindProperty("specLabels");
                labels.arraySize = d.specs.Length;
                for (var i = 0; i < d.specs.Length; i++)
                    labels.GetArrayElementAtIndex(i).stringValue = d.specs[i];
            }
            if (d.indexCount > 0) so.FindProperty("indexCount").intValue = d.indexCount;
            if (d.digits > 0) so.FindProperty("digitsPerCell").intValue = d.digits;
            if (d.seed != 0) so.FindProperty("seed").intValue = d.seed;
            so.ApplyModifiedPropertiesWithoutUndo();
            charts[d.name] = chart;

            foreach (var boardName in d.boards ?? Array.Empty<string>())
            {
                var board = FindComponent<InspectableChart>(boardName);
                if (board == null) continue;
                var bso = new SerializedObject(board);
                bso.FindProperty("chart").objectReferenceValue = chart;
                bso.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        foreach (var d in defs)
        {
            ProcedureChart chart = null;
            if (!string.IsNullOrEmpty(d.chart) && !charts.TryGetValue(d.chart, out chart))
                Debug.LogError($"[LabSceneBuilder] machine '{d.name}': no chart named '{d.chart}'");

            switch (d.type)
            {
                case "chart":
                    break;
                case "analyzer":
                {
                    var so = new SerializedObject(NewMachine<SampleAnalyzer>(d.name, root));
                    so.FindProperty("sampleSocket").objectReferenceValue = FindComponent<Socket>(d.socket);
                    so.FindProperty("runLever").objectReferenceValue = FindComponent<ToggleLever>(d.lever);
                    so.FindProperty("terminal").objectReferenceValue = FindComponent<MachineTerminal>(d.terminal);
                    so.FindProperty("chart").objectReferenceValue = chart;
                    if (d.duration > 0f) so.FindProperty("analyzeDuration").floatValue = d.duration;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    break;
                }
                case "mixer":
                {
                    var so = new SerializedObject(NewMachine<CompoundMixer>(d.name, root));
                    var dials = so.FindProperty("dials");
                    var names = d.dials ?? Array.Empty<string>();
                    dials.arraySize = names.Length;
                    for (var i = 0; i < names.Length; i++)
                        dials.GetArrayElementAtIndex(i).objectReferenceValue = FindComponent<RotaryDial>(names[i]);
                    so.FindProperty("commitButton").objectReferenceValue = FindComponent<PushButton>(d.button);
                    so.FindProperty("terminal").objectReferenceValue = FindComponent<MachineTerminal>(d.terminal);
                    so.FindProperty("chart").objectReferenceValue = chart;
                    so.FindProperty("analyzer").objectReferenceValue = FindComponent<SampleAnalyzer>(d.analyzer);
                    so.FindProperty("outputSocket").objectReferenceValue = FindComponent<Socket>(d.output);
                    so.FindProperty("compound").objectReferenceValue = FindComponent<Carryable>(d.compound);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    break;
                }
                case "bay":
                {
                    var so = new SerializedObject(NewMachine<StorageBay>(d.name, root));
                    var bayDoors = so.FindProperty("doors");
                    var doorNames = d.doors ?? Array.Empty<string>();
                    bayDoors.arraySize = doorNames.Length;
                    for (var i = 0; i < doorNames.Length; i++)
                        bayDoors.GetArrayElementAtIndex(i).objectReferenceValue = FindComponent<LabDoor>(doorNames[i]);
                    var slots = so.FindProperty("slots");
                    var names = d.slots ?? Array.Empty<string>();
                    slots.arraySize = names.Length;
                    for (var i = 0; i < names.Length; i++)
                        slots.GetArrayElementAtIndex(i).objectReferenceValue = FindComponent<Socket>(names[i]);
                    var bayDrawers = so.FindProperty("drawers");
                    var drawerNames = d.drawers ?? Array.Empty<string>();
                    bayDrawers.arraySize = drawerNames.Length;
                    for (var i = 0; i < drawerNames.Length; i++)
                        bayDrawers.GetArrayElementAtIndex(i).objectReferenceValue = FindComponent<Drawer>(drawerNames[i]);
                    if (!string.IsNullOrEmpty(d.light))
                        so.FindProperty("interiorLight").objectReferenceValue = FindComponent<Light>(d.light);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    break;
                }
                case "reactor":
                {
                    var so = new SerializedObject(NewMachine<RunConsole>(d.name, root));
                    so.FindProperty("compoundSocket").objectReferenceValue = FindComponent<Socket>(d.socket);
                    so.FindProperty("runLever").objectReferenceValue = FindComponent<ToggleLever>(d.lever);
                    so.FindProperty("gauge").objectReferenceValue = FindComponent<NeedleGauge>(d.gauge);
                    so.FindProperty("terminal").objectReferenceValue = FindComponent<MachineTerminal>(d.terminal);
                    so.FindProperty("mixer").objectReferenceValue = FindComponent<CompoundMixer>(d.mixer);
                    if (d.duration > 0f) so.FindProperty("runDuration").floatValue = d.duration;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    break;
                }
                default:
                    Debug.LogError($"[LabSceneBuilder] unknown machine type '{d.type}'");
                    break;
            }
        }
        // The mixer is created after the analyzer only if the layout happens to list it later, so the
        // analyzer reference is resolved by name above rather than by build order.
    }

    static T NewMachine<T>(string name, Transform parent) where T : Component
    {
        var go = new GameObject(string.IsNullOrEmpty(name) ? typeof(T).Name : name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<T>();
    }

    // Layout names refer to boxes as the layout wrote them; BuildBoxes prefixes the object with "Box_".
    static GameObject FindNamed(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name || t.name == "Box_" + name) return t.gameObject;
        Debug.LogError($"[LabSceneBuilder] machine wiring: no object named '{name}'");
        return null;
    }

    static T FindComponent<T>(string name) where T : Component
    {
        var go = FindNamed(name);
        if (go == null) return null;
        // Children too: a terminal's component lives on the screen plane inside the monitor, not on
        // the monitor's own object.
        var component = go.GetComponentInChildren<T>(true);
        if (component == null)
            Debug.LogError($"[LabSceneBuilder] machine wiring: '{name}' has no {typeof(T).Name}");
        return component;
    }

    static string Label(InteractDef def, GameObject go) =>
        string.IsNullOrEmpty(def.label) ? go.name.Replace("Box_", "") : def.label;


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
            EnsureCarryHands(existing);
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

    // Carrying a part is a player ability, not a scene's, so it belongs on the prefab rather than on
    // whatever object the layout happens to spawn. Added in place like the camera upgrade above, so
    // scenes built before the machine work pick it up on their next build.
    static void EnsureCarryHands(GameObject prefab)
    {
        if (prefab.GetComponent<CarryHands>() != null) return;

        var contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        contents.AddComponent<CarryHands>();
        PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
        PrefabUtility.UnloadPrefabContents(contents);
        Debug.Log("[LabSceneBuilder] PF_Player: added CarryHands");
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

    static Material GetOrCreateMaterial(string materialPath, string atlasPath, bool cutout = false, string shaderName = null, float alpha = 0f)
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
        else if (alpha > 0f && alpha < 1f)
        {
            // Simple Lit alpha blending, for a pane you see the room through. Depth writing goes off
            // the way every transparent surface needs, or the glass would hide the shelves behind it.
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            // The atlas has no alpha channel of its own, so the tint carries it.
            var tint = new Color(1f, 1f, 1f, alpha);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
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

    static GameObject GetPrefab(string meshName, Kit kit, string moduleKit, string kitRoot, string defaultPrefabDir, Dictionary<string, Material> layoutMaterials, Dictionary<string, GameObject> cache)
    {
        // Use module kit if specified, otherwise use default kit
        var resolvedKit = string.IsNullOrEmpty(moduleKit) ? kit : CreateModuleKit(moduleKit, kitRoot, defaultPrefabDir, kit, layoutMaterials);
        var key = $"{resolvedKit.MeshDir}/{meshName}";
        if (!cache.TryGetValue(key, out var prefab))
            cache[key] = prefab = CreatePrefab(meshName, resolvedKit);
        return prefab;
    }

    static Kit CreateModuleKit(string moduleKit, string kitRoot, string defaultPrefabDir, Kit parentKit, Dictionary<string, Material> layoutMaterials)
    {
        var meshDir = $"{kitRoot}/{moduleKit}/Meshes";
        var prefabDir = string.IsNullOrEmpty(defaultPrefabDir)
            ? $"{kitRoot}/{moduleKit}"
            : $"{Path.GetDirectoryName(defaultPrefabDir).Replace('\\', '/')}/{moduleKit}";
        // Use parent kit's material as default (no new materials created for module kits)
        return new Kit
        {
            MeshDir = meshDir,
            PrefabDir = prefabDir,
            Material = parentKit.Material,
            Materials = layoutMaterials // Share the materials dictionary
        };
    }

    static GameObject CreatePrefab(string meshName, Kit kit)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{kit.MeshDir}/{meshName}.fbx");
        if (model == null) { Debug.LogError($"[LabSceneBuilder] mesh not found: {kit.MeshDir}/{meshName}"); return null; }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        var bounds = new Bounds();
        var first = true;
        // Handle both MeshRenderer and SkinnedMeshRenderer (rigged models)
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterials = r.sharedMaterials.Select(m => SlotMaterial(m, kit, meshName)).ToArray();
            if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);
        }
        if (MeshColliderTags.Any(meshName.Contains))
        {
            // Note: MeshFilter only exists on static meshes, not on SkinnedMeshRenderer (rigged models)
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
            // Indoor defaults are the dim lab every story scene wants. A layout may still override
            // them without becoming an outdoor one (IsOutdoor keys off fogMode, which stays empty):
            // the interaction test bench has to be legible before it is atmospheric, and at the
            // default 0.06 density a 12 m room loses half its light before it reaches the far wall.
            var lit = env != null && env.ambient.maxColorComponent > 0f;
            RenderSettings.ambientLight = lit ? env.ambient : new Color(0.06f, 0.08f, 0.08f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = env != null && env.fogColor.maxColorComponent > 0f
                ? env.fogColor : new Color(0.02f, 0.03f, 0.03f);
            RenderSettings.fogDensity = env != null && env.fogDensity > 0f ? env.fogDensity : 0.06f;
            return;
        }
        // Outdoor: common settings
        RenderSettings.ambientLight = env.ambient;
        RenderSettings.fogMode = Enum.TryParse<FogMode>(env.fogMode, out var mode) ? mode : FogMode.Linear;
        RenderSettings.fogColor = env.fogColor;
        RenderSettings.fogStartDistance = env.fogStart;
        RenderSettings.fogEndDistance = env.fogEnd;
        RenderSettings.fogDensity = env.fogDensity;
        // Sky mode differs
        var clearMode = env.solidSky ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
        if (!env.solidSky) RenderSettings.skybox = GetOrCreateForestSkybox();
        foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            camera.clearFlags = clearMode;
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

    static void ValidateSceneFlags(string scene, Module[] modules, DoorDef[] doors, BoxDef[] boxes,
                                   PrefabDef[] prefabs, LightDef[] lights, SoundDef[] sounds)
    {
        var usedFlags = new Dictionary<string, string>();  // flag name → source (first occurrence)
        var usedItems = new Dictionary<string, string>();  // item name → source

        Action<ConditionalObjectWhen, string> addWhen = (when, source) =>
        {
            if (when == null) return;
            if (!string.IsNullOrEmpty(when.requiredFlag) && !usedFlags.ContainsKey(when.requiredFlag)) usedFlags[when.requiredFlag] = source;
            if (!string.IsNullOrEmpty(when.forbiddenFlag) && !usedFlags.ContainsKey(when.forbiddenFlag)) usedFlags[when.forbiddenFlag] = source;
            if (!string.IsNullOrEmpty(when.requiredItem) && !usedItems.ContainsKey(when.requiredItem)) usedItems[when.requiredItem] = source;
            if (!string.IsNullOrEmpty(when.forbiddenItem) && !usedItems.ContainsKey(when.forbiddenItem)) usedItems[when.forbiddenItem] = source;
        };

        foreach (var m in modules ?? Array.Empty<Module>()) addWhen(m.when, "module " + m.mesh);
        foreach (var d in doors ?? Array.Empty<DoorDef>())
        {
            if (!string.IsNullOrEmpty(d.unlockFlag) && !usedFlags.ContainsKey(d.unlockFlag)) usedFlags[d.unlockFlag] = "door " + d.leaf;
        }
        foreach (var b in boxes ?? Array.Empty<BoxDef>()) addWhen(b.when, "box " + b.name);
        foreach (var p in prefabs ?? Array.Empty<PrefabDef>()) addWhen(p.when, "prefab " + p.name);
        foreach (var l in lights ?? Array.Empty<LightDef>()) addWhen(l.when, "light");
        foreach (var s in sounds ?? Array.Empty<SoundDef>()) addWhen(s.when, "sound " + s.name);

        var definedFlags = new HashSet<string>(typeof(GameFlags).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)));

        var definedItems = new HashSet<string>(typeof(GameItems).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)));

        foreach (var kvp in usedFlags.Where(kvp => !definedFlags.Contains(kvp.Key)))
            Debug.LogWarning($"[LabSceneBuilder] {scene}: flag '{kvp.Key}' (used in {kvp.Value}) is not defined in GameFlags");

        foreach (var kvp in usedItems.Where(kvp => !definedItems.Contains(kvp.Key)))
            Debug.LogWarning($"[LabSceneBuilder] {scene}: item '{kvp.Key}' (used in {kvp.Value}) is not defined in GameItems");
    }
}

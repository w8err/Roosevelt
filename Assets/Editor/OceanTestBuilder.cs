using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Builds Water_Test.unity: a procedural ocean grid (OceanGridMesh + Ocean.shader) under the project's
// usual fog and a solid-colour sky, plus roosevelt-70's rowboat (SM_Forest_Boat_Row) with the player
// seated on its thwart. Captures a wide establishing shot, a close side-on shot of the boat, and a shot
// from the seated eye position looking down (the only one of the three that reproduces what the player
// actually sees, rather than the boat from outside) at a few different simulated times — so the wave
// motion, and the boat following it, show up without pressing Play — plus one straight-down orthographic
// shot to check for a tiling lattice an oblique view can hide. Follows the same request-file/menu
// pattern as LabSceneBuilder and CreatureBuilder but is independent of them — this is a standalone
// prototype, not yet wired into the layout JSON pipeline (2d can fold it into a `water` block later if
// the tone lands).
[InitializeOnLoad]
public static class OceanTestBuilder
{
    const string ShaderName = "Roosevelt/Ocean";
    const string MaterialPath = "Assets/Materials/M_Ocean_Test.mat";
    const string ScenePath = "Assets/Scenes/Water_Test.unity";
    const string PlayerPrefabPath = "Assets/Prefabs/Player/PF_Player.prefab";
    const string BoatModelPath = "Assets/Art/Environment/Forest/Meshes/SM_Forest_Boat_Row_100x240x50.fbx";
    const string ForestPropsMaterialPath = "Assets/Art/Environment/Forest/Materials/M_Forest_Props.mat";
    const string SkyboxMaterialPath = "Assets/Art/Environment/Forest/Materials/M_Forest_Skybox_Clear_V1.mat";
    const string SkyboxTexturePath = "Assets/Art/Environment/Forest/Textures/T_Forest_Skybox_Clear_V1.png";
    // roosevelt-70's measured thwart (seat) position, boat-local — origin is the still-water line.
    // Raised from y=0.100 to 0.150 once the floorboards (top at y=0.030) landed, so the seat clears the
    // floor by more than a child's-chair 62mm; seatEyeHeight (BoatInteractable) is unchanged at 0.750.
    static readonly Vector3 SeatLocalPosition = new Vector3(0f, 0.150f, -0.050f);
    // Must match BoatInteractable's own seatEyeHeight default — this is the eye height the seated
    // verification camera stands in for.
    const float SeatEyeHeight = 0.75f;

    // 150m across. Quad size follows OceanSettings' shortest wavelength (currently 7.9m) at ~4
    // samples/wavelength: 150/75 = 2.0m quads, 76x76 = 5,776 vertices, ~11,250 triangles. Re-derive
    // this constant if OceanSettings' shortest wavelength changes — a shorter wavelength needs a finer
    // grid (this project's first wave tuning bottomed out at 3.1m and needed 0.75m quads instead).
    const float Size = 150f;
    const int Segments = 75;

    // Points along the shared _Time.y + _TimeOffset clock (see Ocean.shader's header comment) spaced
    // far enough apart that the captures don't land on near-identical phases of the fastest wave. Used
    // for both the water material's _TimeOffset and BoatBuoyancy.Evaluate(), so every capture shows the
    // boat sitting on the water exactly as it was rendered, not some other moment.
    static readonly float[] CaptureTimeOffsets = { 0f, 2.2f, 4.6f };

    // Pitches the seated eye is captured at, in degrees down from level, with the file suffix each one
    // gets. 45 down is the flooding check (it frames the floorboards); 0 is level with the horizon,
    // which is what the seated player mostly looks at and the only view here that shows the open sea
    // from inside the boat rather than the boat from outside.
    static readonly (float Pitch, string Suffix)[] SeatPitches = { (45f, ""), (0f, "_fwd") };

    // Creating this file (e.g. from an external tool) requests a build without opening the menu.
    const string RequestPath = "Temp/OceanTestBuilder.request";

    static double nextPoll;

    static OceanTestBuilder() => EditorApplication.update += PollRequest;

    [MenuItem("Roosevelt/Water/Build Water Test Scene")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[OceanTestBuilder] cancelled: the open scene was not saved");
            return;
        }
        Build();
    }

    static void PollRequest()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1.0;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(RequestPath)) return;

        File.Delete(RequestPath);
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                Debug.LogWarning("[OceanTestBuilder] request skipped: save the open scene first");
                return;
            }
        }
        AssetDatabase.Refresh();
        Build();
    }

    static void Build()
    {
        var shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"[OceanTestBuilder] shader not found: {ShaderName}");
            return;
        }

        LabSceneBuilder.EnsureFolder("Assets/Materials");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "M_Ocean_Test" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else if (material.shader != shader) material.shader = shader;

        // OceanSettings.Current is the single source of truth for the wave parameters (Assets/Resources/
        // OceanSettings.asset): pushed onto the material here, and read directly by BoatBuoyancy at
        // runtime via OceanWaves, so the two can never silently disagree about a value. Ensured (not
        // just assumed) to exist here rather than trusting OceanSettingsCreator's delayCall alone: that
        // delayCall and this method's own request-file poll are both "run soon after a domain reload"
        // with no ordering guarantee between them, and OceanSettings.Current would otherwise silently
        // fall back to built-in defaults instead of the real (missing) asset.
        OceanSettingsCreator.EnsureExists();
        var settings = OceanSettings.Current;
        for (var i = 0; i < settings.waves.Length; i++)
        {
            var w = settings.waves[i];
            var n = i + 1;
            material.SetVector($"_Wave{n}Direction", w.direction);
            material.SetFloat($"_Wave{n}Wavelength", w.wavelength);
            material.SetFloat($"_Wave{n}Amplitude", w.amplitude);
            material.SetFloat($"_Wave{n}Speed", w.speed);
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera.backgroundColor fallback only: it's what Skybox clear flags render as when
        // RenderSettings.skybox is unset, which only happens below if the skybox material is missing.
        var skyColor = new Color(0.55f, 0.62f, 0.66f);

        // roosevelt-70's clear-sky skybox. The .mat ships with _MainTex empty (its texture's GUID
        // doesn't exist until Unity first imports the PNG, so 70 couldn't wire it up ahead of time) —
        // filled in here rather than left for someone to drag in by hand.
        var skyMat = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterialPath);
        if (skyMat != null)
        {
            if (skyMat.GetTexture("_MainTex") == null)
            {
                var skyTex = AssetDatabase.LoadAssetAtPath<Texture>(SkyboxTexturePath);
                if (skyTex != null)
                {
                    skyMat.SetTexture("_MainTex", skyTex);
                    EditorUtility.SetDirty(skyMat);
                }
                else Debug.LogWarning($"[OceanTestBuilder] {SkyboxTexturePath} not found; skybox will render black");
            }
            RenderSettings.skybox = skyMat;
        }
        else Debug.LogWarning($"[OceanTestBuilder] {SkyboxMaterialPath} not found; falling back to a solid-colour background");

        // Matches the skybox's own horizon colour (#a8b4b4) exactly, per 70 — fog and sky then fade the
        // horizon into the same colour with no seam. The previous fogColor (0.58, 0.65, 0.68) was tuned
        // for the solid-colour background this replaces and left a visible band where the sea met it.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.30f, 0.34f, 0.36f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.659f, 0.706f, 0.706f);
        RenderSettings.fogStartDistance = 40f;
        RenderSettings.fogEndDistance = 140f;

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(0.92f, 0.90f, 0.85f);
        sun.intensity = 1.0f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.LookRotation(new Vector3(-0.35f, -0.6f, -0.7f));

        var water = new GameObject("Ocean");
        var filter = water.AddComponent<MeshFilter>();
        filter.sharedMesh = OceanGridMesh.Build(Size, Size, Segments, Segments);
        water.AddComponent<MeshRenderer>().sharedMaterial = material;

        var boatPosition = new Vector3(0f, 0f, 5f);
        var buoyancy = BuildBoat(boatPosition, out var seat);
        var playerCamera = BuildPlayerOnSeat(seat);

        var cam = new GameObject("Camera_Review").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.backgroundColor = skyColor; // fallback if RenderSettings.skybox ended up unset above
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 300f;
        cam.transform.position = new Vector3(0f, 2.2f, -18f);
        cam.transform.LookAt(new Vector3(0f, 0.3f, 30f));
        cam.fieldOfView = 55f;

        // Close, side-on, near water height: "does the boat sit on the surface" is the thing an
        // establishing shot from far away hides. 3m out, half a metre up, a tight 35 degree FOV — the
        // 2.42m hull fills most of the frame width and the waterline sits at roughly frame center.
        var boatCam = new GameObject("Camera_Boat").AddComponent<Camera>();
        boatCam.clearFlags = CameraClearFlags.Skybox;
        boatCam.backgroundColor = skyColor;
        boatCam.nearClipPlane = 0.05f;
        boatCam.farClipPlane = 300f;
        boatCam.transform.position = boatPosition + new Vector3(3f, 0.5f, 0f);
        boatCam.transform.LookAt(boatPosition + Vector3.up * 0.05f);
        boatCam.fieldOfView = 35f;

        // From the actual seated eye position — reproduces what the player sees, unlike every other
        // camera here which looks at the boat from outside. Tracks the boat's own seat transform each
        // capture (position AND rotation), not a fixed world spot, since the real eye is parented there
        // and tilts with the hull. Captured twice per moment at two pitches (see SeatPitches): the
        // downward one is the flooding check the floorboard fix was verified with, the level one is the
        // only capture in this scene that shows the thing being built — an empty horizon from inside the
        // boat — and is what the tone gets judged on.
        // Starts as a copy of PF_Player's own camera (FOV, clear flags, background, clipping) rather
        // than settings invented here. The earlier version set its own CameraClearFlags.Skybox, which
        // is exactly how this scene shipped a black sky nobody saw in a capture: PF_Player's camera
        // clears to solid black (the lab wants that — LabSceneBuilder's indoor path sets skybox to null
        // and clears to a flat colour), so every review capture showed a sky the player would not get.
        // Reproducing the player's *position* was not enough; the camera's own settings are part of
        // what the player sees. Cinemachine and any post-processing volumes are still not reproduced
        // here, so this is closer to the real view, not identical to it.
        var seatCam = new GameObject("Camera_Seated").AddComponent<Camera>();
        if (playerCamera != null) seatCam.CopyFrom(playerCamera);
        else
        {
            seatCam.clearFlags = CameraClearFlags.Skybox;
            seatCam.backgroundColor = skyColor;
            seatCam.fieldOfView = 60f;
        }
        seatCam.nearClipPlane = 0.02f;
        seatCam.farClipPlane = 300f;

        // Straight down, orthographic, high enough to frame the whole patch: an oblique view can make
        // a regular interference lattice (the bug an earlier iteration had) look like harmless noise.
        // Redo this capture every time the wave parameters change.
        var topCam = new GameObject("Camera_Top").AddComponent<Camera>();
        topCam.orthographic = true;
        topCam.orthographicSize = Size * 0.5f;
        topCam.clearFlags = CameraClearFlags.Skybox;
        topCam.backgroundColor = skyColor;
        topCam.nearClipPlane = 0.05f;
        topCam.farClipPlane = 300f;
        topCam.transform.position = new Vector3(0f, 60f, 0f);
        topCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // Every camera in the scene, the player's included — not just the review cameras built above.
        // PF_Player's camera ships clearing to solid black because that is what the underground lab
        // wants, so an outdoor scene has to opt its cameras into the sky explicitly; LabSceneBuilder's
        // SetupEnvironment does exactly this for the forest, and this scene was missing it (the sky
        // rendered in every review capture and was black in the Game view, because only the review
        // cameras had ever been told about it). FindObjectsInactive.Include because the review cameras
        // are toggled off around each capture.
        if (RenderSettings.skybox != null)
        {
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                camera.clearFlags = CameraClearFlags.Skybox;
        }

        // Saved with all three review cameras off, same as CreatureBuilder's test scenes: the .unity
        // file is for opening and looking at manually, the captures below are separate in-memory
        // renders. The boat is saved at rest (t=0, Evaluate() below runs after the save).
        cam.gameObject.SetActive(false);
        boatCam.gameObject.SetActive(false);
        seatCam.gameObject.SetActive(false);
        topCam.gameObject.SetActive(false);
        buoyancy.Evaluate(Time.timeSinceLevelLoad);
        EditorSceneManager.SaveScene(scene, ScenePath);

        var captures = new List<string>();
        var baseTime = Time.timeSinceLevelLoad;
        cam.gameObject.SetActive(true);
        boatCam.gameObject.SetActive(true);
        seatCam.gameObject.SetActive(true);
        for (var i = 0; i < CaptureTimeOffsets.Length; i++)
        {
            var offset = CaptureTimeOffsets[i];
            material.SetFloat("_TimeOffset", offset);
            buoyancy.Evaluate(baseTime + offset);
            // seat's own world transform already reflects the boat's current bob/tilt (it's a child of
            // the boat) — the camera copies it rather than tracking a fixed world spot, same as the
            // real seated player's eye would.
            seatCam.transform.position = seat.position + seat.up * SeatEyeHeight;
            var suffix = i == 0 ? "" : $"_t{i}";
            captures.Add(LabSceneBuilder.Capture(cam, "Water_Test" + suffix));
            captures.Add(LabSceneBuilder.Capture(boatCam, "Water_Test_boat" + suffix));
            foreach (var (pitch, pitchSuffix) in SeatPitches)
            {
                seatCam.transform.rotation = seat.rotation * Quaternion.Euler(pitch, 0f, 0f);
                captures.Add(LabSceneBuilder.Capture(seatCam, "Water_Test_seated" + pitchSuffix + suffix));
            }
        }
        cam.gameObject.SetActive(false);
        boatCam.gameObject.SetActive(false);
        seatCam.gameObject.SetActive(false);

        material.SetFloat("_TimeOffset", 0f);
        buoyancy.Evaluate(baseTime);
        topCam.gameObject.SetActive(true);
        captures.Add(LabSceneBuilder.Capture(topCam, "Water_Test_top"));
        topCam.gameObject.SetActive(false);

        material.SetFloat("_TimeOffset", 0f); // leave the saved asset at the gameplay default
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        Debug.Log($"[OceanTestBuilder] built {ScenePath}, {filter.sharedMesh.vertexCount} water vertices, capture {string.Join(" ", captures)}");
    }

    // roosevelt-70's FBX if it's there; a box matching the same draft/freeboard/length/beam otherwise
    // (an earlier placeholder had the whole hull below the origin — entirely submerged, since origin is
    // the waterline — which is what actually made the boat-side capture unreadable, not the camera).
    static BoatBuoyancy BuildBoat(Vector3 position, out Transform seat)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(BoatModelPath);
        GameObject boat;
        if (model != null)
        {
            boat = (GameObject)PrefabUtility.InstantiatePrefab(model);
            boat.name = "Boat";

            // Instantiating alone leaves each renderer on whatever the FBX import fell back to (an
            // untextured white default here, since nothing remapped it) — LabSceneBuilder's own
            // "Select(m => SlotMaterial(...))" loop does the equivalent for its own kit meshes; this
            // boat uses one shared material for every slot, so a straight replace is enough.
            var propsMaterial = AssetDatabase.LoadAssetAtPath<Material>(ForestPropsMaterialPath);
            if (propsMaterial != null)
            {
                foreach (var renderer in boat.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => propsMaterial).ToArray();
            }
            else
            {
                Debug.LogWarning($"[OceanTestBuilder] {ForestPropsMaterialPath} not found; boat keeps its default (untextured) material");
            }
        }
        else
        {
            Debug.LogWarning($"[OceanTestBuilder] {BoatModelPath} not found; using a box placeholder");
            boat = BuildBoatBoxPlaceholder();
        }
        boat.transform.position = position;

        var seatGo = new GameObject("SeatPosition");
        seatGo.transform.SetParent(boat.transform, false);
        seatGo.transform.localPosition = SeatLocalPosition;
        seat = seatGo.transform;

        boat.AddComponent<BoatInteractable>(); // RequireComponent pulls in BoatBuoyancy too
        var so = new SerializedObject(boat.GetComponent<BoatInteractable>());
        so.FindProperty("seatPosition").objectReferenceValue = seat;
        // This test scene has no one to walk up and press Interact, and without SetPosed the player's
        // own CharacterController falls under gravity through a sea with no collider (by design — the
        // boat is the only thing anyone stands on). boardOnStart seats them the moment Play starts.
        so.FindProperty("boardOnStart").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        return boat.GetComponent<BoatBuoyancy>();
    }

    // Draft 0.170m below the origin, freeboard 0.270m above (roosevelt-70's measurements): the hull box
    // spans local y -0.17..+0.27, not centered on the origin, so it doesn't repeat the "whole box below
    // the waterline" mistake even as a rough stand-in.
    static GameObject BuildBoatBoxPlaceholder()
    {
        var boat = new GameObject("Boat_Placeholder");
        var hull = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hull.name = "Hull";
        Object.DestroyImmediate(hull.GetComponent<BoxCollider>());
        hull.transform.SetParent(boat.transform, false);
        hull.transform.localPosition = new Vector3(0f, 0.05f, 0f); // midpoint of -0.17..+0.27
        hull.transform.localScale = new Vector3(0.964f, 0.44f, 2.42f);
        var hullMat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { color = new Color(0.32f, 0.22f, 0.15f) };
        hull.GetComponent<MeshRenderer>().sharedMaterial = hullMat;
        return boat;
    }

    // Parented on the seat for visual reference only (does the player look right seated on the boat
    // once it's floating) — this does not go through BoatInteractable.Interact()/SetPosed(), so it is
    // not a test of the actual boarding flow, only of the boat+player's placement and silhouette.
    // Returns the prefab's own Camera so the seated capture below can copy its settings instead of
    // inventing its own (see that capture's comment).
    static Camera BuildPlayerOnSeat(Transform seat)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[OceanTestBuilder] {PlayerPrefabPath} not found; build a lab layout once to create it");
            return null;
        }
        var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, seat);
        player.transform.localPosition = Vector3.zero;
        player.transform.localRotation = Quaternion.identity;
        return player.GetComponentInChildren<Camera>(true);
    }
}

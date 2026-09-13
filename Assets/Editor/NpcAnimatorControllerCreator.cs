using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds one AC_<identity>.controller per rigged figure FBX under Lab/Figures or Forest/Meshes
// (Figure_*): default state Idle (loops on its own clip settings), and — only when that FBX also has
// a Talk clip — a Talk trigger that jumps from any state into Talk with an Exit Time transition back
// to Idle once the clip finishes. CharacterAnimator only ever calls Play(action)/ReturnToIdle(), so a
// mute figure with no Talk clip (the dream Woman/Child) simply never gets that trigger and
// Play("Talk") on it stays the silent no-op it already is.
//
// One controller per figure rather than one shared AC_Npc: each figure's Idle/Talk clips are
// sub-assets of its own FBX, and two figures that each name a clip "Idle" must not collide into the
// same state. ControllerPathFor(meshName) is the single place the AC_<identity> naming rule lives —
// LabSceneBuilder (roosevelt-2d) looks a figure's controller path up through it instead of guessing.
//
// Runs on every editor load (TransitionSettingsCreator does the same for its asset) and again from
// the menu any time a figure FBX changes. Safe to call repeatedly and cheap when nothing changed:
// existing parameters/states/transitions are left alone and the asset is only re-saved when a state,
// trigger or motion was actually added — a still-missing clip leaves a warning, nothing else.
[InitializeOnLoad]
public static class NpcAnimatorControllerCreator
{
    const string ControllerDir = "Assets/Animation/Controllers";
    const string TalkTrigger = "Talk";
    const string IdleState = "Idle";
    const string TalkState = "Talk";
    const string LabFiguresDir = "Assets/Art/Environment/Lab/Figures/Meshes/";
    const string ForestMeshesDir = "Assets/Art/Environment/Forest/Meshes/";

    static NpcAnimatorControllerCreator() => EditorApplication.delayCall += BuildAll;

    [MenuItem("Roosevelt/Characters/Build NPC Animator Controllers")]
    public static void BuildFromMenu() => BuildAll();

    // The one place the AC_<identity> naming rule lives. `meshName` is the FBX asset's own name —
    // Object.name / filename without extension, e.g. "SM_Lab_Figure_Researcher_Standing_50x40x170" —
    // which is what LabSceneBuilder already has on hand for the model it just loaded. Pure path
    // derivation: works whether or not the controller has been built yet.
    public static string ControllerPathFor(string meshName)
    {
        if (string.IsNullOrEmpty(meshName)) return null;
        return $"{ControllerDir}/AC_{Identity(meshName)}.controller";
    }

    // Mirrors FigureModelPostprocessor.IsFigureModel's two target locations (Lab researcher, Forest
    // Figure_* meshes) so both scripts agree on what counts as a figure. Keep in sync if that changes.
    static bool IsFigureModel(string path) =>
        path.Contains(LabFiguresDir) || (path.Contains(ForestMeshesDir) && path.Contains("Figure_"));

    // Strips the "SM_" prefix and a trailing "_WxHxD" size tag so a remodel that only changes
    // dimensions keeps the same controller instead of orphaning it.
    static string Identity(string meshName)
    {
        var name = meshName.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
            ? meshName.Substring(0, meshName.Length - 4) : meshName;
        if (name.StartsWith("SM_")) name = name.Substring(3);
        return Regex.Replace(name, @"_\d+x\d+x\d+$", "");
    }

    static void BuildAll()
    {
        var models = AssetDatabase.FindAssets("t:Model", new[]
                { "Assets/Art/Environment/Lab/Figures/Meshes", "Assets/Art/Environment/Forest/Meshes" })
            .Select(AssetDatabase.GUIDToAssetPath).Distinct()
            .Where(IsFigureModel).OrderBy(p => p).ToArray();
        if (models.Length == 0) return;

        LabSceneBuilder.EnsureFolder(ControllerDir);
        foreach (var modelPath in models) BuildOne(modelPath);
    }

    static void BuildOne(string modelPath)
    {
        var meshName = Path.GetFileNameWithoutExtension(modelPath);
        var controllerPath = ControllerPathFor(meshName);
        var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().ToList();
        var idleClip = clips.FirstOrDefault(c => c.name == IdleState);
        var talkClip = clips.FirstOrDefault(c => c.name == TalkState);

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        var isNew = controller == null;
        if (isNew) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var changed = isNew;

        var sm = controller.layers[0].stateMachine;
        var idle = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == IdleState);
        if (idle == null)
        {
            idle = sm.AddState(IdleState);
            sm.defaultState = idle;
            changed = true;
        }
        if (idle.motion == null && idleClip != null)
        {
            idle.motion = idleClip;
            changed = true;
        }
        if (idle.motion == null)
            Debug.LogWarning($"[NpcAnimatorControllerCreator] {meshName}: no '{IdleState}' clip yet; Idle state left without a motion.");

        if (talkClip != null)
        {
            if (!controller.parameters.Any(p => p.name == TalkTrigger && p.type == AnimatorControllerParameterType.Trigger))
            {
                controller.AddParameter(TalkTrigger, AnimatorControllerParameterType.Trigger);
                changed = true;
            }
            var talk = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == TalkState);
            if (talk == null)
            {
                talk = sm.AddState(TalkState);
                changed = true;
            }
            if (talk.motion == null)
            {
                talk.motion = talkClip;
                changed = true;
            }

            // From any state (not just Idle) so a conversation running longer than one Talk clip can
            // retrigger it mid-gesture; NpcInteractable does that on a fixed interval.
            if (!sm.anyStateTransitions.Any(t => t.destinationState == talk))
            {
                var toTalk = sm.AddAnyStateTransition(talk);
                toTalk.hasExitTime = false;
                toTalk.duration = 0.1f;
                toTalk.canTransitionToSelf = true;
                toTalk.AddCondition(AnimatorConditionMode.If, 0f, TalkTrigger);
                changed = true;
            }
            if (!talk.transitions.Any(t => t.destinationState == idle))
            {
                var toIdle = talk.AddTransition(idle);
                toIdle.hasExitTime = true;
                toIdle.exitTime = 1f;
                toIdle.hasFixedDuration = true;
                toIdle.duration = 0.15f;
                changed = true;
            }
        }
        // No Talk clip on this figure (e.g. the dream Woman/Child): left Idle-only. CharacterAnimator's
        // Play("Talk") is already a silent no-op when the controller has no such trigger, so there is
        // nothing else to wire up — a mute figure simply never gets one.

        if (!changed) return;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[NpcAnimatorControllerCreator] {(isNew ? "created" : "updated")} {controllerPath} for {meshName}"
            + (talkClip != null ? " (Idle+Talk)" : " (Idle only)"));
    }
}

using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds Assets/Animation/Controllers/AC_Npc.controller: default state Idle (loops on its own
// clip settings), a Talk trigger that jumps from any state into Talk, and an Exit Time transition
// back to Idle once the Talk clip finishes. CharacterAnimator only ever calls Play("Talk") /
// ReturnToIdle(), so more states can be added here later without touching call sites.
//
// Runs once automatically when the editor loads (TransitionSettingsCreator does the same for its
// asset) and again from the menu any time roosevelt-70's researcher FBX changes. Safe to call
// repeatedly: existing parameters/states/transitions are left alone, and a state's motion is only
// filled in when it is still empty or the clip it names has changed.
[InitializeOnLoad]
public static class NpcAnimatorControllerCreator
{
    const string ControllerDir = "Assets/Animation/Controllers";
    const string ControllerPath = ControllerDir + "/AC_Npc.controller";
    const string TalkTrigger = "Talk";
    const string IdleState = "Idle";
    const string TalkState = "Talk";
    // Every Lab figure FBX under here shares the same skeleton and this one controller.
    static readonly string[] ClipSearchDirs = { "Assets/Art/Environment/Lab/Figures/Meshes" };

    static NpcAnimatorControllerCreator() => EditorApplication.delayCall += BuildIfMissing;

    [MenuItem("Roosevelt/Characters/Build NPC Animator Controller")]
    public static void BuildFromMenu() => Build();

    static void BuildIfMissing()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        // Also rebuilds when the controller already exists but Idle/Talk still has no motion, so a
        // researcher FBX that lands its clips after the controller was first created gets picked up
        // on the next editor load instead of staying empty until someone remembers the menu item.
        if (controller != null && !HasEmptyMotion(controller)) return;
        Build();
    }

    static bool HasEmptyMotion(AnimatorController controller) =>
        controller.layers[0].stateMachine.states
            .Select(s => s.state)
            .Any(s => (s.name == IdleState || s.name == TalkState) && s.motion == null);

    static void Build()
    {
        LabSceneBuilder.EnsureFolder(ControllerDir);

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var isNew = controller == null;
        if (isNew) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        if (!controller.parameters.Any(p => p.name == TalkTrigger && p.type == AnimatorControllerParameterType.Trigger))
            controller.AddParameter(TalkTrigger, AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;
        var idle = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == IdleState);
        if (idle == null)
        {
            idle = sm.AddState(IdleState);
            sm.defaultState = idle;
        }
        var talk = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == TalkState);
        if (talk == null) talk = sm.AddState(TalkState);

        // From any state (not just Idle) so a conversation running longer than one Talk clip can
        // retrigger it mid-gesture; NpcInteractable does that on a fixed interval.
        if (!sm.anyStateTransitions.Any(t => t.destinationState == talk))
        {
            var toTalk = sm.AddAnyStateTransition(talk);
            toTalk.hasExitTime = false;
            toTalk.duration = 0.1f;
            toTalk.canTransitionToSelf = true;
            toTalk.AddCondition(AnimatorConditionMode.If, 0f, TalkTrigger);
        }
        if (!talk.transitions.Any(t => t.destinationState == idle))
        {
            var toIdle = talk.AddTransition(idle);
            toIdle.hasExitTime = true;
            toIdle.exitTime = 1f;
            toIdle.hasFixedDuration = true;
            toIdle.duration = 0.15f;
        }

        var linked = 0;
        idle.motion = LinkClip(IdleState, idle.motion, ref linked);
        talk.motion = LinkClip(TalkState, talk.motion, ref linked);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[NpcAnimatorControllerCreator] {(isNew ? "created" : "checked")} {ControllerPath}, {linked} clip(s) linked");
    }

    // Finds an AnimationClip named `clipName` among the figure FBX sub-assets. Returns `current`
    // unchanged (which may be null) when none exists yet — the state stays without a motion rather
    // than failing the whole build.
    static Motion LinkClip(string clipName, Motion current, ref int linked)
    {
        var clip = AssetDatabase.FindAssets("t:AnimationClip", ClipSearchDirs)
            .Select(AssetDatabase.GUIDToAssetPath).Distinct()
            .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c.name == clipName);
        if (clip == null)
        {
            if (current == null)
                Debug.LogWarning($"[NpcAnimatorControllerCreator] no '{clipName}' clip found yet under "
                    + $"{string.Join(", ", ClipSearchDirs)}; {clipName} state left without a motion.");
            return current;
        }
        linked++;
        return clip;
    }
}

using System.Collections.Generic;
using UnityEngine;

// Thin wrapper over an Animator: every caller only ever uses Play(action)/ReturnToIdle(), so the
// state machine behind AC_Npc.controller can grow (Talk today, walking/reactions later) without
// touching call sites. Missing Animator, missing controller, or a trigger the controller doesn't
// define yet are all silently ignored — an NPC whose rig or clips aren't finished must not throw.
public class CharacterAnimator : MonoBehaviour
{
    Animator animator;
    readonly HashSet<string> triggers = new HashSet<string>();

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var param in animator.parameters)
            if (param.type == AnimatorControllerParameterType.Trigger) triggers.Add(param.name);
    }

    public void Play(string action)
    {
        if (animator != null && triggers.Contains(action)) animator.SetTrigger(action);
    }

    // Jumps straight to Idle regardless of the state machine's own transitions (Talk's Exit Time
    // included) — used when a conversation ends earlier or later than a single Talk clip runs.
    public void ReturnToIdle()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        // A SetTrigger left unconsumed (e.g. Play("Talk") called right as the conversation ends)
        // stays armed and would fire the Any State -> Talk transition again on the very next frame,
        // undoing the Play("Idle") below. Clear every trigger this controller defines, not just Talk,
        // so a future state added here can't reintroduce the same bug.
        foreach (var trigger in triggers) animator.ResetTrigger(trigger);
        animator.Play("Idle", 0, 0f);
    }
}

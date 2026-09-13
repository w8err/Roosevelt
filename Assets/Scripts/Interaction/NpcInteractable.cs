using System.Collections;
using UnityEngine;

// An NPC the player can talk to. LabSceneBuilder places these and fills `dialogue` and the
// collider's size; walking the conversation itself is Dialogue's job.
// CharacterFacing isn't attached by LabSceneBuilder (unlike CharacterAnimator), so it's required
// here instead: Unity adds it automatically the first time an NpcInteractable does.
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(CharacterFacing))]
public class NpcInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] DialogueData dialogue;
    [SerializeField] string prompt = "대화하기";
    // AC_Npc's Talk clip plays once (~2-3s) and returns to Idle by itself; a conversation with
    // several nodes runs longer than that, so it gets retriggered on this cadence for as long as
    // Dialogue keeps talking instead of the NPC freezing back into Idle mid-conversation.
    [SerializeField] float talkGestureInterval = 2.5f;

    Coroutine gestureLoop;

    public string GetPrompt() => prompt;
    public bool CanInteract() => dialogue != null && !Dialogue.IsPlaying && !ScreenTransition.IsRunning;

    public void Interact(PlayerInteraction player)
    {
        if (!Dialogue.Play(dialogue, OnDialogueFinished)) return;
        GetComponent<CharacterFacing>()?.FaceTarget(player.transform);
        // Resolved here rather than cached in Awake: LabSceneBuilder adds CharacterAnimator to
        // this GameObject after NpcInteractable, so an Awake-time lookup can miss it.
        var animator = GetComponent<CharacterAnimator>();
        if (animator == null) return;
        animator.Play("Talk");
        gestureLoop = StartCoroutine(RepeatTalkGesture(animator));
    }

    IEnumerator RepeatTalkGesture(CharacterAnimator animator)
    {
        while (Dialogue.IsPlaying)
        {
            yield return new WaitForSeconds(talkGestureInterval);
            if (Dialogue.IsPlaying) animator.Play("Talk");
        }
    }

    void OnDialogueFinished()
    {
        if (gestureLoop != null)
        {
            StopCoroutine(gestureLoop);
            gestureLoop = null;
        }
        GetComponent<CharacterAnimator>()?.ReturnToIdle();
        GetComponent<CharacterFacing>()?.ReturnToRest();
    }
}

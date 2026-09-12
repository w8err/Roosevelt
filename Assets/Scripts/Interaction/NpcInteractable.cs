using UnityEngine;

// An NPC the player can talk to. LabSceneBuilder places these and fills `dialogue` and the
// collider's size; walking the conversation itself is Dialogue's job.
[RequireComponent(typeof(CapsuleCollider))]
public class NpcInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] DialogueData dialogue;
    [SerializeField] string prompt = "대화하기";

    public string GetPrompt() => prompt;
    public bool CanInteract() => dialogue != null && !Dialogue.IsPlaying && !ScreenTransition.IsRunning;
    public void Interact(PlayerInteraction player) => Dialogue.Play(dialogue);
}

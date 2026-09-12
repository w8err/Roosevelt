using UnityEngine;

// The bed the player sleeps in to enter tonight's dream. Locked behind requiredFlag (the
// lobby NPC conversation, by default) until then.
public class BedInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] string requiredFlag = GameFlags.TalkedToLobbyNPC;
    [SerializeField] string prompt = "잠자기";
    [SerializeField] string notReadyPrompt = "아직 잠이 오지 않는다";

    bool Ready => string.IsNullOrEmpty(requiredFlag) || GameState.HasFlag(requiredFlag);

    public string GetPrompt() => Ready ? prompt : notReadyPrompt;
    public bool CanInteract() => Ready && !ScreenTransition.IsRunning && !Dialogue.IsPlaying;
    public void Interact(PlayerInteraction player) => GameFlow.EnterDream();
}

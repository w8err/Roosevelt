using UnityEngine;

// A door that moves the player between rooms in the same scene: no swing, just a
// screen-black teleport to `destination`. LabSceneBuilder fills `destination` and
// `locked` per placed door via SerializedObject, so their field names stay as-is.
public class TransitionDoor : MonoBehaviour, IInteractable
{
    const string LockedPrompt = "잠겨 있다";

    [SerializeField] Transform destination;
    [SerializeField] bool locked;
    [Tooltip("If set, the door also needs this GameState flag before it can be used.")]
    [SerializeField] string requiredFlag;
    [SerializeField] string prompt = "열기";

    bool Unlocked => !locked && (string.IsNullOrEmpty(requiredFlag) || GameState.HasFlag(requiredFlag));

    public string GetPrompt() => Unlocked ? prompt : LockedPrompt;
    public bool CanInteract() => Unlocked && !ScreenTransition.IsRunning;

    public void Interact(PlayerInteraction player) =>
        ScreenTransition.Teleport(player.GetComponent<FirstPersonController>(), destination);
}

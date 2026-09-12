// Anything the player can aim at and use with the Interact key: doors, people, the bed, the chair.
// PlayerInteraction finds it on the hit collider or any of its parents.
public interface IInteractable
{
    // Short label under the crosshair, e.g. "열기". Still shown while CanInteract is false ("잠겨 있다").
    string GetPrompt();

    // False keeps the prompt on screen but ignores the key.
    bool CanInteract();

    void Interact(PlayerInteraction player);
}

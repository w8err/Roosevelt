using UnityEngine;

// Lives on the hinge pivot; the leaf and its handle are children offset from it.
public class LabDoor : MonoBehaviour, IInteractable
{
    const string OpenPrompt = "열기", ClosePrompt = "닫기", LockedPrompt = "잠겨 있다";

    [SerializeField] float openAngle = 90f;
    [SerializeField] float swingSeconds = 0.8f;
    [SerializeField] bool locked;

    bool isOpen;
    float progress;

    // A cabinet door gates what is behind it: StorageBay keeps its shelf sockets locked until this
    // says otherwise, so a vial cannot be taken through a shut door.
    public bool IsOpen => isOpen;
    public event System.Action Changed;

    public string GetPrompt() => locked ? LockedPrompt : isOpen ? ClosePrompt : OpenPrompt;
    public bool CanInteract() => !locked;

    public void Interact(PlayerInteraction player)
    {
        isOpen = !isOpen;
        Changed?.Invoke();
    }

    void Update()
    {
        var target = isOpen ? 1f : 0f;
        if (progress == target) return;
        progress = Mathf.MoveTowards(progress, target, Time.deltaTime / swingSeconds);
        transform.localRotation = Quaternion.Euler(0f, Mathf.SmoothStep(0f, openAngle, progress), 0f);
    }
}

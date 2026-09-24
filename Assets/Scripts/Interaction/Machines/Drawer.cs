using UnityEngine;

// A drawer that slides out of a cabinet or a cart. The sibling of LabDoor: same open/close toggle,
// same lock, same Changed event, so StorageBay governs what is inside either one without caring
// which it is. Only the motion differs -- a hinge turns, this travels.
//
// The kit puts the drawer's origin at its closed position and tells us which way it pulls, so the
// mesh needs no animation of its own.
public class Drawer : MonoBehaviour, IInteractable
{
    const string OpenPrompt = "열기", ClosePrompt = "닫기", LockedPrompt = "잠겨 있다";

    [Tooltip("The sliding mesh. Falls back to this transform, which is where the kit puts it.")]
    [SerializeField] Transform panel;
    [Tooltip("Where the drawer sits fully open, in its own local space, relative to shut.")]
    [SerializeField] Vector3 openOffset = new Vector3(0f, 0f, 0.34f);
    [SerializeField] float slideSeconds = 0.45f;
    [SerializeField] bool locked;

    bool isOpen;
    float progress;
    Vector3 closedPosition;

    // StorageBay reads this to decide whether the slots inside are reachable.
    public bool IsOpen => isOpen;
    public event System.Action Changed;

    void Awake()
    {
        if (panel == null) panel = transform;
        closedPosition = panel.localPosition;
    }

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
        progress = Mathf.MoveTowards(progress, target, Time.deltaTime / Mathf.Max(0.01f, slideSeconds));
        // Smoothed rather than linear, so a drawer eases to a stop instead of arriving at full speed
        // and appearing to hit something.
        panel.localPosition = closedPosition + openOffset * Mathf.SmoothStep(0f, 1f, progress);
    }

    public void SetLocked(bool value) => locked = value;

    // Shut by something other than the player: the cart closes its drawers before it will roll.
    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        Changed?.Invoke();
    }
}

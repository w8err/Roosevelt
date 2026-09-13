using UnityEngine;

// Boards or leaves this boat. Boarding parents the player under `seatPosition` (the thwart, per
// roosevelt-70's boat: local (0, 0.150, -0.050) — raised from 0.100 once the floorboards landed, so the
// seat clears the floor by more than a child's-chair 62mm) so BoatBuoyancy's per-frame bob/tilt carries the
// player along for free (no per-frame follow code, no CharacterController fight: SetPosed(true, ...)
// disables the CharacterController for the duration), and puts FirstPersonController into posed mode
// WITHOUT acquiring InputLock. That combination already exists and already does exactly "movement
// locked, look free": FirstPersonController.Update() only gates Look() on InputLock and only gates
// Move() on posed, so leaving InputLock alone here is what keeps the camera free — the dread of an
// endless sea is "you can't go anywhere," not "you can't see anything." No change to
// FirstPersonController was needed or made; every existing InputLock use (dialogue, doors, chairs)
// behaves exactly as before.
[RequireComponent(typeof(BoatBuoyancy))]
public class BoatInteractable : MonoBehaviour, IInteractable
{
    [Tooltip("The thwart (seat) the player is parented to; local position/rotation reset to identity here.")]
    [SerializeField] Transform seatPosition;
    [Tooltip("Eye height above seatPosition once seated, not above the water. roosevelt-70's boat: seat at " +
        "local y=0.150, seated eye at y=0.900, so 0.750 above the seat itself (unchanged when the seat was " +
        "raised for the floorboards) — deliberately low: eyes barely over the 0.27m freeboard is the point " +
        "(\"trapped\", not \"a vantage point\").")]
    [SerializeField] float seatEyeHeight = 0.75f;
    [SerializeField] string boardPrompt = "배에 타기";
    [SerializeField] string leavePrompt = "배에서 내리기";
    [Tooltip("Boards whatever FirstPersonController is in the scene the moment this starts, instead of " +
        "waiting for Interact() — for a test/review scene with no one to walk up and press the key. " +
        "Leave off for a real placement; Interact() still boards/leaves normally either way.")]
    [SerializeField] bool boardOnStart;

    FirstPersonController rider;
    Transform originalParent;

    public string GetPrompt() => rider == null ? boardPrompt : leavePrompt;
    public bool CanInteract() => !ScreenTransition.IsRunning && !Dialogue.IsPlaying;

    void Start()
    {
        if (!boardOnStart) return;
        var controller = Object.FindAnyObjectByType<FirstPersonController>();
        if (controller == null)
        {
            Debug.LogWarning($"[BoatInteractable] {name}: boardOnStart set but no FirstPersonController found");
            return;
        }
        Board(controller);
    }

    public void Interact(PlayerInteraction player)
    {
        if (rider == null) Board(player.GetComponent<FirstPersonController>());
        else Leave();
    }

    void Board(FirstPersonController controller)
    {
        if (controller == null || seatPosition == null) return;
        rider = controller;
        originalParent = controller.transform.parent;

        controller.transform.SetParent(seatPosition, false);
        controller.transform.localPosition = Vector3.zero;
        controller.transform.localRotation = Quaternion.identity;
        controller.SetPosed(true, seatEyeHeight);
    }

    void Leave()
    {
        if (rider == null) return;
        rider.transform.SetParent(originalParent, true);
        rider.SetPosed(false);
        rider = null;
    }
}

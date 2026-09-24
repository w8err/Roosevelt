using UnityEngine;

// A part the player picks up and walks somewhere: a sample vial, a fuse, a punch card.
// Taking one fills CarryHands, and a full pair of hands refuses the next part rather than
// swapping, so every procedure step is one trip.
public class Carryable : MonoBehaviour, IInteractable
{
    [Tooltip("Which Sockets will accept this. Use the MachineParts constants.")]
    [SerializeField] string partId = MachineParts.Sample;
    [Tooltip("Shown under the crosshair, e.g. \"검체 용기\".")]
    [SerializeField] string displayName = "부품";

    public string PartId => partId;
    public string DisplayName => displayName;

    // The socket currently holding this, or null while carried or resting in the world.
    public Socket Holder { get; private set; }

    public string GetPrompt()
    {
        var hands = FindAnyObjectByType<CarryHands>();
        if (hands != null && hands.IsFull)
            return hands.Held == this ? $"{displayName} (들고 있다)" : "손이 차 있다";
        return $"{displayName} 집기";
    }

    // Held parts sit under the camera, which PlayerInteraction's raycast skips, so this only
    // runs for parts in the world or in a socket.
    public bool CanInteract()
    {
        var hands = FindAnyObjectByType<CarryHands>();
        return hands != null && !hands.IsFull;
    }

    public void Interact(PlayerInteraction player)
    {
        var hands = player.GetComponent<CarryHands>();
        if (hands == null || hands.IsFull) return;
        // Leaving the socket before the hands take it keeps the socket's own state consistent
        // even if Take() refuses for a reason added later.
        if (Holder != null) Holder.Eject();
        hands.Take(this);
    }

    // Called by CarryHands and Socket; the caller owns where this ends up.
    public void AttachTo(Transform mount)
    {
        Holder = null;
        transform.SetParent(mount, false);
        transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    public void AttachToSocket(Socket socket, Transform mount)
    {
        AttachTo(mount);
        Holder = socket;
    }
}

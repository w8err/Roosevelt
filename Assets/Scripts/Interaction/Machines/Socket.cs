using UnityEngine;

// A named slot on a machine that holds one Carryable: the analyzer's sample port, the run
// console's compound port, the workbench that accepts anything so the player can always set a
// part down. A locked socket keeps its prompt but refuses the key, which is how ToggleLever
// gates it (the vice in The Other Side did the same job).
public class Socket : MonoBehaviour, IInteractable
{
    [Tooltip("PartId this accepts. Leave empty to accept anything (a bench or shelf).")]
    [SerializeField] string acceptedPartId = "";
    [Tooltip("Where a mounted part sits. Falls back to this transform.")]
    [SerializeField] Transform mountPoint;
    [Tooltip("Shown when the socket is empty and the player's hands are too, e.g. \"검체 투입구\".")]
    [SerializeField] string displayName = "투입구";
    [Tooltip("A part already in the machine can be locked in place while it runs.")]
    [SerializeField] bool startsLocked;

    bool locked;

    public Carryable Mounted { get; private set; }
    public bool IsEmpty => Mounted == null;
    public bool Locked => locked;

    // Fires after anything mounts or unmounts, so machines can re-read their own readiness.
    public event System.Action Changed;

    void Awake()
    {
        locked = startsLocked;
        if (mountPoint == null) mountPoint = transform;

        // A scene can start with something already in the slot: the vials the fridge is stocked
        // with. The builder parents them here and this adopts them, so a starting part needs no
        // separate placement path and sits exactly where a part the player put there would.
        if (Mounted == null)
        {
            var resident = GetComponentInChildren<Carryable>(true);
            if (resident != null)
            {
                Mounted = resident;
                resident.AttachToSocket(this, mountPoint);
            }
        }
    }

    public void SetLocked(bool value)
    {
        if (locked == value) return;
        locked = value;
        Changed?.Invoke();
    }

    public bool Accepts(Carryable part) =>
        part != null && (string.IsNullOrEmpty(acceptedPartId) || part.PartId == acceptedPartId);

    public string GetPrompt()
    {
        if (locked) return Mounted != null ? $"{Mounted.DisplayName} (고정됨)" : $"{displayName} (잠김)";
        if (Mounted != null) return $"{Mounted.DisplayName} 빼기";
        var hands = FindAnyObjectByType<CarryHands>();
        if (hands == null || !hands.IsFull) return $"{displayName} (비어 있다)";
        return Accepts(hands.Held) ? $"{hands.Held.DisplayName} 넣기" : $"{displayName}: 맞지 않는다";
    }

    public bool CanInteract()
    {
        if (locked) return false;
        var hands = FindAnyObjectByType<CarryHands>();
        if (hands == null) return false;
        return hands.IsFull ? IsEmpty && Accepts(hands.Held) : Mounted != null;
    }

    public void Interact(PlayerInteraction player)
    {
        var hands = player.GetComponent<CarryHands>();
        if (hands == null || locked) return;

        if (hands.IsFull)
        {
            if (!IsEmpty || !Accepts(hands.Held)) return;
            Mounted = hands.Release();
            Mounted.AttachToSocket(this, mountPoint);
        }
        else
        {
            if (Mounted == null) return;
            var part = Mounted;
            Mounted = null;
            hands.Take(part);
        }
        Changed?.Invoke();
    }

    // Used when a part is taken straight out of the socket by aiming at the part itself.
    public void Eject()
    {
        if (Mounted == null) return;
        Mounted = null;
        Changed?.Invoke();
    }

    // Machines that produce a part (the mixer's compound) drop it in directly.
    public void MountDirect(Carryable part)
    {
        if (part == null || !IsEmpty) return;
        Mounted = part;
        part.AttachToSocket(this, mountPoint);
        Changed?.Invoke();
    }
}

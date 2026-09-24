using UnityEngine;

// The player's one pair of hands. Exactly one Carryable at a time: picking something up while
// already holding something is refused rather than swapped, because the whole point of the
// machine procedures is that carrying a part costs you the ability to carry anything else.
// Lives on PF_Player next to PlayerInteraction.
public class CarryHands : MonoBehaviour
{
    [Tooltip("Where a held part sits, in the camera's local space: right, down and forward from the eye.")]
    [SerializeField] Vector3 holdOffset = new Vector3(0.28f, -0.22f, 0.45f);
    [Tooltip("Euler angles applied to a held part so it reads as inspected rather than axis-aligned.")]
    [SerializeField] Vector3 holdRotation = new Vector3(12f, -25f, 6f);
    [Tooltip("Walk speed multiplier while carrying. Carrying should feel like a commitment.")]
    [Range(0.2f, 1f)] [SerializeField] float carrySpeedScale = 0.72f;

    Transform hand;
    FirstPersonController controller;

    public Carryable Held { get; private set; }
    public bool IsFull => Held != null;
    public float SpeedScale => IsFull ? carrySpeedScale : 1f;

    void Awake()
    {
        controller = GetComponent<FirstPersonController>();

        // Parented to the rendering camera so the part follows the walk bob and hand sway that
        // PlayerCameraFeel puts on the view; a part pinned to the body would slide against it.
        var eye = GetComponentInChildren<Camera>();
        hand = new GameObject("CarryHand").transform;
        hand.SetParent(eye != null ? eye.transform : transform, false);
        hand.localPosition = holdOffset;
        hand.localEulerAngles = holdRotation;
    }

    // True if the part was taken. False means the hands were already full.
    public bool Take(Carryable part)
    {
        if (IsFull || part == null) return false;
        Held = part;
        part.AttachTo(hand);
        ApplyEncumbrance();
        return true;
    }

    // Hands the part over to a socket or the world; the caller decides where it lands.
    public Carryable Release()
    {
        var part = Held;
        Held = null;
        ApplyEncumbrance();
        return part;
    }

    void ApplyEncumbrance()
    {
        if (controller != null) controller.SpeedScale = SpeedScale;
    }
}

using UnityEngine;

// The clunk that starts a machine or clamps a socket shut. Owners set Enabled to false to refuse
// the pull while keeping the prompt on screen, which is how a machine says "not yet" without a
// message: the player sees the lever, aims at it, and the key does nothing.
public class ToggleLever : MonoBehaviour, IInteractable
{
    [Tooltip("The separated handle mesh. Its own origin must sit on the hinge.")]
    [SerializeField] Transform handle;
    [Tooltip("Handle pitch around local X when off, then on.")]
    [SerializeField] float offAngle = -25f;
    [SerializeField] float onAngle = 25f;
    [SerializeField] float throwDuration = 0.18f;
    [SerializeField] string displayName = "레버";
    [Tooltip("Prompt suffix while Enabled is false, e.g. \"(준비 안 됨)\".")]
    [SerializeField] string blockedSuffix = "(아직 아니다)";
    [Tooltip("False for a lever that latches on and cannot be pulled back.")]
    [SerializeField] bool canTurnOff = true;

    float shownAngle;

    public bool IsOn { get; private set; }
    // Owners gate the pull without hiding the lever.
    public bool Enabled { get; set; } = true;
    public event System.Action Changed;

    void Awake()
    {
        if (handle == null) handle = transform;
        shownAngle = offAngle;
        ApplyAngle();
    }

    void Update()
    {
        var target = IsOn ? onAngle : offAngle;
        if (Mathf.Approximately(shownAngle, target)) return;
        var speed = throwDuration > 0f ? Mathf.Abs(onAngle - offAngle) / throwDuration : float.MaxValue;
        shownAngle = Mathf.MoveTowards(shownAngle, target, speed * Time.deltaTime);
        ApplyAngle();
    }

    void ApplyAngle()
    {
        var euler = handle.localEulerAngles;
        handle.localEulerAngles = new Vector3(shownAngle, euler.y, euler.z);
    }

    public string GetPrompt()
    {
        if (!Enabled) return $"{displayName} {blockedSuffix}";
        if (IsOn && !canTurnOff) return $"{displayName} (내려가 있다)";
        return IsOn ? $"{displayName} 올리기" : $"{displayName} 내리기";
    }

    public bool CanInteract() => Enabled && (!IsOn || canTurnOff);

    public void Interact(PlayerInteraction player)
    {
        IsOn = !IsOn;
        Changed?.Invoke();
    }

    // Machines snap the lever back themselves when a run ends.
    public void ForceOff()
    {
        if (!IsOn) return;
        IsOn = false;
        Changed?.Invoke();
    }
}

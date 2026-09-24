using UnityEngine;

// A numbered knob on a console: each use advances one step and wraps. The value is never shown
// on the HUD, only by the knob's own mark and the machine's readout, so setting three dials from
// a wall chart is a job for the player's memory rather than the interface.
public class RotaryDial : MonoBehaviour, IInteractable
{
    [Tooltip("The separated knob mesh. Its own origin must sit on the turning axis.")]
    [SerializeField] Transform knob;
    [Tooltip("Positions on the dial. 10 gives a single digit, 0-9.")]
    [SerializeField] int steps = 10;
    [Tooltip("Label under the crosshair, e.g. \"A 다이얼\".")]
    [SerializeField] string displayName = "다이얼";
    [Tooltip("Degrees the knob turns for one step, around its local Z.")]
    [SerializeField] float degreesPerStep = 36f;
    [Tooltip("Seconds for the knob to settle into the new position.")]
    [SerializeField] float turnDuration = 0.12f;

    float shownAngle;
    float targetAngle;

    public int Value { get; private set; }
    public int Steps => steps;
    public event System.Action Changed;

    void Awake()
    {
        if (knob == null) knob = transform;
        shownAngle = targetAngle = 0f;
    }

    void Update()
    {
        if (Mathf.Approximately(shownAngle, targetAngle)) return;
        // MoveTowards on a fixed duration rather than a spring: a knob that overshoots reads as
        // loose, and this one is meant to feel detented.
        var speed = turnDuration > 0f ? degreesPerStep / turnDuration : float.MaxValue;
        shownAngle = Mathf.MoveTowards(shownAngle, targetAngle, speed * Time.deltaTime);
        var euler = knob.localEulerAngles;
        knob.localEulerAngles = new Vector3(euler.x, euler.y, shownAngle);
    }

    public string GetPrompt() => $"{displayName}: {Value}";
    public bool CanInteract() => true;

    public void Interact(PlayerInteraction player)
    {
        Value = (Value + 1) % Mathf.Max(1, steps);
        // The angle keeps climbing instead of wrapping, so the knob turns forward past 9 → 0
        // rather than spinning backwards the long way.
        targetAngle += degreesPerStep;
        Changed?.Invoke();
    }

    public void ResetTo(int value)
    {
        Value = Mathf.Clamp(value, 0, Mathf.Max(0, steps - 1));
        Changed?.Invoke();
    }
}

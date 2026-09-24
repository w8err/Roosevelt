using UnityEngine;

// A momentary button: commit the dials, reboot the terminal. Fires once per press and springs back.
public class PushButton : MonoBehaviour, IInteractable
{
    [Tooltip("The separated plunger mesh, pushed along its local -Z while held down.")]
    [SerializeField] Transform plunger;
    [SerializeField] float travel = 0.012f;
    [SerializeField] float springBackDuration = 0.14f;
    [SerializeField] string displayName = "버튼";

    Vector3 restPosition;
    float depressed;

    public bool Enabled { get; set; } = true;
    public event System.Action Pressed;

    void Awake()
    {
        if (plunger == null) plunger = transform;
        restPosition = plunger.localPosition;
    }

    void Update()
    {
        if (depressed <= 0f) return;
        depressed = Mathf.MoveTowards(depressed, 0f, Time.deltaTime / Mathf.Max(0.01f, springBackDuration));
        plunger.localPosition = restPosition - new Vector3(0f, 0f, travel * depressed);
    }

    public string GetPrompt() => Enabled ? $"{displayName} 누르기" : $"{displayName} (반응 없다)";
    public bool CanInteract() => Enabled;

    public void Interact(PlayerInteraction player)
    {
        depressed = 1f;
        plunger.localPosition = restPosition - new Vector3(0f, 0f, travel);
        Pressed?.Invoke();
    }
}

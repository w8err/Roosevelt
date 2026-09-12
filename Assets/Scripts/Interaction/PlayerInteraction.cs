using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Finds the IInteractable the player is looking at, shows its prompt under a small crosshair
// and uses it on Interact. Everything hides while InputLock is held.
// The HUD canvas is built at runtime, so scenes need no UI of their own.
[RequireComponent(typeof(FirstPersonController))]
public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] float interactDistance = 2f;
    [Tooltip("Dot at screen center: faint while idle, bright over something usable.")]
    [SerializeField] bool showCrosshair = true;

    [Header("Colors")]
    [SerializeField] Color readyColor = Color.white;
    [Tooltip("Something that can't be used right now, like a locked door.")]
    [SerializeField] Color blockedColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] Color idleCrosshairColor = new Color(1f, 1f, 1f, 0.25f);

    FirstPersonController controller;
    GameObject hud;
    Image crosshair;
    Text prompt;
    string keyLabel;

    // What the player is aiming at this frame, or null.
    public IInteractable Target { get; private set; }

    void Awake()
    {
        controller = GetComponent<FirstPersonController>();
        BuildHud();
    }

    // FirstPersonController looks up its actions in Awake, so the key label waits until Start.
    void Start()
    {
        keyLabel = controller.InteractAction.GetBindingDisplayString(
            InputBinding.MaskByGroup("Keyboard&Mouse"), InputBinding.DisplayStringOptions.DontIncludeInteractions);
        if (string.IsNullOrEmpty(keyLabel)) keyLabel = "E";
    }

    void Update()
    {
        Target = InputLock.IsLocked ? null : FindTarget();
        // The template gives Interact a Hold interaction; WasPressedThisFrame reacts on press regardless.
        if (Target != null && Target.CanInteract() && controller.InteractAction.WasPressedThisFrame())
            Target.Interact(this);

        // Read the lock again: the interaction may have just taken it (a door transition, a dialogue).
        var locked = InputLock.IsLocked;
        hud.SetActive(!locked);
        if (!locked) ShowPrompt();
    }

    IInteractable FindTarget()
    {
        var eye = controller.CameraTarget;
        if (!Physics.Raycast(eye.position, eye.forward, out var hit, interactDistance, ~0, QueryTriggerInteraction.Ignore)) return null;
        return hit.collider.GetComponentInParent<IInteractable>();
    }

    void ShowPrompt()
    {
        if (Target == null)
        {
            prompt.text = "";
            crosshair.color = idleCrosshairColor;
            return;
        }
        var usable = Target.CanInteract();
        prompt.text = usable ? $"[{keyLabel}] {Target.GetPrompt()}" : Target.GetPrompt();
        prompt.color = usable ? readyColor : blockedColor;
        crosshair.color = usable ? readyColor : blockedColor;
    }

    void BuildHud()
    {
        var canvas = PixelUI.CreateCanvas("InteractionHud", transform, 10);
        hud = canvas.gameObject;
        crosshair = PixelUI.CreateImage("Crosshair", canvas.transform, new Vector2(2f, 2f));
        crosshair.color = idleCrosshairColor;
        crosshair.enabled = showCrosshair;
        prompt = PixelUI.CreateText("Prompt", canvas.transform);
        // Just under the crosshair, clear of the view center.
        ((RectTransform)prompt.transform).anchoredPosition = new Vector2(0f, -10f);
    }
}

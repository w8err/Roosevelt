using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Finds the IInteractable the player is looking at, shows its prompt under a small crosshair
// and uses it on Interact. Everything hides while InputLock is held.
// The HUD canvas is built at runtime, so scenes need no UI of their own.
[RequireComponent(typeof(FirstPersonController))]
public class PlayerInteraction : MonoBehaviour
{
    // How far the view ray looks. Whether the hit is close enough is decided afterwards, by the
    // distance to the object's nearest point, so this only has to cover the far end of a bed.
    const float MaxRayDistance = 4f;

    readonly RaycastHit[] hits = new RaycastHit[8];

    [Tooltip("Meters from the eye to the nearest point of what is aimed at, so any part of a big object like a bed counts.")]
    [SerializeField] float interactDistance = 2f;
    [Tooltip("Dot at screen center: faint while idle, bright over something usable.")]
    [SerializeField] bool showCrosshair = true;

    [Header("Colors")]
    [SerializeField] Color readyColor = Color.white;
    [Tooltip("Something that can't be used right now, like a locked door.")]
    [SerializeField] Color blockedColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] Color idleCrosshairColor = new Color(1f, 1f, 1f, 0.25f);

    FirstPersonController controller;
    Camera eyeCamera;
    GameObject hud;
    Image crosshair;
    Text prompt;
    string keyLabel;

    // What the player is aiming at this frame, or null.
    public IInteractable Target { get; private set; }

    void Awake()
    {
        controller = GetComponent<FirstPersonController>();
        eyeCamera = GetComponentInChildren<Camera>();
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
        // While the hands are on a cart, E belongs to the cart mode itself. Looking around must
        // not operate a nearby machine or door by accident.
        Target = InputLock.IsLocked || controller.IsCartControlled ? null : FindTarget();
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
        // Aim from the rendering camera so the ray stays on the crosshair while the camera noise sways the view.
        var eye = eyeCamera != null ? eyeCamera.transform : controller.CameraTarget;
        var count = Physics.RaycastNonAlloc(eye.position, eye.forward, hits, MaxRayDistance, ~0, QueryTriggerInteraction.Ignore);
        // The nearest hit that isn't the player's own body. Looking down 30-40 degrees (at a bed), the
        // ray hit the player's own CharacterController even though the eye sits inside it, and the
        // prompt flickered (confirmed with an aim log, 2026-09-13).
        var found = false;
        var hit = default(RaycastHit);
        for (var i = 0; i < count; i++)
        {
            if (hits[i].collider.transform.IsChildOf(transform)) continue;
            if (found && hits[i].distance >= hit.distance) continue;
            hit = hits[i];
            found = true;
        }
        if (!found) return null;
        var target = hit.collider.GetComponentInParent<IInteractable>();
        if (target == null || hit.distance <= interactDistance) return target;
        // What decides is how close the object's nearest point is, not where on it the player looks,
        // so standing by a bed and looking at its far end still counts.
        return NearestDistance(hit.collider, eye.position) <= interactDistance ? target : null;
    }

    // Collider.ClosestPoint only supports primitives and convex meshes; anything else only counts within ray range.
    static float NearestDistance(Collider collider, Vector3 from) => collider is MeshCollider { convex: false }
        ? float.PositiveInfinity
        : Vector3.Distance(from, collider.ClosestPoint(from));

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

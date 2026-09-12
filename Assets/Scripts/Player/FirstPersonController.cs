using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [SerializeField] InputActionAsset actions;
    [Tooltip("Pitch pivot at eye height. The Cinemachine camera follows it.")]
    [SerializeField] Transform cameraTarget;

    [Header("Movement")]
    [SerializeField] float walkSpeed = 2.2f;
    [SerializeField] float gravity = -9.81f;

    [Header("Look")]
    [SerializeField] float mouseSensitivity = 0.08f; // degrees per pixel
    [SerializeField] float stickSensitivity = 120f;  // degrees per second
    [SerializeField] float maxPitch = 85f;

    [Header("Interact")]
    [SerializeField] float interactDistance = 2f;

    CharacterController body;
    InputActionMap map;
    InputAction move, look, interact;
    float pitch, fallSpeed;

    public float WalkSpeed => walkSpeed;

    void Awake()
    {
        body = GetComponent<CharacterController>();
        map = actions.FindActionMap("Player", true);
        move = map.FindAction("Move", true);
        look = map.FindAction("Look", true);
        interact = map.FindAction("Interact", true);
    }

    void OnEnable()
    {
        map.Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        map.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        Look();
        Move();
        // The template gives Interact a Hold interaction; WasPressedThisFrame reacts on press regardless.
        if (interact.WasPressedThisFrame()) Interact();
    }

    void Look()
    {
        var delta = look.ReadValue<Vector2>();
        delta *= look.activeControl?.device is Pointer ? mouseSensitivity : stickSensitivity * Time.deltaTime;
        transform.Rotate(0f, delta.x, 0f);
        pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);
        cameraTarget.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        var input = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
        var velocity = (transform.right * input.x + transform.forward * input.y) * walkSpeed;
        fallSpeed = body.isGrounded ? -1f : fallSpeed + gravity * Time.deltaTime;
        velocity.y = fallSpeed;
        body.Move(velocity * Time.deltaTime);
    }

    void Interact()
    {
        if (!Physics.Raycast(cameraTarget.position, cameraTarget.forward, out var hit, interactDistance, ~0, QueryTriggerInteraction.Ignore)) return;
        var door = hit.collider.GetComponentInParent<LabDoor>();
        if (door != null) door.Toggle();
    }
}

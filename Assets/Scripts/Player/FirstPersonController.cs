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
    [SerializeField] float sprintSpeed = 6f;
    [Tooltip("Seconds to ramp between walk and sprint speed, both ways.")]
    [SerializeField] float speedChangeTime = 0.8f;
    [SerializeField] float gravity = -9.81f;

    [Header("Stamina")]
    [Tooltip("Off: unlimited sprint and the stamina bar is hidden.")]
    [SerializeField] bool useStamina = true;
    [Tooltip("Seconds of sprinting from full to empty.")]
    [SerializeField] float sprintDuration = 10f;
    [Tooltip("Seconds to refill from empty. Once empty, sprint stays locked until full again.")]
    [SerializeField] float recoverDuration = 5f;

    [Header("Look")]
    [SerializeField] float mouseSensitivity = 0.08f; // degrees per pixel
    [SerializeField] float stickSensitivity = 120f;  // degrees per second
    [SerializeField] float maxPitch = 85f;

    CharacterController body;
    InputActionMap map;
    InputAction move, look, interact, sprint;
    float pitch, fallSpeed, speed, stamina = 1f;
    bool exhausted, wantedSprint;

    public Transform CameraTarget => cameraTarget;
    // Read by PlayerInteraction; the action sits in the same Player map as movement.
    public InputAction InteractAction => interact;
    public float WalkSpeed => walkSpeed;
    public float SprintSpeed => sprintSpeed;
    public bool UseStamina => useStamina;
    public float Stamina01 => stamina;
    public bool Exhausted => exhausted;
    // Fired once each time the player tries to sprint while exhausted.
    public event System.Action SprintDenied;

    void Awake()
    {
        body = GetComponent<CharacterController>();
        map = actions.FindActionMap("Player", true);
        move = map.FindAction("Move", true);
        look = map.FindAction("Look", true);
        interact = map.FindAction("Interact", true);
        sprint = map.FindAction("Sprint", true);
        speed = walkSpeed;
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
        // Dialogue, transitions and cutscenes hold InputLock. Gravity keeps running so the body stays grounded.
        var locked = InputLock.IsLocked;
        if (!locked) Look();
        Move(locked);
    }

    // Moves the player without CharacterController fighting the teleport, for room
    // transitions and the dream/wake flow. Used by ScreenTransition while the screen is
    // black. rotation sets yaw only; pitch resets level and fall speed resets grounded,
    // so the player never lands already mid-fall from wherever they warped from.
    public void Warp(Vector3 position, Quaternion rotation)
    {
        body.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        body.enabled = true;
        pitch = 0f;
        cameraTarget.localRotation = Quaternion.identity;
        fallSpeed = -1f;
    }

    void Look()
    {
        var delta = look.ReadValue<Vector2>();
        delta *= look.activeControl?.device is Pointer ? mouseSensitivity : stickSensitivity * Time.deltaTime;
        transform.Rotate(0f, delta.x, 0f);
        pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);
        cameraTarget.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move(bool locked)
    {
        var input = locked ? Vector2.zero : Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
        // Forward or forward-diagonal only; strafing or backing up falls back to walking.
        var wantsSprint = sprint.IsPressed() && input.y > 0.5f;
        // Only on the moment of trying, so holding Shift while exhausted doesn't refire every frame.
        if (wantsSprint && !wantedSprint && exhausted) SprintDenied?.Invoke();
        wantedSprint = wantsSprint;
        var sprinting = wantsSprint && !exhausted;
        UpdateStamina(sprinting);
        var ramp = (sprintSpeed - walkSpeed) / speedChangeTime;
        speed = Mathf.MoveTowards(speed, sprinting ? sprintSpeed : walkSpeed, ramp * Time.deltaTime);

        var velocity = (transform.right * input.x + transform.forward * input.y) * speed;
        fallSpeed = body.isGrounded ? -1f : fallSpeed + gravity * Time.deltaTime;
        velocity.y = fallSpeed;
        body.Move(velocity * Time.deltaTime);
    }

    // Drains while sprinting and refills at once otherwise. Hitting empty locks sprint until full.
    void UpdateStamina(bool sprinting)
    {
        if (!useStamina)
        {
            stamina = 1f;
            exhausted = false;
            return;
        }
        if (sprinting)
        {
            stamina = Mathf.Max(0f, stamina - Time.deltaTime / sprintDuration);
            if (stamina == 0f) exhausted = true;
        }
        else
        {
            stamina = Mathf.Min(1f, stamina + Time.deltaTime / recoverDuration);
            if (stamina == 1f) exhausted = false;
        }
    }
}

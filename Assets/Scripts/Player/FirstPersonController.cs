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
    [Tooltip("Seconds to reach walk speed from standing still.")]
    [SerializeField] float accelerationTime = 0.2f;
    [Tooltip("Seconds to stop from walk speed. A sprint stops at the same rate, so it takes longer.")]
    [SerializeField] float decelerationTime = 0.12f;
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

    [Header("Seating")]
    [Tooltip("Eye height above the seat's root position while seated. Standing eye height comes from CameraTarget's own local position.")]
    [SerializeField] float seatEyeHeight = 1.15f;

    CharacterController body;
    InputActionMap map;
    InputAction move, look, interact, sprint;
    // speed: the walk/sprint speed the player is heading for. moveSpeed: how fast they actually go along moveDir.
    float pitch, fallSpeed, speed, moveSpeed, stamina = 1f;
    Vector3 moveDir;
    bool exhausted, wantedSprint, seated;

    public Transform CameraTarget => cameraTarget;
    // Read by PlayerInteraction; the action sits in the same Player map as movement.
    public InputAction InteractAction => interact;
    public float WalkSpeed => walkSpeed;
    public float SprintSpeed => sprintSpeed;
    public bool UseStamina => useStamina;
    public float Stamina01 => stamina;
    public bool Exhausted => exhausted;
    public float Pitch => pitch;
    public bool IsSeated => seated;
    public float SeatEyeHeight => seatEyeHeight;
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
        if (!seated) Move(locked);
    }

    // Enters or leaves seated mode. While seated, Move() (input, gravity, CharacterController.Move)
    // is skipped entirely and the CharacterController is disabled, so a chair's collider never
    // pushes the player and ChairInteractable can drive transform.position/rotation directly
    // during the sit-down lerp. PlayerCameraFeel reads IsSeated/SeatEyeHeight to ease the eye
    // height between standing and seated.
    public void SetSeated(bool value)
    {
        seated = value;
        body.enabled = !value;
        if (!value)
        {
            fallSpeed = -1f;
            moveSpeed = 0f;
            speed = walkSpeed;
        }
    }

    // Moves the player without CharacterController fighting the teleport, for room
    // transitions and the dream/wake flow. Used by ScreenTransition while the screen is
    // black. rotation sets yaw only; pitch resets level, fall speed resets grounded and the
    // player arrives standing still, so nothing carries over from wherever they warped from.
    public void Warp(Vector3 position, Quaternion rotation)
    {
        body.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        body.enabled = true;
        pitch = 0f;
        cameraTarget.localRotation = Quaternion.identity;
        fallSpeed = -1f;
        moveSpeed = 0f;
    }

    // Sets pitch (up/down look) while respecting maxPitch constraints. Used by cutscenes
    // and special moments to frame the camera correctly without breaking the look system.
    public void SetPitch(float newPitch)
    {
        pitch = Mathf.Clamp(newPitch, -maxPitch, maxPitch);
        cameraTarget.localRotation = Quaternion.Euler(pitch, 0f, 0f);
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
        // Sprint momentum never outlasts actual motion: stop, then walk again, and it starts from walk speed.
        speed = Mathf.Min(speed, Mathf.Max(walkSpeed, moveSpeed));
        var ramp = (sprintSpeed - walkSpeed) / speedChangeTime;
        speed = Mathf.MoveTowards(speed, sprinting ? sprintSpeed : walkSpeed, ramp * Time.deltaTime);

        // Direction follows the input at once so turning stays crisp; only the speed eases in and out.
        // With no input the last direction is kept, so the player comes to a short stop instead of halting dead.
        var wish = transform.right * input.x + transform.forward * input.y;
        var targetSpeed = 0f;
        if (wish.sqrMagnitude > 1e-4f)
        {
            moveDir = wish.normalized;
            targetSpeed = speed * wish.magnitude;
        }
        var rate = walkSpeed / (targetSpeed > moveSpeed ? accelerationTime : decelerationTime);
        moveSpeed = Mathf.MoveTowards(moveSpeed, targetSpeed, rate * Time.deltaTime);

        var velocity = moveDir * moveSpeed;
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

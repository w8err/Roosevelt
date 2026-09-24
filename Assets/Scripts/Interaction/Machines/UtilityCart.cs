using UnityEngine;

// A wheeled staging cart. With a carried part, interaction loads the first compatible empty
// socket. Empty-handed interaction moves the player to the rear handle and enters a dedicated
// drive mode: W/S roll, A/D steer, and looking around never whips the cart sideways.
[RequireComponent(typeof(Rigidbody))]
public sealed class UtilityCart : MonoBehaviour, IInteractable
{
    enum CartState { Idle, Aligning, Driving, Coasting }

    [SerializeField] string displayName = "운반 카트";

    [Header("Handle")]
    [SerializeField] Vector3 handleStandLocal = new Vector3(0f, 0f, -0.68f);
    [Tooltip("How far around the back the handle can be reached from, in degrees off dead astern. " +
             "The drawers are on the front face, so the front must not offer the cart itself.")]
    [Range(15f, 160f)] [SerializeField] float handleArcDegrees = 75f;
    [SerializeField] float alignDuration = 0.32f;

    [Header("Motion")]
    [SerializeField] float forwardSpeed = 1.35f;
    [SerializeField] float reverseSpeed = 0.72f;
    [SerializeField] float acceleration = 1.8f;
    [SerializeField] float braking = 3.2f;
    [SerializeField] float coastDeceleration = 2.2f;
    [SerializeField] float movingTurnSpeed = 88f;
    [SerializeField] float pivotTurnSpeed = 72f;
    [SerializeField] float turnAcceleration = 190f;
    [SerializeField] float collisionSkin = 0.025f;

    [Header("Impact audio")]
    [Tooltip("A percussive knock. Deliberately not one of the ambience groans: those swell over " +
             "seconds and read as something happening in the room, which is a signal this game " +
             "spends elsewhere and must not waste on a cart touching a wall.")]
    [SerializeField] AudioClip wheelImpactClip;
    [SerializeField] float impactVolume = 0.7f;
    [SerializeField] float impactCooldown = 0.3f;

    Rigidbody cartBody;
    Socket[] slots;
    Drawer[] drawers;
    Collider[] cartColliders;
    AudioSource impactSource;
    CartState state;
    Transform carrier;
    FirstPersonController carrierController;
    CharacterController carrierCollider;
    Vector3 alignStartPosition;
    Quaternion alignStartRotation;
    float alignStarted;
    float currentSpeed;
    float currentTurnSpeed;
    float nextImpactTime;
    bool contactHeld;
    int grabbedFrame;
    readonly Collider[] overlapBuffer = new Collider[24];

#if UNITY_EDITOR
    Vector2? editorDriveInput;
#endif

    public bool IsTransporting => state is CartState.Aligning or CartState.Driving;
    public bool IsDriving => state == CartState.Driving;
    public bool IsCoasting => state == CartState.Coasting;

    void Awake()
    {
        cartBody = GetComponent<Rigidbody>();
        slots = GetComponentsInChildren<Socket>(true);
        drawers = GetComponentsInChildren<Drawer>(true);
        cartColliders = GetComponentsInChildren<Collider>(true);
        impactSource = GetComponent<AudioSource>();
    }

    public string GetPrompt()
    {
        if (IsTransporting) return $"{displayName} 손잡이 놓기";
        if (!PlayerAtHandle()) return $"{displayName}: 뒤쪽 손잡이를 잡아야 한다";

        var hands = FindAnyObjectByType<CarryHands>();
        if (hands != null && hands.IsFull)
            return FindEmptySlot(hands.Held) != null
                ? $"{hands.Held.DisplayName} 카트에 싣기"
                : $"{displayName}: 열린 빈칸이 없다";

        return $"{displayName} 잡기";
    }

    public bool CanInteract()
    {
        // Letting go always works, wherever the player ended up.
        if (IsTransporting) return true;
        if (!PlayerAtHandle()) return false;
        var hands = FindAnyObjectByType<CarryHands>();
        return hands == null || !hands.IsFull || FindEmptySlot(hands.Held) != null;
    }

    // The cart is pushed from behind, and its front face is drawers. Offering the cart itself from
    // the front put a "grab the handle" prompt over the drawer the player was reaching for, so the
    // body answers only to someone who has walked around to the handle.
    bool PlayerAtHandle()
    {
        var hands = FindAnyObjectByType<CarryHands>();
        if (hands == null) return true;   // nothing to measure against; do not lock the cart away
        var toPlayer = hands.transform.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 1e-4f) return false;
        return Vector3.Angle(toPlayer, -transform.forward) <= handleArcDegrees;
    }

    public void Interact(PlayerInteraction player)
    {
        if (IsTransporting)
        {
            ReleaseHandle();
            return;
        }

        var hands = player.GetComponent<CarryHands>();
        if (hands != null && hands.IsFull)
        {
            var slot = FindEmptySlot(hands.Held);
            if (slot != null) slot.MountDirect(hands.Release());
            return;
        }

        GrabHandle(player);
    }

    void GrabHandle(PlayerInteraction player)
    {
        RestoreCarrierCollision();
        carrier = player.transform;
        carrierController = player.GetComponent<FirstPersonController>();
        carrierCollider = player.GetComponent<CharacterController>();
        IgnoreCarrierCollision(true);
        carrierController.BeginCartControl();
        // Nobody wheels a cart with its drawers hanging open, and the cart's collision box only
        // covers the shut body: an open drawer would sweep straight through a wall.
        foreach (var drawer in drawers)
            if (drawer != null) drawer.Close();

        alignStartPosition = carrier.position;
        alignStartRotation = carrier.rotation;
        alignStarted = Time.time;
        currentSpeed = 0f;
        currentTurnSpeed = 0f;
        contactHeld = false;
        grabbedFrame = Time.frameCount;
        state = CartState.Aligning;
    }

    void Update()
    {
        switch (state)
        {
            case CartState.Aligning:
                AlignToHandle();
                break;
            case CartState.Driving:
                if (Time.frameCount > grabbedFrame && carrierController.InteractAction.WasPressedThisFrame())
                    ReleaseHandle();
                else
                    Drive(ReadDriveInput(), Time.deltaTime, false);
                break;
            case CartState.Coasting:
                Drive(Vector2.zero, Time.deltaTime, true);
                break;
        }
    }

    void AlignToHandle()
    {
        var duration = Mathf.Max(0.01f, alignDuration);
        var t = Mathf.Clamp01((Time.time - alignStarted) / duration);
        var eased = t * t * (3f - 2f * t);
        var targetPosition = transform.TransformPoint(handleStandLocal);
        carrierController.SetCartPose(Vector3.Lerp(alignStartPosition, targetPosition, eased),
                                      Quaternion.Slerp(alignStartRotation, transform.rotation, eased));
        if (t >= 1f) state = CartState.Driving;
    }

    void Drive(Vector2 input, float deltaTime, bool coasting)
    {
        var blocked = false;
        var throttle = Mathf.Clamp(input.y, -1f, 1f);
        var targetSpeed = throttle >= 0f ? throttle * forwardSpeed : throttle * reverseSpeed;
        var speedRate = coasting ? coastDeceleration : Mathf.Abs(targetSpeed) > Mathf.Abs(currentSpeed) ? acceleration : braking;
        currentSpeed = Mathf.MoveTowards(currentSpeed, coasting ? 0f : targetSpeed, speedRate * deltaTime);

        var steer = coasting ? 0f : Mathf.Clamp(input.x, -1f, 1f);
        var steerLimit = Mathf.Abs(currentSpeed) > 0.08f ? movingTurnSpeed : pivotTurnSpeed;
        currentTurnSpeed = Mathf.MoveTowards(currentTurnSpeed, steer * steerLimit, turnAcceleration * deltaTime);

        var yaw = currentTurnSpeed * deltaTime;
        if (Mathf.Abs(yaw) > 0.001f)
        {
            var proposed = Quaternion.Euler(0f, yaw, 0f) * cartBody.rotation;
            if (CanOccupy(cartBody.position, proposed)) cartBody.rotation = proposed;
            else
            {
                // Strength from the turn that was actually denied. The old code added a flat 0.25,
                // which cleared the quiet-impact threshold no matter how gently the cart was leaning
                // on the wall, so nudging a corner rang the bell at full volume forever.
                var denied = Mathf.Abs(currentTurnSpeed) / Mathf.Max(1f, movingTurnSpeed);
                currentTurnSpeed = 0f;
                blocked = true;
                ReportContact(denied);
            }
        }

        var distance = Mathf.Abs(currentSpeed) * deltaTime;
        if (distance > 0.0001f)
        {
            var direction = cartBody.rotation * Vector3.forward * Mathf.Sign(currentSpeed);
            if (cartBody.SweepTest(direction, out var hit, distance + collisionSkin, QueryTriggerInteraction.Ignore))
            {
                var allowed = Mathf.Max(0f, hit.distance - collisionSkin);
                cartBody.position += direction * Mathf.Min(distance, allowed);
                blocked = true;
                ReportContact(Mathf.Abs(currentSpeed) / Mathf.Max(0.01f, forwardSpeed));
                currentSpeed = 0f;
            }
            else cartBody.position += direction * distance;
        }

        // Released the moment the cart has a free frame, so backing off and bumping again sounds
        // again, while holding it against the wall stays silent.
        if (!blocked) contactHeld = false;

        if (state == CartState.Driving)
            carrierController.SetCartPose(transform.TransformPoint(handleStandLocal), transform.rotation);
        else if (state == CartState.Coasting && Mathf.Abs(currentSpeed) < 0.01f && Mathf.Abs(currentTurnSpeed) < 0.1f)
            FinishCoast();
    }

    void ReleaseHandle()
    {
        carrierController.EndCartControl();
        carrier = null;
        carrierController = null;
        state = Mathf.Abs(currentSpeed) > 0.02f || Mathf.Abs(currentTurnSpeed) > 0.2f
            ? CartState.Coasting
            : CartState.Idle;
        if (state == CartState.Idle) RestoreCarrierCollision();
    }

    void FinishCoast()
    {
        currentSpeed = 0f;
        currentTurnSpeed = 0f;
        contactHeld = false;
        state = CartState.Idle;
        RestoreCarrierCollision();
    }

    Socket FindEmptySlot(Carryable part)
    {
        if (part == null) return null;
        foreach (var slot in slots)
            // Locked means the drawer holding it is shut: loading through a closed drawer would
            // undo the whole point of the drawers gating their own slots.
            if (slot != null && !slot.Locked && slot.IsEmpty && slot.Accepts(part)) return slot;
        return null;
    }

    Vector2 ReadDriveInput()
    {
#if UNITY_EDITOR
        if (editorDriveInput.HasValue) return editorDriveInput.Value;
#endif
        return carrierController != null ? carrierController.MoveInput : Vector2.zero;
    }

    bool CanOccupy(Vector3 position, Quaternion rotation)
    {
        // Slightly inset from the real body collider so resting on the floor is never treated as
        // a rotation obstruction. Own socket/vial colliders are ignored below.
        var center = position + rotation * new Vector3(0f, 0.37f, 0f);
        var count = Physics.OverlapBoxNonAlloc(center, new Vector3(0.425f, 0.33f, 0.225f), overlapBuffer,
                                               rotation, ~0, QueryTriggerInteraction.Ignore);
        for (var i = 0; i < count; i++)
        {
            var other = overlapBuffer[i];
            if (other == null || other.transform.IsChildOf(transform) || other == carrierCollider) continue;
            return false;
        }
        return true;
    }

    // An impact is the moment of contact, not the state of touching something. Resting against a
    // wall reports contact every frame, and sounding every one of those turned a parked cart into a
    // pile of overlapping clangs.
    void ReportContact(float strength01)
    {
        if (contactHeld) return;
        contactHeld = true;
        PlayImpact(strength01);
    }

    void PlayImpact(float strength01)
    {
        if (impactSource == null || strength01 < 0.12f || Time.time < nextImpactTime) return;
        nextImpactTime = Time.time + impactCooldown;
        impactSource.pitch = Random.Range(0.92f, 1.06f);
        var volume = Mathf.Clamp01(strength01) * impactVolume;
        if (wheelImpactClip != null) impactSource.PlayOneShot(wheelImpactClip, volume);
    }

    void IgnoreCarrierCollision(bool ignore)
    {
        if (carrierCollider == null) return;
        foreach (var collider in cartColliders)
            if (collider != null && collider != carrierCollider)
                Physics.IgnoreCollision(collider, carrierCollider, ignore);
    }

    void RestoreCarrierCollision()
    {
        IgnoreCarrierCollision(false);
        carrierCollider = null;
    }

    void OnDisable()
    {
        if (carrierController != null) carrierController.EndCartControl();
        RestoreCarrierCollision();
        carrier = null;
        carrierController = null;
        state = CartState.Idle;
    }

#if UNITY_EDITOR
    // Used only by the opt-in Play Mode smoke probe; player builds always read the real Input System.
    public void SetEditorDriveInput(Vector2? value) => editorDriveInput = value;
#endif
}

using UnityEngine;

// Propels and steers the boat in response to oar strokes. Each public method is one discrete stroke,
// triggered by a key press, not by continuous input: pulling an oar is a deliberate act and the
// laboriousness is the point.
//
// This component writes only transform.position.x/z and BoatBuoyancy.Heading. It must NOT touch .y or
// transform.rotation — BoatBuoyancy owns those (wave height and tilt), and the two would fight over the
// transform if that split were blurred. See BoatBuoyancy's header for the other half of the contract.
[RequireComponent(typeof(BoatBuoyancy))]
public class BoatRowing : MonoBehaviour
{
    [Tooltip("Speed the boat is moving at the instant a forward stroke lands, m/s.")]
    [SerializeField] float strokeSpeed = 1.2f;

    [Tooltip("Exponential drag, 1/seconds. Total coast from one stroke is strokeSpeed / this, so the " +
        "defaults give 1.2 / 0.48 = 2.5m if left to run out, about 1.75m of it in the first 2.5s.")]
    [SerializeField] float dragPerSecond = 0.48f;

    [Tooltip("Below this speed (m/s) the boat is parked outright. Exponential decay never actually " +
        "reaches zero, and without a floor the hull keeps creeping for the rest of the scene — which " +
        "also means the water grid that snaps to follow it never settles.")]
    [SerializeField] float stopSpeed = 0.02f;

    [Tooltip("Degrees of heading change per turn stroke.")]
    [SerializeField] float turnAngle = 12f;

    [Tooltip("Seconds one turn stroke takes to play out.")]
    [SerializeField] float turnDuration = 0.6f;

    // Resolved on first use rather than cached in Awake alone. Awake does not run in Edit mode, and the
    // stroke methods below are public precisely so an editor script can call them — a cache filled only
    // by Awake left every one of them dereferencing null the moment they were driven from the builder.
    BoatBuoyancy Buoyancy => buoyancy != null ? buoyancy : buoyancy = GetComponent<BoatBuoyancy>();
    BoatBuoyancy buoyancy;

    Vector3 velocity;

    // A turn is played back from a captured start angle rather than by nudging the live heading toward
    // a target each frame. "Lerp from wherever I am now, by a fraction that grows to 1" looks like
    // easing but is not: it crawls for most of the stroke and then jumps the remaining distance on the
    // last frame, and how far it gets depends on the frame rate. Holding startHeading fixed makes the
    // stroke take exactly turnDuration and land exactly on target at any frame rate.
    float turnStartHeading;
    float turnTargetHeading;
    float turnElapsed = -1f; // negative = no turn in progress

    void Update() => Tick(Time.deltaTime);

    // The per-frame motion, separated from Update() so an editor script can drive strokes and watch what
    // they do without entering play mode — Update() doesn't run in Edit mode, the same reason
    // CreatureBuilder calls StalkWalker.Tick() by hand and BoatBuoyancy exposes Evaluate(). Verifying
    // rowing from a still capture is otherwise impossible, and the turn direction in particular is a sign
    // that has already been wrong twice.
    public void Tick(float deltaTime)
    {

        // Frame-rate independent drag. The obvious (1 - drag * dt) is the Euler approximation of this
        // and diverges from it as dt grows, going negative outright below ~2 FPS.
        velocity *= Mathf.Exp(-dragPerSecond * deltaTime);
        if (velocity.sqrMagnitude < stopSpeed * stopSpeed) velocity = Vector3.zero;

        if (velocity != Vector3.zero)
        {
            var position = transform.position;
            position.x += velocity.x * deltaTime;
            position.z += velocity.z * deltaTime;
            transform.position = position; // y deliberately left as-is; BoatBuoyancy sets it
        }

        if (turnElapsed < 0f) return;

        turnElapsed += deltaTime;
        var t = turnDuration > 0f ? Mathf.Clamp01(turnElapsed / turnDuration) : 1f;
        Buoyancy.Heading = Mathf.LerpAngle(turnStartHeading, turnTargetHeading, Mathf.SmoothStep(0f, 1f, t));
        if (t >= 1f) turnElapsed = -1f;
    }

    // One forward stroke, along the boat's own heading. Velocity is replaced rather than accumulated:
    // rowing faster then means keeping a steady speed up instead of building an unbounded one, which
    // suits a heavy skiff and removes any need to clamp a top speed.
    public void RowForward()
    {
        var heading = Buoyancy.Heading * Mathf.Deg2Rad;
        velocity.x = Mathf.Sin(heading) * strokeSpeed;
        velocity.z = Mathf.Cos(heading) * strokeSpeed;
    }

    // Named for the oar that gets pulled, not the direction the boat ends up going, because those are
    // opposites and naming them by outcome is how the signs got flipped twice already. Thrust on one
    // side pushes the far side forward: pull the port (left) oar and the bow swings to STARBOARD.
    // The user chose this real behaviour over the more guessable "aim left, go left".
    //
    // Unity's yaw increases clockwise seen from above, so turning right is POSITIVE.
    public void PullPortOar() => BeginTurn(turnAngle);

    public void PullStarboardOar() => BeginTurn(-turnAngle);

    // Re-aiming mid-stroke restarts the ease from the live heading, so holding down a direction gives a
    // continuous sweep rather than a stutter back to a stale start angle.
    void BeginTurn(float degrees)
    {
        turnStartHeading = Buoyancy.Heading;
        turnTargetHeading = turnStartHeading + degrees;
        turnElapsed = 0f;
    }
}

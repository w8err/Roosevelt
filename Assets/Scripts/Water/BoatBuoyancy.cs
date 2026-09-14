using UnityEngine;

// Floats a boat on Ocean.shader's surface: Y and roll/pitch follow OceanWaves.Height/Slope sampled at the
// bow, stern, port and starboard every frame, reading the same OceanSettings asset the water's material
// was tuned from. The FBX's origin is the still-water line itself (roosevelt-70: local y = 0 is the
// surface a resting boat sits at, draft 0.170m below it, freeboard 0.270m at midship, 0.336m at the bow).
//
// This writes ONLY position.y and the rotation. X/Z belong to BoatRowing, which rows the boat around;
// an earlier version of this file pinned X/Z here because the boat was designed never to sail, and the
// two would fight if that were restored. Heading below is the other half of that split — rowing steers
// by writing it, and this reads it to orient both the sample points and the final rotation.
public class BoatBuoyancy : MonoBehaviour
{
    // Sample distances to the bow/stern/port/starboard, in metres from the boat's own origin — SM_Forest_Boat_Row's
    // actual measurements (roosevelt-70): 2.42m long, 0.964m beam, origin not centered fore-aft. A hull
    // this size next to these wavelengths still needs the tilt these four points give it (a flat plane
    // through one point alone reads the surface as flatter than it is one hull-length away), so this
    // stays even though the wavelengths were separately lengthened to keep the boat under ~1/3 of the
    // shortest one.
    [SerializeField] float bowDistance = 1.30f;
    [SerializeField] float sternDistance = 1.12f;
    [SerializeField] float portDistance = 0.46f;
    [SerializeField] float starboardDistance = 0.46f;

    // Heading (degrees around world Y) is writable so BoatRowing can steer. It seeds itself from the
    // boat's placed rotation on first access and persists from then on.
    //
    // The seeding is guarded by an explicit bool, NOT by comparing Heading to a sentinel. This replaced
    // `if (Heading == default)`, which looks like the `restYaw == default` check it grew out of but is
    // not the same thing at all: default(Quaternion) is (0,0,0,0), a degenerate value no real rotation
    // ever takes, whereas default(float) is 0 — a perfectly ordinary heading, and the exact one the
    // Water_Test boat is placed at. That check therefore re-seeded every frame, reading eulerAngles.y
    // back off a rotation this method had already multiplied by the wave tilt, so the extracted yaw was
    // no longer 0 and the boat slowly yawed on its own. Seeding through the property also makes script
    // execution order irrelevant: BoatRowing reads this in its own Awake.
    float heading;
    bool headingSeeded;

    public float Heading
    {
        get
        {
            if (!headingSeeded)
            {
                heading = transform.eulerAngles.y;
                headingSeeded = true;
            }
            return heading;
        }
        set
        {
            heading = value;
            headingSeeded = true;
        }
    }

    // Matches Ocean.shader's _Time.y exactly (Time.timeSinceLevelLoad, not Time.time) — see that
    // shader's header comment for why using the wrong clock would drift the boat out of sync.
    void Update() => Evaluate(Time.timeSinceLevelLoad);

    // The actual per-frame math, pulled out of Update() so an editor script can simulate specific
    // moments without Play mode — Update() doesn't run in Edit mode, the same reason CreatureBuilder
    // drives StalkWalker.Tick() by hand instead of relying on it.
    public void Evaluate(float time)
    {
        var settings = OceanSettings.Current;
        var position = transform.position;
        var xz = new Vector2(position.x, position.z);

        // Read Heading once: the property seeds itself on first access, so reading it repeatedly inside
        // one frame is harmless but pointless.
        var headingRotation = Quaternion.Euler(0f, Heading, 0f);

        // Sample points from the boat's own heading, not assumed world axes — see OceanWaves.Slope's
        // comment for why (an earlier version used worldXZ + (0, distance) directly, correct only at yaw 0).
        var forward3 = headingRotation * Vector3.forward;
        var right3 = headingRotation * Vector3.right;
        var forward = new Vector2(forward3.x, forward3.z);
        var right = new Vector2(right3.x, right3.z);

        var bowPoint = xz + forward * bowDistance;
        var sternPoint = xz - forward * sternDistance;
        var starboardPoint = xz + right * starboardDistance;
        var portPoint = xz - right * portDistance;

        var bowHeight = OceanWaves.Height(settings, bowPoint, time);
        var sternHeight = OceanWaves.Height(settings, sternPoint, time);
        var portHeight = OceanWaves.Height(settings, portPoint, time);
        var starboardHeight = OceanWaves.Height(settings, starboardPoint, time);

        // The origin's own height uses the highest of the four corners rather than the analytic height
        // exactly at the origin: measured (see the offline sweep in this change's report, not guessed)
        // — using the origin's own point lets a symmetric trough (both ends measurably higher than a
        // dead-center low point) sit the whole hull below where all four corners actually are, gaining
        // only ~0.04m of worst-case clearance here but never making it worse. A residual gap beyond
        // this and the sampling above is what buoyancyBias is for, left at 0 until playtesting says
        // otherwise — see BoatInteractable/this change's report for what was and wasn't measured.
        // Only Y is written; BoatRowing owns X/Z positioning.
        position.y = Mathf.Max(Mathf.Max(bowHeight, sternHeight), Mathf.Max(portHeight, starboardHeight)) + buoyancyBias;
        transform.position = position;

        var slope = OceanWaves.Slope(bowPoint, bowHeight, sternPoint, sternHeight, portPoint, portHeight, starboardPoint, starboardHeight);
        transform.rotation = Quaternion.Slerp(Quaternion.identity, slope, tiltScale) * headingRotation;
    }

    [Range(0f, 1f)]
    [Tooltip("How much of the water's actual slope the boat takes on. 1 = the hull lies exactly in the " +
        "surface (physically right, but the seated camera is parented to this transform, so the full " +
        "tilt goes straight into the player's view — these waves swing roughly 12-15 degrees peak-to-peak " +
        "over 3-10 second periods, which reads as violent from a first-person seat). Below 1 the boat " +
        "tilts less than the water does. This is a deliberate cheat and it is cheap to hide: nobody sees " +
        "this boat from outside during play, and at the seated eye height the hull itself covers the " +
        "waterline where the mismatch would show. Heave (up/down) is deliberately NOT scaled — that is " +
        "what keeps the boat sitting on the sea instead of slicing through it. If the bow or stern starts " +
        "poking through a crest at a low value here, raise buoyancyBias rather than this.")]
    [SerializeField] float tiltScale = 0.35f;

    [Tooltip("Added on top of the computed height as a flat safety margin. Left at 0: an offline sweep " +
        "(see this change's report) found the modeled worst-case clearance already positive without one, " +
        "so this is here for playtesting to dial in against the real mesh/lighting, not a guessed constant.")]
    [SerializeField] float buoyancyBias;
}

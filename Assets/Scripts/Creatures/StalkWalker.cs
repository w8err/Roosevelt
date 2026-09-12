using System.Collections.Generic;
using UnityEngine;

// Procedural walk for the Stalk creatures (Art/tools/make_stalk.py). The body drifts between roam points, each
// root foot steps on its own when it lags behind its rest spot, legs bend with two-bone IK, the root claws close
// while a foot is in the air, and the neck sways and slowly turns the head slit toward a nearby viewer.
// Bones are found by name and every rotation is rebuilt from the rest pose, so the bone axes of the imported rig
// never matter. Tick() also runs in edit mode, which is how CreatureBuilder renders walking review shots.
public class StalkWalker : MonoBehaviour
{
    [Header("Walk")]
    [SerializeField] float speed = 0.5f;
    [SerializeField] float turnSpeed = 20f;          // degrees per second
    [SerializeField] float stepTime = 0.9f;
    [SerializeField] float stepHeight = 0.5f;
    [Tooltip("How far a foot may lag behind its rest spot before it steps (m).")]
    [SerializeField] float stepTrigger = 0.55f;
    [Tooltip("Legs allowed in the air at once. 0 = half the legs, at least one.")]
    [SerializeField] int maxAirborne;
    [Tooltip("How far the body sinks while a foot is in the air (m).")]
    [SerializeField] float bob = 0.12f;

    [Header("Roam")]
    [SerializeField] float roamRadius = 14f;
    [SerializeField] Vector2 pauseRange = new Vector2(3f, 9f);
    [Tooltip("Clearance kept around the body at hip height when picking a path (m).")]
    [SerializeField] float bodyRadius = 0.7f;

    [Header("Watch")]
    [Tooltip("Within this distance the creature stops, turns and stares.")]
    [SerializeField] float watchDistance = 22f;
    [Tooltip("Who it stares at. Empty: the main camera.")]
    [SerializeField] Transform watchTarget;

    [Header("World")]
    [Tooltip("Collider the feet stand on (the terrain). Empty: the lowest surface below.")]
    [SerializeField] Collider ground;
    [SerializeField] int seed = 1;

    class Leg
    {
        public Transform upper, lower, foot;
        public Transform[] roots, rootEnds;
        public Quaternion[] rootRest;
        public Quaternion upperOffset, lowerOffset, footRest;
        public float len1, len2, ankleHeight;
        public Vector3 restSpot, bendLocal;   // body space: ground spot under the ankle, knee bend direction
        public Vector3 planted, from, to;
        public float t = 1f, fromYaw, toYaw, grounded;
        public bool Airborne => t < 1f;
        public float Lift => Airborne ? Mathf.Sin(Mathf.PI * t) : 0f;
        public Vector3 Ground => Airborne ? Vector3.Lerp(from, to, Smooth(t)) : planted;
    }

    enum Mode { Pause, Walk }

    bool ready;
    Transform hip, head, headEnd;
    Transform[] spine;
    Quaternion[] spineRest;
    Quaternion hipRest;
    Vector3 hipRestLocal, faceLocal;
    Leg[] legs;
    Mode mode;
    Vector3 home, destination, velocity;
    float yaw, pauseTimer, clock, look;
    System.Random random;
    readonly Collider[] overlaps = new Collider[16];
    readonly RaycastHit[] hits = new RaycastHit[16];

    // review numbers for CreatureBuilder
    int steps;
    float overreach, lowestHip = float.MaxValue;
    Vector3 start;

    static float Smooth(float t) => t * t * (3f - 2f * t);
    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    Transform Viewer => watchTarget != null ? watchTarget : Camera.main != null ? Camera.main.transform : null;

    void Update() => Tick(Time.deltaTime);

    public void Init()
    {
        if (ready) return;
        var bones = new Dictionary<string, Transform>();
        foreach (var t in GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
        if (!bones.TryGetValue("Hip", out hip) || !bones.TryGetValue("Head", out head))
        {
            Debug.LogError($"[StalkWalker] {name}: rig has no Hip/Head bones", this);
            enabled = false;
            return;
        }
        bones.TryGetValue("Head_end", out headEnd);

        var chain = new List<Transform>();
        for (var i = 1; bones.TryGetValue("Neck_" + i, out var neck); i++) chain.Add(neck);
        chain.Add(head);
        spine = chain.ToArray();
        spineRest = System.Array.ConvertAll(spine, b => b.localRotation);
        hipRest = Quaternion.Inverse(transform.rotation) * hip.rotation;
        hipRestLocal = transform.InverseTransformPoint(hip.position);
        faceLocal = Quaternion.Inverse(head.rotation) * transform.forward;   // the slit faces the model front

        var list = new List<Leg>();
        for (var i = 1; bones.TryGetValue($"Leg{i}_Upper", out var upper); i++)
        {
            var l = new Leg { upper = upper, lower = bones[$"Leg{i}_Lower"], foot = bones[$"Leg{i}_Foot"] };
            Vector3 a = l.upper.position, k = l.lower.position, f = l.foot.position;
            l.len1 = (k - a).magnitude;
            l.len2 = (f - k).magnitude;
            l.ankleHeight = f.y - transform.position.y;
            l.restSpot = transform.InverseTransformPoint(f);
            l.restSpot.y = 0f;
            var bend = Vector3.ProjectOnPlane(k - a, f - a).normalized;
            l.bendLocal = transform.InverseTransformDirection(bend);
            l.upperOffset = Quaternion.Inverse(Quaternion.LookRotation(k - a, bend)) * l.upper.rotation;
            l.lowerOffset = Quaternion.Inverse(Quaternion.LookRotation(f - k, bend)) * l.lower.rotation;
            l.footRest = Quaternion.Inverse(transform.rotation) * l.foot.rotation;

            var roots = new List<Transform>();
            for (var j = 1; bones.TryGetValue($"Leg{i}_Root{j}", out var root); j++) roots.Add(root);
            l.roots = roots.ToArray();
            l.rootEnds = roots.ConvertAll(r => bones.TryGetValue(r.name + "_end", out var e) ? e : null).ToArray();
            l.rootRest = roots.ConvertAll(r => r.localRotation).ToArray();
            list.Add(l);
        }
        legs = list.ToArray();

        random = new System.Random(seed);
        clock = seed * 17.3f;
        yaw = transform.eulerAngles.y;
        home = start = transform.position;
        foreach (var l in legs)
        {
            GroundAt(Spot(l, 0f), out l.planted);
            l.toYaw = l.fromYaw = yaw;
        }
        mode = Mode.Pause;
        pauseTimer = Range(pauseRange) * 0.5f;
        ready = true;
    }

    public void Tick(float dt)
    {
        Init();
        if (!ready || dt <= 0f) return;
        clock += dt;
        Think(dt);
        MoveBody(dt);
        Feet(dt);
        PoseHip(dt);
        foreach (var l in legs) PoseLeg(l);
        PoseSpine();
    }

    // ---------- behaviour ----------

    void Think(float dt)
    {
        var viewer = Viewer;
        var watching = viewer != null && Flat(viewer.position - transform.position).magnitude < watchDistance;
        look = Mathf.MoveTowards(look, watching ? 1f : 0f, dt / 2.5f);   // the head turns slowly
        var want = Vector3.zero;
        if (watching)
        {
            TurnToward(viewer.position, dt, 0.5f);
            mode = Mode.Pause;
            pauseTimer = Mathf.Max(pauseTimer, 2f);   // keeps standing a while after the viewer leaves
        }
        else if (mode == Mode.Pause)
        {
            pauseTimer -= dt;
            if (pauseTimer <= 0f && PickDestination()) mode = Mode.Walk;
        }
        else
        {
            var to = Flat(destination - transform.position);
            if (to.magnitude < 1.5f || Blocked(transform.position, transform.position + transform.forward * 2.5f))
            {
                mode = Mode.Pause;
                pauseTimer = Range(pauseRange);
            }
            else
            {
                TurnToward(destination, dt, 1f);
                var align = Mathf.Clamp01(Vector3.Dot(transform.forward, to.normalized));
                want = transform.forward * (speed * align * align);   // slows down to turn
            }
        }
        velocity = Vector3.MoveTowards(velocity, want, speed * 0.8f * dt);
    }

    void TurnToward(Vector3 target, float dt, float rate)
    {
        var to = Flat(target - transform.position);
        if (to.sqrMagnitude < 0.01f) return;
        yaw = Mathf.MoveTowardsAngle(yaw, Quaternion.LookRotation(to).eulerAngles.y, turnSpeed * rate * dt);
    }

    bool PickDestination()
    {
        for (var k = 0; k < 12; k++)
        {
            var a = (float)random.NextDouble() * Mathf.PI * 2f;
            var r = roamRadius * Mathf.Sqrt((float)random.NextDouble());
            var p = home + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            if (Flat(p - transform.position).magnitude < 4f || !GroundAt(p, out p) || Blocked(transform.position, p)) continue;
            destination = p;
            return true;
        }
        pauseTimer = 1f;
        return false;
    }

    // A sphere at hip height swept along the path: trees, rocks and walls block it, the ground does not.
    bool Blocked(Vector3 from, Vector3 to)
    {
        var d = Flat(to - from);
        var n = Physics.SphereCastNonAlloc(from + Vector3.up * 1.5f, bodyRadius, d.normalized, hits, d.magnitude,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (var i = 0; i < n; i++)
            if (hits[i].distance > 0f && !Ignored(hits[i].collider)) return true;   // distance 0 = already touching
        return false;
    }

    bool Ignored(Collider c) => c == ground || c.transform.IsChildOf(transform) || c is CharacterController;

    float Range(Vector2 r) => r.x + (float)random.NextDouble() * (r.y - r.x);

    // ---------- body and feet ----------

    void MoveBody(float dt)
    {
        var p = transform.position + velocity * dt;
        if (GroundAt(p, out var g)) p.y = g.y;
        transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
    }

    Vector3 Spot(Leg l, float ahead) => transform.TransformPoint(l.restSpot) + velocity * ahead;

    void Feet(float dt)
    {
        var airborne = 0;
        foreach (var l in legs)
        {
            if (!l.Airborne) { l.grounded += dt; continue; }
            l.t = Mathf.Min(1f, l.t + dt / stepTime);
            if (l.Airborne) airborne++;
            else { l.planted = l.to; l.grounded = 0f; }
        }
        var limit = maxAirborne > 0 ? maxAirborne : Mathf.Max(1, legs.Length / 2);
        if (airborne >= limit) return;

        // the foot that lags most steps next; neighbours never lift together
        Leg next = null;
        var worst = 0f;
        var threshold = velocity.sqrMagnitude > 0.01f ? stepTrigger : stepTrigger * 0.4f;   // settle after turning
        for (var i = 0; i < legs.Length; i++)
        {
            var l = legs[i];
            if (l.Airborne || l.grounded < 0.2f) continue;
            if (legs.Length > 2 && (legs[(i + 1) % legs.Length].Airborne || legs[(i + legs.Length - 1) % legs.Length].Airborne)) continue;
            var lag = Flat(Spot(l, 0f) - l.planted).magnitude;
            if (lag > threshold && lag > worst) { worst = lag; next = l; }
        }
        if (next == null) return;
        var target = FreeSpot(Spot(next, stepTime * 0.8f));
        GroundAt(target, out target);
        next.from = next.planted;
        next.to = target;
        next.t = 0f;
        next.fromYaw = next.toYaw;
        next.toYaw = yaw;
        steps++;
    }

    // Swings a blocked landing spot around the body until it is off trees and rocks.
    Vector3 FreeSpot(Vector3 p)
    {
        var centre = transform.position;
        for (var k = 0; k < 5; k++)
        {
            var angle = k == 0 ? 0f : (k % 2 == 1 ? 25f : -25f) * ((k + 1) / 2);
            var q = centre + Quaternion.Euler(0f, angle, 0f) * (p - centre);
            GroundAt(q, out q);
            var n = Physics.OverlapSphereNonAlloc(q + Vector3.up * 0.5f, 0.3f, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var free = true;
            for (var i = 0; i < n && free; i++) free = Ignored(overlaps[i]);
            if (free) return q;
        }
        return p;
    }

    bool GroundAt(Vector3 p, out Vector3 point)
    {
        point = p;
        var ray = new Ray(new Vector3(p.x, p.y + 30f, p.z), Vector3.down);
        if (ground != null)
        {
            if (!ground.Raycast(ray, out var hit, 80f)) return false;
            point = hit.point;
            return true;
        }
        var n = Physics.RaycastNonAlloc(ray, hits, 80f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        var best = -1f;
        for (var i = 0; i < n; i++)
        {
            if (hits[i].collider.transform.IsChildOf(transform) || hits[i].distance <= best) continue;
            best = hits[i].distance;   // the lowest surface, so tree crowns overhead are not taken for ground
            point = hits[i].point;
        }
        return best >= 0f;
    }

    // ---------- pose ----------

    void PoseHip(float dt)
    {
        var footY = 0f;
        var lift = 0f;
        var mid = Vector3.zero;
        var planted = 0;
        foreach (var l in legs)
        {
            var g = l.Ground;
            footY += g.y;
            lift = Mathf.Max(lift, l.Lift);
            if (!l.Airborne) { mid += g; planted++; }
        }
        var rest = transform.TransformPoint(hipRestLocal) - transform.position;
        var target = transform.position + rest;
        target.y = footY / legs.Length + rest.y - bob * lift + 0.04f * Mathf.Sin(clock * 0.8f);
        if (planted > 0) target += Flat(mid / planted - target) * 0.12f;   // leans over the feet on the ground

        // sink rather than overstretch a leg
        var drop = 0f;
        foreach (var l in legs)
        {
            var a = target + (l.upper.position - hip.position);
            var ankle = l.Ground + Vector3.up * (l.ankleHeight + stepHeight * l.Lift);
            var reach = (l.len1 + l.len2) * 0.985f;
            var h = Flat(ankle - a).magnitude;
            if (h < reach) drop = Mathf.Max(drop, a.y - ankle.y - Mathf.Sqrt(reach * reach - h * h));
        }
        target.y -= drop;

        hip.position = Vector3.Lerp(hip.position, target, 1f - Mathf.Exp(-dt * 8f));
        hip.rotation = Quaternion.AngleAxis(2.5f * Mathf.Sin(clock * 0.6f), transform.forward) * transform.rotation * hipRest;
        lowestHip = Mathf.Min(lowestHip, hip.position.y - transform.position.y);
    }

    void PoseLeg(Leg l)
    {
        var ankle = l.Ground + Vector3.up * (l.ankleHeight + stepHeight * l.Lift);
        var a = l.upper.position;
        var to = ankle - a;
        overreach = Mathf.Max(overreach, to.magnitude - (l.len1 + l.len2));
        var d = Mathf.Clamp(to.magnitude, Mathf.Abs(l.len1 - l.len2) + 1e-3f, l.len1 + l.len2 - 1e-3f);
        var dir = to.normalized;
        var bend = Vector3.ProjectOnPlane(transform.TransformDirection(l.bendLocal), dir).normalized;
        var cos = (l.len1 * l.len1 + d * d - l.len2 * l.len2) / (2f * l.len1 * d);
        var knee = a + dir * (cos * l.len1) + bend * (Mathf.Sqrt(Mathf.Max(0f, 1f - cos * cos)) * l.len1);
        l.upper.rotation = Quaternion.LookRotation(knee - a, bend) * l.upperOffset;
        l.lower.rotation = Quaternion.LookRotation(a + dir * d - knee, bend) * l.lowerOffset;

        // a planted foot keeps the heading it landed with, so its roots do not twist with the body
        var footYaw = l.Airborne ? Mathf.LerpAngle(l.fromYaw, l.toYaw, Smooth(l.t)) : l.toYaw;
        l.foot.rotation = Quaternion.Euler(0f, footYaw, 0f) * l.footRest;

        // the roots pull out of the ground and close like a claw, then open before landing
        var curl = l.Airborne ? Mathf.Clamp01(Mathf.Min(l.t / 0.25f, (1f - l.t) / 0.3f)) : 0f;
        for (var j = 0; j < l.roots.Length; j++)
        {
            var r = l.roots[j];
            r.localRotation = l.rootRest[j];
            if (curl <= 0f || l.rootEnds[j] == null) continue;
            var now = l.rootEnds[j].position - r.position;
            var want = Vector3.down - Flat(now).normalized * 0.35f;
            r.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(now, want), curl * 0.85f) * r.rotation;
        }
    }

    void PoseSpine()
    {
        var n = spine.Length;
        var lean = Vector3.Dot(velocity, transform.forward) / Mathf.Max(speed, 0.01f) * 8f;   // degrees over the chain
        for (var i = 0; i < n; i++)
        {
            var b = spine[i];
            b.localRotation = spineRest[i];
            var k = (i + 1f) / n;
            var pitch = (Mathf.PerlinNoise(clock * 0.15f, seed * 7.1f + i * 0.37f) - 0.5f) * 2f * (2f + 5f * k) + lean / n;
            var roll = (Mathf.PerlinNoise(seed * 3.3f + i * 0.41f, clock * 0.12f) - 0.5f) * 2f * (1.5f + 4f * k);
            b.rotation = Quaternion.AngleAxis(pitch, transform.right) * Quaternion.AngleAxis(roll, transform.forward) * b.rotation;
        }

        // staring: the top three bones share the turn that points the slit at the viewer
        var viewer = Viewer;
        if (look <= 0.001f || viewer == null) return;
        var centre = headEnd != null ? Vector3.Lerp(head.position, headEnd.position, 0.5f) : head.position;
        var want = (viewer.position - centre).normalized;
        float[] share = { 0.3f, 0.5f, 1f };
        for (var s = 0; s < 3; s++)
        {
            var i = n - 3 + s;
            if (i < 0) continue;
            var turn = Quaternion.FromToRotation(head.rotation * faceLocal, want);
            spine[i].rotation = Quaternion.Slerp(Quaternion.identity, turn, share[s] * look) * spine[i].rotation;
        }
    }

    // ---------- review hooks (CreatureBuilder) ----------

    public void ReviewWalkTo(Vector3 target)
    {
        Init();
        watchDistance = 0f;
        destination = target;
        mode = Mode.Walk;
    }

    public void ReviewRoam()
    {
        Init();
        watchDistance = 0f;
    }

    public void ReviewWatch(Transform target)
    {
        Init();
        watchTarget = target;
        watchDistance = 1000f;
    }

    public string ReviewStats() =>
        $"moved {Flat(transform.position - start).magnitude:F2} m, {steps} steps, lowest hip {lowestHip:F2} m, " +
        $"max overreach {overreach:F3} m, look {look:F2}";

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.3f, 0.3f);
        var c = Application.isPlaying ? home : transform.position;
        Gizmos.DrawWireSphere(c, roamRadius);
        if (Application.isPlaying && mode == Mode.Walk) Gizmos.DrawLine(transform.position, destination);
    }
}

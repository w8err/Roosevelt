using UnityEngine;

// Smoothly yaws the whole body toward a target (the player, during a conversation) and back to
// whatever direction the NPC was placed facing. Kept separate from CharacterAnimator: rotation and
// animation are independent axes (a future walking state will want to drive this same facing while
// mid-stride, not just while idle/talking), and CharacterAnimator's own contract is to stay a plain
// Play(action)/ReturnToIdle() wrapper that never touches the transform.
public class CharacterFacing : MonoBehaviour
{
    // A calm researcher turning to see who's talking, not snapping to attention: a full 180 degree
    // turn takes about 1.5s, and the common case (the player approaching from roughly in front) is
    // under a second.
    [SerializeField] float turnSpeedDegPerSec = 120f;

    Quaternion restRotation;
    Quaternion targetRotation;
    Transform trackTarget;

    void Awake() => targetRotation = restRotation = transform.rotation;

    // Turns to face `target` and keeps tracking it every frame until ReturnToRest() is called.
    // Continuous tracking rather than a one-time facing: InputLock freezes the player in place for
    // the whole conversation today, so the two behave identically, but tracking costs nothing extra
    // and stays correct if a future conversation type doesn't lock movement.
    public void FaceTarget(Transform target) => trackTarget = target;

    // Releases the target and smoothly turns back to the rotation this NPC was placed with. Safe to
    // call mid-turn or back-to-back with FaceTarget: it only ever changes where Update is steering
    // toward, never snaps the current rotation, so nothing pops.
    public void ReturnToRest()
    {
        trackTarget = null;
        targetRotation = restRotation;
    }

    void Update()
    {
        if (trackTarget != null) targetRotation = YawTowards(trackTarget.position);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeedDegPerSec * Time.deltaTime);
    }

    // Yaw only: pitch/roll would tip the body toward the player's head height.
    Quaternion YawTowards(Vector3 worldPosition)
    {
        var direction = worldPosition - transform.position;
        direction.y = 0f;
        return direction.sqrMagnitude < 0.0001f ? targetRotation : Quaternion.LookRotation(direction);
    }
}

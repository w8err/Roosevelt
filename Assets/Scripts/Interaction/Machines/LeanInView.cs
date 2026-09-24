using System.Collections;
using UnityEngine;

// Walks the player's eye up to a surface and holds it there until they back off: a CRT, the sheet
// on the wall board. The alternative was a full-screen overlay, and this is better for three
// reasons that all pull the same way.
//
// The text stays live, because it is the real screen rather than a copy of it that has to be kept
// in sync. Nothing about the interface admits to being an interface, which is the whole register
// these machines are written in. And reading costs something: nose to the glass, the room is behind
// you and you cannot see it. An overlay would have stopped time and made reading free.
public static class LeanInView
{
    public static bool IsActive { get; private set; }

    // Play mode starts without a domain reload here, so a lean left open by a stopped play session
    // must not convince the next one that the player is still at a screen.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => IsActive = false;

    // `surface` is the plane being read; the eye stops `distance` metres out along its facing.
    public static IEnumerator Look(object holder, PlayerInteraction player, Transform surface,
                                   bool facesBackwards, float distance, float moveDuration = 0.45f)
    {
        var controller = player != null ? player.GetComponent<FirstPersonController>() : null;
        if (controller == null || surface == null) yield break;

        IsActive = true;
        InputLock.Acquire(holder);

        var body = controller.transform;
        var startPosition = body.position;
        var startRotation = body.rotation;
        var startPitch = controller.Pitch;

        var feel = controller.GetComponent<PlayerCameraFeel>();
        var eyeHeight = feel != null ? feel.EyeHeight : 1.6f;
        // Freezing the CharacterController for the whole move, so the console the player is leaning
        // into can never push them and the body can be driven straight to the spot.
        controller.SetPosed(true, eyeHeight);

        // The surface's outward direction. A mesh whose normal points away from the room reports it
        // backwards, so the caller says which it is rather than this guessing from the transform.
        var outward = facesBackwards ? -surface.forward : surface.forward;
        var eyeTarget = surface.position + outward * distance;
        var endPosition = eyeTarget - Vector3.up * eyeHeight;

        var toSurface = surface.position - eyeTarget;
        var horizontal = new Vector3(toSurface.x, 0f, toSurface.z);
        var endRotation = horizontal.sqrMagnitude > 1e-6f
            ? Quaternion.LookRotation(horizontal.normalized, Vector3.up)
            : startRotation;
        var endPitch = Mathf.Atan2(-toSurface.y, Mathf.Max(horizontal.magnitude, 1e-4f)) * Mathf.Rad2Deg;

        yield return Move(controller, startPosition, endPosition, startRotation, endRotation, startPitch, endPitch, eyeHeight, moveDuration);

        // The key that started the lean is still down on this frame; skip one before watching for
        // the press that ends it.
        yield return null;
        var interact = controller.InteractAction;
        while (interact == null || !interact.WasPressedThisFrame())
            yield return null;

        yield return Move(controller, endPosition, startPosition, endRotation, startRotation, endPitch, startPitch, eyeHeight, moveDuration);

        controller.SetPosed(false);
        InputLock.Release(holder);
        IsActive = false;
    }

    static IEnumerator Move(FirstPersonController controller, Vector3 from, Vector3 to,
                            Quaternion fromRotation, Quaternion toRotation,
                            float fromPitch, float toPitch, float eyeHeight, float duration)
    {
        var body = controller.transform;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            body.SetPositionAndRotation(Vector3.Lerp(from, to, t), Quaternion.Slerp(fromRotation, toRotation, t));
            controller.SetPitch(Mathf.Lerp(fromPitch, toPitch, t));
            // Re-posed every frame so PlayerCameraFeel keeps the eye at a fixed height instead of
            // letting the standing bob drift the framing while the player is parked at the glass.
            controller.SetPosed(true, eyeHeight);
            yield return null;
        }
        body.SetPositionAndRotation(to, toRotation);
        controller.SetPitch(toPitch);
    }
}

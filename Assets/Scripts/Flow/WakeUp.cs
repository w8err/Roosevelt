using System.Collections;
using UnityEngine;

// Plays "waking up in bed" whenever the player arrives in Reality: lying facing the ceiling
// while the screen is black, a brief ceiling hold once it fades in, then standing up at the
// bed's standPosition. Hooked into ScreenTransition (afterLoad/onDone) for the day-advance and
// continue-from-save loads into Reality, and run directly (with its own fade) when Play starts
// already in Reality, since then no scene load is there to hook into. Does nothing if the scene
// has no bed with its wake anchors assigned (other test scenes).
public static class WakeUp
{
    const float CeilingHoldSeconds = 1.2f;
    const float StandUpSeconds = 1.75f;
    // Matches the small lift LabSceneBuilder gives playerStarts, so the CharacterController
    // capsule doesn't spawn embedded in the floor when it's re-enabled at the end.
    const float GroundClearance = 0.05f;

    static readonly object LockKey = new object();
    static Runner runner;
    static bool active;

    // Play mode starts without a domain reload in this project, so a sequence left running
    // from the previous session (and the lock it held) must not survive into the next one.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        active = false;
        runner = null;
        InputLock.Release(LockKey);
    }

    // For starting Play directly in Reality, where no scene load already provides a fade to
    // hook into: fades to black, plays the wake-up, fades back in. Does nothing (no fade
    // either) if the scene has no bed with wake anchors.
    public static void PlayOnBoot()
    {
        if (ScreenTransition.IsRunning) return;
        var bed = Object.FindAnyObjectByType<BedInteractable>();
        if (bed == null || !bed.HasWakeAnchors) return;
        // Play starts already showing Reality; snap black (and silent) first so neither the
        // standing scene nor its ambience flashes/plays before ScreenTransition's own fade-out
        // (which then starts from whatever this already is, so both stay at 0 the whole time).
        ScreenFader.Instance.SnapToBlack();
        Ambience.SnapTo(0f);
        ScreenTransition.Run(TransitionKind.Wake, PoseLyingRoutine, StartStandingUp);
    }

    // ScreenTransition afterLoad hook: called while the screen is still black, right after
    // Reality has finished loading. Snaps the player into the lying pose so it is already in
    // place when the screen fades in. Sets active so StartStandingUp knows whether there is
    // anything to do; no-ops if the loaded scene has no bed with wake anchors.
    public static void PoseLyingInstant()
    {
        var bed = Object.FindAnyObjectByType<BedInteractable>();
        var controller = Object.FindAnyObjectByType<FirstPersonController>();
        active = bed != null && controller != null && bed.HasWakeAnchors;
        if (!active) return;

        InputLock.Acquire(LockKey);
        controller.SetPosed(true, 0f);
        var (position, rotation, pitch) = bed.ComputeLiePose();
        controller.transform.SetPositionAndRotation(position, rotation);
        controller.SetPitch(pitch);
    }

    static IEnumerator PoseLyingRoutine()
    {
        PoseLyingInstant();
        yield break;
    }

    // ScreenTransition onDone hook: called once the screen has fully faded in and
    // ScreenTransition's own lock is already released. Holds the ceiling gaze, then stands up
    // while keeping the lock PoseLyingInstant took, so the player can't move in between.
    public static void StartStandingUp()
    {
        if (!active) return;
        EnsureRunner().StartCoroutine(StandUpRoutine());
    }

    static IEnumerator StandUpRoutine()
    {
        active = false;
        var bed = Object.FindAnyObjectByType<BedInteractable>();
        var controller = Object.FindAnyObjectByType<FirstPersonController>();
        if (bed == null || controller == null)
        {
            InputLock.Release(LockKey);
            yield break;
        }

        yield return new WaitForSeconds(CeilingHoldSeconds);

        var feel = controller.GetComponent<PlayerCameraFeel>();
        var startEyeHeight = feel != null ? feel.EyeHeight : 0f;
        var standEyeHeight = feel != null ? feel.StandEyeHeight : 1.6f;

        var body = controller.transform;
        var startPosition = body.position;
        var startRotation = body.rotation;
        var startPitch = controller.Pitch;
        var stand = bed.StandPosition;
        // Lifted slightly like LabSceneBuilder's playerStarts, so the CharacterController capsule
        // doesn't spawn embedded in the floor once it's re-enabled below.
        var standPosition = stand.position + Vector3.up * GroundClearance;

        var elapsed = 0f;
        while (elapsed < StandUpSeconds)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / StandUpSeconds));
            body.SetPositionAndRotation(Vector3.Lerp(startPosition, standPosition, t), Quaternion.Slerp(startRotation, stand.rotation, t));
            controller.SetPitch(Mathf.Lerp(startPitch, 0f, t));
            // Same t as the position/pitch lerp above, so the eye's world height moves
            // continuously; by the end it already equals standEyeHeight, so handing off to
            // SetPosed(false) below has no seam.
            controller.SetPosed(true, Mathf.Lerp(startEyeHeight, standEyeHeight, t));
            yield return null;
        }
        body.SetPositionAndRotation(standPosition, stand.rotation);
        controller.SetPitch(0f);
        controller.SetPosed(true, standEyeHeight);
        // Only now, with the player already at rest at standPosition and the eye already at
        // standing height, is it safe to re-enable the CharacterController: doing either earlier
        // would let Move() fight our manual transform writes above (gravity kicking in while
        // airborne mid-lerp, etc) or jump the eye height on handoff.
        controller.SetPosed(false);

        InputLock.Release(LockKey);
    }

    static Runner EnsureRunner()
    {
        if (runner != null) return runner;
        var go = new GameObject("WakeUpRunner");
        Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();
        return runner;
    }

    // A MonoBehaviour to host the coroutine; WakeUp itself is static and can't.
    class Runner : MonoBehaviour { }
}

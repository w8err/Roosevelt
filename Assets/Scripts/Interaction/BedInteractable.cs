using System.Collections;
using UnityEngine;

// The bed the player sleeps in to enter tonight's dream, and (via WakeUp) the same bed they
// wake up in the next morning. Locked behind requiredFlag (the lobby NPC conversation, by
// default) until then. Reality has two bed pieces (frame, mattress), each with its own
// BedInteractable carrying the same wake anchors.
public class BedInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] string requiredFlag = GameFlags.TalkedToLobbyNPC;
    [SerializeField] string prompt = "잠자기";
    [SerializeField] string notReadyPrompt = "아직 잠이 오지 않는다";

    [Header("Wake anchors (LabSceneBuilder assigns these by field name)")]
    // The lying eye's world position; its rotation is head-to-foot yaw.
    [SerializeField] Transform lieEye;
    // World point the lying gaze aims at (ceiling, toward the feet).
    [SerializeField] Transform lieLook;
    // Floor point to stand up at; its rotation faces into the room.
    [SerializeField] Transform standPosition;

    [Tooltip("Seconds to lerp position, yaw, pitch and eye height down into lying.")]
    [SerializeField] float lieTransitionDuration = 1.5f;
    [SerializeField] float lieHoldDuration = 1.25f;

    bool Ready => string.IsNullOrEmpty(requiredFlag) || GameState.HasFlag(requiredFlag);

    public Transform StandPosition => standPosition;
    public bool HasWakeAnchors => lieEye != null && lieLook != null && standPosition != null;

    public string GetPrompt() => Ready ? prompt : notReadyPrompt;
    public bool CanInteract() => Ready && !ScreenTransition.IsRunning && !Dialogue.IsPlaying;
    public void Interact(PlayerInteraction player) => StartCoroutine(LieDown());

    // The lying pose (position, yaw, pitch): eye position sits exactly at lieEye (posture eye
    // height offset 0), yaw comes from lieEye's own rotation (flattened, in case it carries any
    // tilt), and pitch is computed toward lieLook rather than trusted from lieEye's own pitch.
    // Shared by LieDown() below and by WakeUp, which uses the same anchors in reverse.
    public (Vector3 position, Quaternion rotation, float pitch) ComputeLiePose()
    {
        var position = lieEye.position;
        var rotation = Quaternion.Euler(0f, lieEye.rotation.eulerAngles.y, 0f);
        var toLook = lieLook.position - position;
        var horizontal = new Vector3(toLook.x, 0f, toLook.z);
        var pitch = Mathf.Atan2(-toLook.y, horizontal.magnitude) * Mathf.Rad2Deg;
        return (position, rotation, pitch);
    }

    IEnumerator LieDown()
    {
        var controller = FindAnyObjectByType<FirstPersonController>();

        if (controller == null)
        {
            Debug.LogError("[BedInteractable] FirstPersonController not found");
            yield break;
        }

        if (!HasWakeAnchors)
        {
            Debug.LogError($"[BedInteractable] {gameObject.name}: lieEye/lieLook/standPosition not configured");
            yield break;
        }

        InputLock.Acquire(this);
        // Disables the CharacterController and Move() for the whole sequence, so the bed's
        // collider can never push the player and we can drive transform/pitch directly below.
        // Eye height starts from wherever it actually is (standing) so entering posture is seamless.
        var feel = controller.GetComponent<PlayerCameraFeel>();
        var startEyeHeight = feel != null ? feel.EyeHeight : 0f;
        controller.SetPosed(true, startEyeHeight);

        var body = controller.transform;
        var startPosition = body.position;
        var startRotation = body.rotation;
        var startPitch = controller.Pitch;
        var (endPosition, endRotation, endPitch) = ComputeLiePose();

        var elapsed = 0f;
        while (elapsed < lieTransitionDuration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / lieTransitionDuration));
            body.SetPositionAndRotation(Vector3.Lerp(startPosition, endPosition, t), Quaternion.Slerp(startRotation, endRotation, t));
            controller.SetPitch(Mathf.Lerp(startPitch, endPitch, t));
            // Same t as the position/pitch lerp above, so the eye's world height moves continuously.
            controller.SetPosed(true, Mathf.Lerp(startEyeHeight, 0f, t));
            yield return null;
        }
        body.SetPositionAndRotation(endPosition, endRotation);
        controller.SetPitch(endPitch);
        controller.SetPosed(true, 0f);

        yield return new WaitForSeconds(lieHoldDuration);

        InputLock.Release(this);
        GameFlow.EnterDream();
    }
}

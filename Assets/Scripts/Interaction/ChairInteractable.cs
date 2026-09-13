using System.Collections;
using UnityEngine;

// The empty chair in Dream_00 where the player sits to witness the ceremony and complete 0-day.
// The woman and child sit in the other chairs. At the end, the player flags DreamSatChair,
// the camera frames all three + tree, and the scene fades to Day 1.
public class ChairInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] Transform seatPosition;
    [SerializeField] Transform cameraLookTarget;
    [Tooltip("Seconds to lerp position, yaw and pitch into the seat.")]
    [SerializeField] float sitTransitionDuration = 1.2f;
    [SerializeField] float contemplateDuration = 3.5f;
    [Tooltip("Eye height above seatPosition's floor point once seated.")]
    [SerializeField] float seatEyeHeight = 1.15f;
    [Tooltip("Ambience.Volume target while contemplating the scene (0 = silent, 1 = untouched).")]
    [Range(0f, 1f)] [SerializeField] float contemplateAmbienceVolume = 0.3f;
    [Tooltip("Seconds to ease into contemplateAmbienceVolume, within contemplateDuration.")]
    [SerializeField] float ambienceDuckDuration = 1.5f;

    public string GetPrompt() => "앉기";
    public bool CanInteract() => !GameState.HasFlag(GameFlags.DreamSatChair)
        && !ScreenTransition.IsRunning
        && !Dialogue.IsPlaying;

    public void Interact(PlayerInteraction player) => StartCoroutine(Sit());

    void OnValidate()
    {
        // Auto-find seat and camera target by name if not assigned (for editor workflow)
        if (seatPosition == null)
            seatPosition = transform.Find("SeatPosition");
        if (cameraLookTarget == null)
            cameraLookTarget = transform.Find("CameraTarget");
    }

    IEnumerator Sit()
    {
        var controller = FindAnyObjectByType<FirstPersonController>();

        if (controller == null)
        {
            Debug.LogError($"[ChairInteractable] FirstPersonController not found");
            yield break;
        }

        if (seatPosition == null || cameraLookTarget == null)
        {
            Debug.LogError($"[ChairInteractable] {gameObject.name}: seat or camera target not configured");
            yield break;
        }

        InputLock.Acquire(this);
        // Disables the CharacterController and Move() for the whole sequence, so the chair's
        // collider can never push the player and we can drive transform/pitch directly below.
        // Eye height starts from wherever it actually is (standing) so entering posture is seamless.
        var feel = controller.GetComponent<PlayerCameraFeel>();
        var startEyeHeight = feel != null ? feel.EyeHeight : seatEyeHeight;
        controller.SetPosed(true, startEyeHeight);

        var body = controller.transform;
        var startPosition = body.position;
        var startRotation = body.rotation;
        var startPitch = controller.Pitch;

        // Aim from the actual seated eye position rather than trusting seatPosition's own
        // rotation, so yaw and pitch both land exactly on cameraLookTarget.
        var endPosition = seatPosition.position;
        var eyeAtSeat = endPosition + Vector3.up * seatEyeHeight;
        var toLook = cameraLookTarget.position - eyeAtSeat;
        var horizontal = new Vector3(toLook.x, 0f, toLook.z);
        var endRotation = horizontal.sqrMagnitude > 1e-6f
            ? Quaternion.LookRotation(horizontal.normalized, Vector3.up)
            : seatPosition.rotation;
        var endPitch = Mathf.Atan2(-toLook.y, horizontal.magnitude) * Mathf.Rad2Deg;

        var elapsed = 0f;
        while (elapsed < sitTransitionDuration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / sitTransitionDuration));
            body.SetPositionAndRotation(Vector3.Lerp(startPosition, endPosition, t), Quaternion.Slerp(startRotation, endRotation, t));
            controller.SetPitch(Mathf.Lerp(startPitch, endPitch, t));
            // Same t as the position/pitch lerp above, so the eye's world height moves continuously.
            controller.SetPosed(true, Mathf.Lerp(startEyeHeight, seatEyeHeight, t));
            yield return null;
        }
        body.SetPositionAndRotation(endPosition, endRotation);
        controller.SetPitch(endPitch);
        controller.SetPosed(true, seatEyeHeight);

        // Environment sound (drone/wind) ducks down while the player contemplates the scene;
        // EndDream's own screen transition takes it the rest of the way to 0 right after.
        Ambience.FadeTo(contemplateAmbienceVolume, ambienceDuckDuration);

        // Hold this view for contemplation. PlayerCameraFeel keeps the standing breathing bob
        // on top of the now-settled seated eye height.
        yield return new WaitForSeconds(contemplateDuration);

        // Mark that player experienced the moment (before transition, safer if interrupted)
        GameState.SetFlag(GameFlags.DreamSatChair);

        // End dream: EndDream() handles fade out, scene load, save, and input unlock
        InputLock.Release(this);
        GameFlow.EndDream();
    }
}

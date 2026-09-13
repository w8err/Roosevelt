using Unity.Cinemachine;
using UnityEngine;

// Blends the camera noise from a slow breathing drift (standing) to a smooth walking bob, then a heavier sprint bob.
// The up-down bob moves the camera target here instead of living in the noise profile,
// so sprinting can deepen it without also scaling the sway and rotation.
[RequireComponent(typeof(CharacterController), typeof(FirstPersonController))]
public class PlayerCameraFeel : MonoBehaviour
{
    [SerializeField] CinemachineBasicMultiChannelPerlin noise;

    [Header("Noise gains (x = standing, y = walking)")]
    [SerializeField] Vector2 amplitude = new Vector2(0.4f, 1f);
    [SerializeField] Vector2 frequency = new Vector2(0.35f, 1f);

    [Header("Noise gains at sprint speed")]
    [SerializeField] float sprintAmplitude = 1.8f;
    [SerializeField] float sprintFrequency = 1.5f;
    [SerializeField] float blendSpeed = 3f;

    [Header("Up-down bob")]
    [Tooltip("Peak height in meters: x = standing, y = walking, z = sprinting.")]
    [SerializeField] Vector3 bobHeight = new Vector3(0.005f, 0.0125f, 0.05f);
    [Tooltip("Bobs per second at frequency gain 1. Speeds up and slows down with the frequency gains above.")]
    [SerializeField] float bobRate = 2f;

    [Header("Seating")]
    [Tooltip("Seconds to ease the eye height between standing and FirstPersonController.SeatEyeHeight.")]
    [SerializeField] float seatEyeTransitionTime = 1.2f;

    CharacterController body;
    FirstPersonController controller;
    Transform eye;
    Vector3 eyeRest;
    float standEyeHeight;
    float moveBlend; // 0 = standing, 1 = walking, 2 = sprinting
    float bobPhase;

    void Awake()
    {
        body = GetComponent<CharacterController>();
        controller = GetComponent<FirstPersonController>();
        eye = controller.CameraTarget;
        eyeRest = eye.localPosition;
        standEyeHeight = eyeRest.y;
    }

    void Update()
    {
        var seated = controller.IsSeated;
        var planar = seated ? Vector3.zero : body.velocity;
        planar.y = 0f;
        var target = !seated && body.isGrounded ? SpeedToBlend(planar.magnitude) : 0f;
        moveBlend = Mathf.MoveTowards(moveBlend, target, Time.deltaTime * blendSpeed);
        noise.AmplitudeGain = Blend(amplitude.x, amplitude.y, sprintAmplitude);
        noise.FrequencyGain = Blend(frequency.x, frequency.y, sprintFrequency);

        // Seating lowers CameraTarget's rest height; the standing breathing bob below still applies on top.
        var targetEyeY = seated ? controller.SeatEyeHeight : standEyeHeight;
        var eyeRate = Mathf.Abs(standEyeHeight - controller.SeatEyeHeight) / seatEyeTransitionTime;
        eyeRest.y = Mathf.MoveTowards(eyeRest.y, targetEyeY, eyeRate * Time.deltaTime);

        bobPhase = Mathf.Repeat(bobPhase + Time.deltaTime * bobRate * noise.FrequencyGain, 1f);
        var height = Blend(bobHeight.x, bobHeight.y, bobHeight.z);
        eye.localPosition = eyeRest + Vector3.up * (height * Mathf.Sin(bobPhase * 2f * Mathf.PI));
    }

    float SpeedToBlend(float speed)
    {
        var walk = controller.WalkSpeed;
        if (speed <= walk) return speed / walk;
        return 1f + Mathf.Clamp01((speed - walk) / (controller.SprintSpeed - walk));
    }

    float Blend(float stand, float walk, float sprint) => moveBlend <= 1f
        ? Mathf.Lerp(stand, walk, moveBlend)
        : Mathf.Lerp(walk, sprint, moveBlend - 1f);
}

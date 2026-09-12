using Unity.Cinemachine;
using UnityEngine;

// Blends the camera noise from a slow breathing drift (standing) to a smooth walking bob.
[RequireComponent(typeof(CharacterController), typeof(FirstPersonController))]
public class PlayerCameraFeel : MonoBehaviour
{
    [SerializeField] CinemachineBasicMultiChannelPerlin noise;

    [Header("Noise gains (x = standing, y = walking)")]
    [SerializeField] Vector2 amplitude = new Vector2(0.4f, 1f);
    [SerializeField] Vector2 frequency = new Vector2(0.35f, 1f);
    [SerializeField] float blendSpeed = 3f;

    CharacterController body;
    FirstPersonController controller;
    float walkBlend;

    void Awake()
    {
        body = GetComponent<CharacterController>();
        controller = GetComponent<FirstPersonController>();
    }

    void Update()
    {
        var planar = body.velocity;
        planar.y = 0f;
        var target = body.isGrounded ? Mathf.Clamp01(planar.magnitude / controller.WalkSpeed) : 0f;
        walkBlend = Mathf.MoveTowards(walkBlend, target, Time.deltaTime * blendSpeed);
        noise.AmplitudeGain = Mathf.Lerp(amplitude.x, amplitude.y, walkBlend);
        noise.FrequencyGain = Mathf.Lerp(frequency.x, frequency.y, walkBlend);
    }
}

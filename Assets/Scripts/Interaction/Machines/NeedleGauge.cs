using UnityEngine;

// Shows a machine's state as a needle and nothing else. Deliberately not a HUD number: reading
// the room instead of reading an overlay is what makes an operator out of the player, the way
// Iron Lung's sub has no window and only instruments.
public class NeedleGauge : MonoBehaviour
{
    [Tooltip("The separated needle mesh. Its own origin must sit on the pivot at one end.")]
    [SerializeField] Transform needle;
    [Tooltip("Needle yaw around local Y at value 0, then at value 1.")]
    [SerializeField] float minAngle = -120f;
    [SerializeField] float maxAngle = 120f;
    [Tooltip("Seconds for the needle to chase a changed value. A slow needle reads as mechanical.")]
    [SerializeField] float responseTime = 0.45f;
    [Tooltip("Degrees of idle flutter, so a live gauge never looks frozen.")]
    [SerializeField] float jitter = 1.2f;

    float target;
    float shown;

    public float Value => shown;

    void Awake()
    {
        if (needle == null) needle = transform;
    }

    void Update()
    {
        shown = responseTime > 0f
            ? Mathf.Lerp(shown, target, 1f - Mathf.Exp(-Time.deltaTime / responseTime))
            : target;
        // PerlinNoise rather than Random so the flutter is smooth instead of buzzing.
        var flutter = jitter * (Mathf.PerlinNoise(Time.time * 3.1f, 0f) - 0.5f) * 2f;
        var angle = Mathf.Lerp(minAngle, maxAngle, Mathf.Clamp01(shown)) + flutter * shown;
        var euler = needle.localEulerAngles;
        needle.localEulerAngles = new Vector3(euler.x, angle, euler.z);
    }

    public void SetValue(float value01) => target = Mathf.Clamp01(value01);

    // Parks the needle where a reading of 0 puts it. The mesh is authored pointing straight up,
    // which on a +-120 dial is mid-scale, so a scene that has never been played shows a pressure the
    // machine never has at rest. The builder calls this so a review capture shows the resting state
    // instead of a pose that only looks deliberate.
    public void ApplyRestPose()
    {
        var t = needle != null ? needle : transform;
        var euler = t.localEulerAngles;
        t.localEulerAngles = new Vector3(euler.x, minAngle, euler.z);
    }

    // Skips the chase, for a gauge that must read correctly the frame a scene opens.
    public void SnapTo(float value01)
    {
        target = shown = Mathf.Clamp01(value01);
    }
}

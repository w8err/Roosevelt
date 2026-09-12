using UnityEngine;

// Lives on the hinge pivot; the leaf and its handle are children offset from it.
public class LabDoor : MonoBehaviour
{
    [SerializeField] float openAngle = 90f;
    [SerializeField] float swingSeconds = 0.8f;
    [SerializeField] bool locked;

    bool isOpen;
    float progress;

    public void Toggle()
    {
        if (locked)
        {
            Debug.Log($"[LabDoor] {name} is locked");
            return;
        }
        isOpen = !isOpen;
    }

    void Update()
    {
        var target = isOpen ? 1f : 0f;
        if (progress == target) return;
        progress = Mathf.MoveTowards(progress, target, Time.deltaTime / swingSeconds);
        transform.localRotation = Quaternion.Euler(0f, Mathf.SmoothStep(0f, openAngle, progress), 0f);
    }
}

using UnityEngine;

// Keeps a screen-space canvas at a whole-number scale so pixel fonts land on the pixel grid.
// One UI unit is Screen.height / PixelUI.VirtualHeight real pixels, rounded down (1080p → 3).
[RequireComponent(typeof(Canvas))]
public class PixelCanvasScaler : MonoBehaviour
{
    Canvas canvas;

    void Awake() => canvas = GetComponent<Canvas>();

    void LateUpdate()
    {
        var scale = Mathf.Max(1, Screen.height / PixelUI.VirtualHeight);
        if (canvas.scaleFactor != scale) canvas.scaleFactor = scale;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Bottom-left stamina gauge: an outline marks 100%, a fill shows what is left.
// Red while exhausted, shakes when a sprint is refused, fades out once full.
// The overlay canvas is built at runtime, so scenes need no UI of their own.
[RequireComponent(typeof(FirstPersonController))]
public class StaminaBar : MonoBehaviour
{
    [Tooltip("Outer size in reference pixels (1920x1080).")]
    [SerializeField] Vector2 size = new Vector2(240f, 12f);
    [Tooltip("Distance from the bottom-left corner of the screen.")]
    [SerializeField] Vector2 margin = new Vector2(40f, 40f);
    [SerializeField] float outline = 2f;
    [Tooltip("Gap between the outline and the fill.")]
    [SerializeField] float padding = 2f;

    [Header("Colors")]
    [SerializeField] Color normalColor = Color.white;
    [Tooltip("From hitting 0% until full again. Red from the project palette.")]
    [SerializeField] Color exhaustedColor = new Color32(0xd6, 0x4c, 0x4c, 0xff);

    [Header("Fade")]
    [Tooltip("Seconds the bar stays at 100% before fading out.")]
    [SerializeField] float fadeDelay = 0.5f;
    [SerializeField] float fadeOutTime = 0.6f;
    [SerializeField] float fadeInTime = 0.1f;

    [Header("Shake (sprint refused while exhausted)")]
    [SerializeField] float shakeDistance = 6f;
    [Tooltip("Left-right swings per second.")]
    [SerializeField] float shakeFrequency = 12f;
    [SerializeField] float shakeDuration = 0.35f;

    FirstPersonController controller;
    GameObject canvas;
    RectTransform frame, fill;
    CanvasGroup group;
    Vector2 framePosition;
    readonly List<Image> images = new List<Image>();
    bool red;
    // Start as if long full and long done shaking, so the bar begins hidden and still.
    float fullTime = float.PositiveInfinity, shakeElapsed = float.PositiveInfinity;

    void Awake()
    {
        controller = GetComponent<FirstPersonController>();
        canvas = BuildCanvas();
        group.alpha = 0f;
    }

    void OnEnable() => controller.SprintDenied += Shake;
    void OnDisable() => controller.SprintDenied -= Shake;

    void Shake() => shakeElapsed = 0f;

    void LateUpdate()
    {
        canvas.SetActive(controller.UseStamina);
        if (!controller.UseStamina) return;

        var dt = Time.deltaTime;
        var stamina = controller.Stamina01;
        fill.anchorMax = new Vector2(stamina, 1f);

        if (red != controller.Exhausted)
        {
            red = controller.Exhausted;
            foreach (var image in images) image.color = red ? exhaustedColor : normalColor;
        }

        fullTime = stamina < 1f ? 0f : fullTime + dt;
        var fadingOut = fullTime > fadeDelay;
        group.alpha = Mathf.MoveTowards(group.alpha, fadingOut ? 0f : 1f, dt / (fadingOut ? fadeOutTime : fadeInTime));

        // Damped sine: strongest at the start, settling back to rest by the end.
        shakeElapsed += dt;
        var offset = shakeElapsed < shakeDuration
            ? shakeDistance * (1f - shakeElapsed / shakeDuration) * Mathf.Sin(shakeElapsed * shakeFrequency * 2f * Mathf.PI)
            : 0f;
        frame.anchoredPosition = framePosition + new Vector2(offset, 0f);
    }

    GameObject BuildCanvas()
    {
        var root = new GameObject("StaminaBar", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        var c = root.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.pixelPerfect = true;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        frame = Panel("Frame", root.transform, Vector2.zero, Vector2.zero, margin, margin + size, false);
        framePosition = frame.anchoredPosition;
        group = frame.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        // Outline as four strips so the inside stays see-through.
        Panel("Top", frame, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -outline), Vector2.zero, true);
        Panel("Bottom", frame, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, outline), true);
        Panel("Left", frame, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, outline), new Vector2(outline, -outline), true);
        Panel("Right", frame, new Vector2(1f, 0f), Vector2.one, new Vector2(-outline, outline), new Vector2(0f, -outline), true);

        var inset = outline + padding;
        var track = Panel("Track", frame, Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset), false);
        fill = Panel("Fill", track, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
        return root;
    }

    RectTransform Panel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, bool drawn)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        if (drawn)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = normalColor;
            images.Add(image);
        }
        return rect;
    }
}

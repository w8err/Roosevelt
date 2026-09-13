using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Fullscreen black overlay used by ScreenTransition. Built once at runtime and kept alive
// across scene loads (DontDestroyOnLoad) so a fade never needs rebuilding mid-transition.
public class ScreenFader : MonoBehaviour
{
    // Above PlayerInteraction's HUD (sortingOrder 10) and any other gameplay UI.
    const int SortingOrder = 1000;

    static ScreenFader instance;
    CanvasGroup group;
    Text captionText;

    public static ScreenFader Instance
    {
        get
        {
            if (instance == null) Build();
            return instance;
        }
    }

    // Title-card text (e.g. "Day 1") shown while the screen is fully black. Hidden by default;
    // ScreenTransition activates it only for transitions that pass a caption.
    public Text Caption
    {
        get
        {
            if (captionText == null) BuildCaption();
            return captionText;
        }
    }

    void BuildCaption()
    {
        captionText = PixelUI.CreateText("Caption", transform);
        captionText.fontSize = PixelUI.FontSize * 2;
        var rect = (RectTransform)captionText.transform;
        rect.sizeDelta = new Vector2(320f, captionText.fontSize + 4f);
        rect.anchoredPosition = new Vector2(0f, rect.sizeDelta.y * 0.5f);
        captionText.gameObject.SetActive(false);
    }

    // Play mode starts without a domain reload in this project, so a stale reference to a
    // destroyed instance would otherwise survive between runs.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => instance = null;

    static void Build()
    {
        var canvas = new GameObject("ScreenFader").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        DontDestroyOnLoad(canvas.gameObject);

        var image = new GameObject("Black", typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(canvas.transform, false);
        image.color = Color.black;
        image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        instance = canvas.gameObject.AddComponent<ScreenFader>();
        instance.group = canvas.gameObject.AddComponent<CanvasGroup>();
        instance.group.alpha = 0f;
        instance.group.blocksRaycasts = false;
        instance.group.interactable = false;
    }

    public IEnumerator FadeTo(float target, float seconds)
    {
        var start = group.alpha;
        if (seconds <= 0f)
        {
            group.alpha = target;
            yield break;
        }
        var elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(start, target, elapsed / seconds);
            yield return null;
        }
        group.alpha = target;
    }
}

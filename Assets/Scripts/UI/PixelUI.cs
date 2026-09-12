using UnityEngine;
using UnityEngine.UI;

// Shared pieces for runtime-built HUDs drawn in the Galmuri pixel font.
public static class PixelUI
{
    // Virtual screen height in UI units; PixelCanvasScaler maps it to whole real pixels.
    public const int VirtualHeight = 360;
    // Galmuri11 is drawn on a 12px grid, so this size times the whole-number canvas scale stays crisp.
    public const int FontSize = 12;
    const string FontPath = "Fonts/Galmuri11";

    static Font font;

    // Galmuri from Resources, or Unity's built-in font until Galmuri has been imported.
    public static Font Font
    {
        get
        {
            if (font == null) font = Resources.Load<Font>(FontPath);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    // Statics survive between play sessions here; look the font up again in case it was imported since.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => font = null;

    public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = sortingOrder;
        go.AddComponent<PixelCanvasScaler>();
        return canvas;
    }

    // Centered on the parent with the pivot at the top, so lines grow downward from anchoredPosition.
    public static Text CreateText(string name, Transform parent)
    {
        var rect = NewRect(name, parent);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(320f, FontSize + 4f);
        var text = rect.gameObject.AddComponent<Text>();
        text.font = Font;
        text.fontSize = FontSize;
        text.alignment = TextAnchor.UpperCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        // A one-unit drop shadow keeps white text readable against bright walls.
        var shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(1f, -1f);
        return text;
    }

    // A plain white box (no sprite), centered on the parent.
    public static Image CreateImage(string name, Transform parent, Vector2 size)
    {
        var rect = NewRect(name, parent);
        rect.sizeDelta = size;
        var image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        return rect;
    }
}

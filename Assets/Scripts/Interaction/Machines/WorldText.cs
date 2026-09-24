using UnityEngine;
using UnityEngine.UI;

// Builds a world-space pixel-font panel on a surface in the room: a CRT's glass, the sheet clipped
// to the wall board. Used instead of a full-screen overlay so that reading is something the player
// does by walking up to an object, not something the interface does for them.
//
// Scenes carry no UI of their own, so these are built at runtime like every other HUD here.
public static class WorldText
{
    // Creates a canvas of `widthUnits` x `heightUnits` UI units, scaled so it covers
    // widthUnits*unitScale metres. Fewer units over the same surface means bigger glyphs.
    public static Text Create(Transform parent, Vector3 localPosition, bool faceBackwards,
                              float widthUnits, float heightUnits, float unitScale,
                              Color textColor, Color backgroundColor, TextAnchor alignment)
    {
        var go = new GameObject("WorldTextCanvas", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(widthUnits, heightUnits);
        rect.localPosition = localPosition;
        rect.localScale = Vector3.one * unitScale;
        // A canvas is read from its own +Z. Which way that has to point depends on which way the
        // surface mesh faces, so it stays a flag rather than an assumption baked into the code.
        rect.localRotation = faceBackwards ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        if (backgroundColor.a > 0f)
        {
            var back = PixelUI.CreateImage("Background", rect, new Vector2(widthUnits, heightUnits));
            back.color = backgroundColor;
        }

        var text = PixelUI.CreateText("Body", rect);
        text.color = textColor;
        text.alignment = alignment;
        // PixelUI's default is Overflow, which suits a caption centred on screen and not text inside
        // a box: a long line ran straight off the glass and floated in the room.
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        text.rectTransform.sizeDelta = new Vector2(-6f, -5f);
        text.rectTransform.anchoredPosition = Vector2.zero;
        return text;
    }
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Developer overlay in the top-left corner: F1 shows or hides it. While it is open, F2 enters
// tonight's dream, F3 wakes into the next day, F5 saves, F9 continues from the save.
// Lists the active scene, Day and every flag that is set.
public sealed class DebugPanel : MonoBehaviour
{
    GameObject panel;
    Text text;
    bool dirty = true;

    void Awake()
    {
        var canvas = PixelUI.CreateCanvas("DebugPanel", transform, short.MaxValue);
        panel = canvas.gameObject;
        text = PixelUI.CreateText("Info", canvas.transform);
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(6f, -6f);
        text.alignment = TextAnchor.UpperLeft;
        panel.SetActive(false);
    }

    void OnEnable()
    {
        GameState.Changed += MarkDirty;
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    void OnDisable()
    {
        GameState.Changed -= MarkDirty;
        SceneManager.activeSceneChanged -= OnSceneChanged;
    }

    void MarkDirty() => dirty = true;
    void OnSceneChanged(Scene from, Scene to) => dirty = true;

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.f1Key.wasPressedThisFrame) panel.SetActive(!panel.activeSelf);
        // The other keys only work with the panel open, so a stray press can't skip a day or save.
        if (!panel.activeSelf) return;
        // Walk the flow without a bed or a chair in the scene.
        if (keyboard.f2Key.wasPressedThisFrame) GameFlow.EnterDream();
        if (keyboard.f3Key.wasPressedThisFrame) GameFlow.EndDream();
        if (keyboard.f5Key.wasPressedThisFrame)
        {
            SaveSystem.Save();
            Debug.Log($"[DebugPanel] Saved Day {GameState.Day} to {SaveSystem.DefaultPath}");
        }
        if (keyboard.f9Key.wasPressedThisFrame) GameFlow.ContinueFromSave();
    }

    void LateUpdate()
    {
        if (!dirty || !panel.activeSelf) return;
        dirty = false;
        var scene = SceneManager.GetActiveScene().name;
        var flags = GameState.Capture().flags;
        text.text = $"Scene: {scene} ({(SceneNames.IsDream(scene) ? "dream" : "awake")})\n" +
                    $"Day: {GameState.Day}\n" +
                    $"Flags: {(flags.Count == 0 ? "-" : string.Join(", ", flags))}\n" +
                    "F2 dream / F3 wake / F5 save / F9 load";
    }
}
#endif

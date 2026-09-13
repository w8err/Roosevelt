using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Bottom-of-screen dialogue box: a typed-out line of text and — when the current node offers
// any — a numbered choice list below it. Built once at runtime like ScreenFader and kept alive
// across scene loads. The node's speaker stays in the data for identification but is never
// shown: the player is not told who anyone is.
public class DialogueView : MonoBehaviour
{
    // Above the gameplay HUD (10), below the transition fader (1000).
    const int SortingOrder = 500;
    const float SecondsPerChar = 0.02f;
    const int MaxChoices = 4; // enough for a 1-4 number-key shortcut
    // Choices appear dimmed once the line is fully typed and only take input after this many
    // seconds without a confirm press, so mashing through the lines can't pick choice 1.
    const float ChoiceArmDelay = 0.5f;

    static DialogueView instance;

    public static DialogueView Instance
    {
        get
        {
            if (instance == null) Build();
            return instance;
        }
    }

    // Play mode starts without a domain reload in this project, so a stale reference to a
    // destroyed instance would otherwise survive between runs.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => instance = null;

    Text bodyText, choicesText;
    Color choicesColor;
    DialogueRunner runner;
    InputAction interact;
    string fullBody = "";
    int revealedChars;
    float revealTimer;
    int selected;
    int openedOnFrame;
    bool pendingFinish;
    bool choicesShown, choicesArmed;
    float armTimer;

    public void Show(DialogueRunner runner)
    {
        this.runner = runner;
        interact = FindAnyObjectByType<FirstPersonController>()?.InteractAction;
        // The same key press that made an NPC call Dialogue.Play() must not also advance the
        // line it just opened.
        openedOnFrame = Time.frameCount;
        gameObject.SetActive(true);
        DisplayCurrent();
    }

    public void Hide()
    {
        runner = null;
        gameObject.SetActive(false);
    }

    void DisplayCurrent()
    {
        var node = runner.Current;
        fullBody = node.text ?? "";
        revealedChars = 0;
        revealTimer = 0f;
        selected = 0;
        choicesShown = choicesArmed = false;
        RenderChoices();
    }

    void Update()
    {
        if (runner == null) return;

        revealTimer += Time.deltaTime;
        while (revealTimer >= SecondsPerChar && revealedChars < fullBody.Length)
        {
            revealTimer -= SecondsPerChar;
            revealedChars++;
        }
        bodyText.text = fullBody.Substring(0, revealedChars);

        if (Time.frameCount == openedOnFrame) return;

        var revealed = revealedChars >= fullBody.Length;
        var choices = runner.VisibleChoices;
        if (choices.Count > 0 && revealed)
        {
            if (!choicesShown)
            {
                choicesShown = true;
                armTimer = 0f;
                RenderChoices();
                return;
            }
            if (!choicesArmed)
            {
                // Every press while the choices are still dim restarts the wait.
                if (ConfirmPressed() || NumberPressed(choices.Count) >= 0) armTimer = 0f;
                else armTimer += Time.deltaTime;
                if (armTimer < ChoiceArmDelay) return;
                choicesArmed = true;
                RenderChoices();
                return;
            }
            Navigate(choices.Count);
            if (ChoiceConfirmed(choices.Count)) Step(() => runner.Choose(selected));
        }
        else if (ConfirmPressed())
        {
            // Skips the typing; on a choice node the choices then appear (still dim) next frame.
            if (!revealed) revealedChars = fullBody.Length;
            else Step(() => runner.Advance());
        }
    }

    void Navigate(int count)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        var moved = false;
        if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) { selected = (selected + 1) % count; moved = true; }
        if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) { selected = (selected - 1 + count) % count; moved = true; }
        if (moved) RenderChoices();
    }

    // A number key jumps straight to that choice and confirms it; otherwise the confirm key
    // (Interact / Enter / Space / click) picks whichever choice is highlighted.
    bool ChoiceConfirmed(int count)
    {
        var number = NumberPressed(count);
        if (number >= 0) { selected = number; return true; }
        return ConfirmPressed();
    }

    // Index of the choice whose number key went down this frame, or -1.
    static int NumberPressed(int count)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return -1;
        for (var i = 0; i < count && i < MaxChoices; i++)
            if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) return i;
        return -1;
    }

    bool ConfirmPressed()
    {
        if (interact != null && interact.WasPressedThisFrame()) return true;
        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) return true;
        var mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }

    void Step(System.Action advance)
    {
        advance();
        if (runner.IsFinished) pendingFinish = true;
        else DisplayCurrent();
    }

    // Deferred so every Update this frame (including the interactable that reads
    // Dialogue.IsPlaying/InputLock) still sees the conversation as open. Otherwise the same
    // key press that closed the last line could immediately reopen it: script execution
    // order between DialogueView and PlayerInteraction isn't guaranteed.
    void LateUpdate()
    {
        if (!pendingFinish) return;
        pendingFinish = false;
        Dialogue.NotifyFinished();
    }

    // Hidden while the line is still typing, dim and without a cursor until armed.
    void RenderChoices()
    {
        var choices = runner.VisibleChoices;
        if (!choicesShown || choices.Count == 0) { choicesText.text = ""; return; }
        var lines = new string[choices.Count];
        for (var i = 0; i < choices.Count; i++)
            lines[i] = (choicesArmed && i == selected ? "> " : "  ") + $"{i + 1}. {choices[i].text}";
        // No trailing newline: with LowerLeft alignment an extra blank line would push every
        // visible line up by one, toward the body text above.
        choicesText.text = string.Join("\n", lines);
        choicesText.color = choicesArmed ? choicesColor : new Color(choicesColor.r, choicesColor.g, choicesColor.b, choicesColor.a * 0.4f);
    }

    static void Build()
    {
        var canvas = PixelUI.CreateCanvas("DialogueView", null, SortingOrder);
        DontDestroyOnLoad(canvas.gameObject);
        instance = canvas.gameObject.AddComponent<DialogueView>();

        // Tall enough for the worst case in either direction: a 3-line body with no choices,
        // or a 1-line body (a choice node's prompt) above up to 4 choice lines.
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);
        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.sizeDelta = new Vector2(320f, 150f);
        panelRect.anchoredPosition = new Vector2(0f, 8f);
        var background = panel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.75f);
        background.raycastTarget = false;

        // No speaker line, so the body starts at the top of the box.
        instance.bodyText = PixelUI.CreateText("Body", panel.transform);
        Place(instance.bodyText, new Vector2(6f, -6f), 72f, TextAnchor.UpperLeft);
        instance.bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;

        instance.choicesText = PixelUI.CreateText("Choices", panel.transform);
        Place(instance.choicesText, new Vector2(6f, 4f), 64f, TextAnchor.LowerLeft, fromBottom: true);
        instance.choicesColor = instance.choicesText.color;

        canvas.gameObject.SetActive(false);
    }

    // PixelUI.CreateText centers its result on the parent; the dialogue box instead pins each
    // line to a corner with its own height, so speaker, body and choices stack without overlapping.
    static void Place(Text text, Vector2 anchoredPosition, float height, TextAnchor alignment, bool fromBottom = false)
    {
        var pivot = new Vector2(0f, fromBottom ? 0f : 1f);
        var rect = (RectTransform)text.transform;
        rect.anchorMin = rect.anchorMax = pivot;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(308f, height);
        text.alignment = alignment;
    }
}

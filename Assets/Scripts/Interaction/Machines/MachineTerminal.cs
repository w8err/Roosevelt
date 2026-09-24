using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The CRT bolted to a console. It builds its own world-space canvas on the kit's blank screen plane
// at runtime, so the kit ships no text and scenes carry no UI of their own.
//
// Interact leans the player in until the glass fills the view, rather than opening a copy of the
// readout on a full-screen panel. The machine keeps printing while they are down there: it is the
// real screen, so there is no second copy to keep in sync, and the player cannot watch the room
// while they read.
public class MachineTerminal : MonoBehaviour, IInteractable
{
    // UI units across the glass, multiplied by unitScale to reach metres. Fewer units over the same
    // surface means bigger glyphs, so this is the knob for readable-but-contained text: at 12 px per
    // Hangul glyph, 108 units is 9 characters a line.
    const float ScreenWidthUnits = 108f;
    const float ScreenHeightUnits = 81f;
    const int GlassLines = 6;

    [Tooltip("Metres per UI unit. 0.00296 fits the 32x24cm CRT screen plane in the LabPanel kit.")]
    [SerializeField] float unitScale = 0.00296f;
    [Tooltip("True when the screen plane's own +Z points into the room. The kit's CRT_Screen does.")]
    [SerializeField] bool screenFacesPlayer = true;
    [Tooltip("Metres from the glass the eye stops at. 0.34 fills most of the view with a 32cm screen.")]
    [SerializeField] float readDistance = 0.34f;
    [Tooltip("Phosphor colour for text.")]
    [SerializeField] Color textColor = new Color(0.65f, 1f, 0.72f);
    [Tooltip("Dark glass behind the text, so an off screen still reads as a screen.")]
    [SerializeField] Color screenColor = new Color(0.03f, 0.05f, 0.04f, 1f);
    [Tooltip("Seconds per line while a reboot prints, so a diagnostic takes real time.")]
    [SerializeField] float lineDelay = 0.35f;
    [Tooltip("Label under the crosshair.")]
    [SerializeField] string displayName = "단말";

    Text body;
    Coroutine printing;
    string[] lines = System.Array.Empty<string>();
    int printed;
    bool reading;

    // True while a reboot or diagnostic is still printing; machines refuse input until it ends.
    public bool IsBusy => printing != null;

    void Awake() => Build();

    public string GetPrompt() => $"{displayName} 보기";
    public bool CanInteract() => !reading && !LeanInView.IsActive && !ScreenTransition.IsRunning && !Dialogue.IsPlaying;

    public void Interact(PlayerInteraction player) => StartCoroutine(Read(player));

    IEnumerator Read(PlayerInteraction player)
    {
        reading = true;
        // The screen keeps updating underneath: nothing is snapshotted, because there is nothing to
        // snapshot. A diagnostic that finishes while the player is leaning in finishes in front of them.
        yield return LeanInView.Look(this, player, transform, !screenFacesPlayer, readDistance);
        reading = false;
    }

    void OnDisable()
    {
        // A scene unload mid-read must not leave the player frozen.
        if (!reading) return;
        InputLock.Release(this);
        reading = false;
    }

    void Build()
    {
        // A world-space canvas is read from its own -Z, so on a plane whose normal points into the
        // room the canvas has to be turned to face the other way.
        body = WorldText.Create(transform, Vector3.zero, screenFacesPlayer,
                                ScreenWidthUnits, ScreenHeightUnits, unitScale,
                                textColor, screenColor, TextAnchor.UpperLeft);
    }

    public void Show(params string[] text)
    {
        StopPrinting();
        lines = text ?? System.Array.Empty<string>();
        printed = lines.Length;
        Draw();
    }

    public void Clear() => Show();

    // The self-check the player runs to find out whether the batch was mixed right. Printing one
    // line at a time is the whole point: the verdict arrives last.
    public void PrintSequence(params string[] text)
    {
        StopPrinting();
        lines = text ?? System.Array.Empty<string>();
        printed = 0;
        Draw();
        printing = StartCoroutine(PrintLines());
    }

    void StopPrinting()
    {
        if (printing == null) return;
        StopCoroutine(printing);
        printing = null;
    }

    IEnumerator PrintLines()
    {
        while (printed < lines.Length)
        {
            printed++;
            Draw();
            yield return new WaitForSeconds(lineDelay);
        }
        printing = null;
    }

    // The glass shows the tail of what has printed, so the newest line stays visible even when a
    // readout runs longer than the lines that fit.
    void Draw()
    {
        if (body == null) return;
        var end = Mathf.Clamp(printed, 0, lines.Length);
        var start = Mathf.Max(0, end - GlassLines);
        body.text = string.Join("\n", lines, start, end - start);
    }
}

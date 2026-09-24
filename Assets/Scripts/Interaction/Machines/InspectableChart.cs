using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The data board on the wall. The kit ships the board as a blank sheet under a clip and the table
// is printed onto it at runtime, at a size that is actually readable, instead of being baked into a
// texture at 64 px/m where the digits would be smaller than a pixel font's own glyphs.
//
// Interact leans the player in, the same way a CRT does, so every read in this room is something
// done by walking up to a thing in it. Nothing is copied into a journal: stepping back means
// carrying the digits in your head, which is the only reason setting three dials is a task at all.
public class InspectableChart : MonoBehaviour, IInteractable
{
    // The sheet is 84 x 112 cm of usable paper. 300 units across gives a table of 4 rows x 6 columns
    // of three digits room to breathe while staying legible from readDistance.
    const float SheetWidthUnits = 300f;
    const float SheetHeightUnits = 380f;

    [Tooltip("Where the rows come from. The same object the mixer checks against.")]
    [SerializeField] ProcedureChart chart;
    [Tooltip("Label under the crosshair.")]
    [SerializeField] string displayName = "대조표";
    [Tooltip("Extra lines under the table, e.g. a procedure reminder.")]
    [SerializeField] string[] footnotes;
    [Tooltip("Sheet position in the board's local space. The kit's paper face sits at z +0.02.")]
    [SerializeField] Vector3 sheetOffset = new Vector3(0f, -0.03f, 0.021f);
    [Tooltip("Metres per UI unit. 300 units x 0.0028 covers the 84cm sheet.")]
    [SerializeField] float unitScale = 0.0028f;
    [Tooltip("True when the sheet's own +Z points into the room. The kit's ChartBoard front does.")]
    [SerializeField] bool sheetFacesPlayer = true;
    [Tooltip("Metres from the sheet the eye stops at. Further out than a CRT because the sheet is bigger.")]
    [SerializeField] float readDistance = 0.85f;
    [Tooltip("Ink colour on the printed sheet.")]
    [SerializeField] Color inkColor = new Color(0.16f, 0.15f, 0.13f);

    Text sheet;
    bool reading;

    public string GetPrompt() => $"{displayName} 보기";
    public bool CanInteract() => !reading && !LeanInView.IsActive && !ScreenTransition.IsRunning && !Dialogue.IsPlaying;

    void Awake()
    {
        // No background: the kit's own paper shows through, so the table reads as printed on it
        // rather than as a panel floating in front of it.
        sheet = WorldText.Create(transform, sheetOffset, sheetFacesPlayer,
                                 SheetWidthUnits, SheetHeightUnits, unitScale,
                                 inkColor, Color.clear, TextAnchor.UpperCenter);
        sheet.lineSpacing = 1.5f;
        sheet.text = BuildSheet();
    }

    string BuildSheet()
    {
        var rows = chart != null ? chart.RenderRows() : new[] { "대조표 없음" };
        var body = string.Join("\n", rows);
        if (footnotes != null && footnotes.Length > 0)
            body += "\n\n" + string.Join("\n", footnotes);
        return body;
    }

    public void Interact(PlayerInteraction player) => StartCoroutine(Read(player));

    IEnumerator Read(PlayerInteraction player)
    {
        reading = true;
        yield return LeanInView.Look(this, player, transform, !sheetFacesPlayer, readDistance);
        reading = false;
    }

    void OnDisable()
    {
        if (!reading) return;
        InputLock.Release(this);
        reading = false;
    }
}

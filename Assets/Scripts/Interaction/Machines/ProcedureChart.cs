using UnityEngine;

// The lookup table that the wall board shows and the mixer checks against. One object owns both
// sides so the board can never disagree with the machine, which would read as a bug rather than
// as the player's mistake.
//
// This is the Carbon Steel move: the analyzer prints a spec and an index, the board turns that
// pair into three digits, and the player is the only thing carrying those digits across the room.
public class ProcedureChart : MonoBehaviour
{
    [Tooltip("Rows on the board. The analyzer reports one of these.")]
    [SerializeField] string[] specLabels = { "A", "B", "C", "D" };
    [Tooltip("Columns on the board, labelled 0..n-1.")]
    [SerializeField] int indexCount = 6;
    [Tooltip("Digits per cell = dials on the mixer.")]
    [SerializeField] int digitsPerCell = 3;
    [Tooltip("Changes every cell in the table. Bump it so a tester cannot play from memory.")]
    [SerializeField] int seed = 4417;

    public int SpecCount => specLabels.Length;
    public int IndexCount => indexCount;
    public int DigitsPerCell => digitsPerCell;

    public string SpecLabel(int spec) =>
        specLabels.Length == 0 ? "?" : specLabels[Mathf.Clamp(spec, 0, specLabels.Length - 1)];

    // A hash rather than a stored array: the table is reproducible from the seed, so the board
    // and the mixer derive the same digits without a serialized blob to keep in sync.
    public int[] Lookup(int spec, int index)
    {
        var digits = new int[digitsPerCell];
        for (var d = 0; d < digitsPerCell; d++)
        {
            // unchecked so the wraparound is deliberate rather than an exception in a checked
            // build; the result only has to be well spread, not cryptographic. Masking the sign
            // bit instead of Mathf.Abs, which throws on int.MinValue.
            unchecked
            {
                var h = seed;
                h = h * 31 + spec;
                h = h * 31 + index;
                h = h * 31 + d;
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                digits[d] = (h & 0x7FFFFFFF) % 10;
            }
        }
        return digits;
    }

    public bool Matches(int spec, int index, int[] entered)
    {
        if (entered == null || entered.Length != digitsPerCell) return false;
        var want = Lookup(spec, index);
        for (var d = 0; d < digitsPerCell; d++)
            if (entered[d] != want[d]) return false;
        return true;
    }

    // Lines for InspectableChart to draw. Built here so the board is always the real table.
    public string[] RenderRows()
    {
        var rows = new string[specLabels.Length + 3];
        rows[0] = "배합 대조표  COMPOUND INDEX";
        rows[1] = "";
        var header = "SPEC \\ IDX ";
        for (var i = 0; i < indexCount; i++) header += $"  {i}  ";
        rows[2] = header;
        for (var s = 0; s < specLabels.Length; s++)
        {
            var line = $"    {specLabels[s]}      ";
            for (var i = 0; i < indexCount; i++)
            {
                var digits = Lookup(s, i);
                var cell = "";
                foreach (var d in digits) cell += d.ToString();
                line += $" {cell} ";
            }
            rows[s + 3] = line;
        }
        return rows;
    }
}

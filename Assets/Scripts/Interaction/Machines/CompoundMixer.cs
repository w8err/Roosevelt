using UnityEngine;

// Machine 2 of the chain: enter the digits the wall chart gives for the analyzer's spec and
// index, then commit.
//
// It deliberately does NOT reject a wrong batch. Committing always produces a compound and the
// CRT always says it finished, because the point of the chain is that a wrong digit is only
// discovered later, at the run console, by which time the player has already carried it across
// the room. "The difference between life and death can be any overlooked detail."
public class CompoundMixer : MonoBehaviour
{
    [Tooltip("One dial per digit in the chart's cells, left to right.")]
    [SerializeField] RotaryDial[] dials;
    [SerializeField] PushButton commitButton;
    [SerializeField] MachineTerminal terminal;
    [SerializeField] ProcedureChart chart;
    [SerializeField] SampleAnalyzer analyzer;
    [Tooltip("Where the finished batch appears.")]
    [SerializeField] Socket outputSocket;
    [Tooltip("The compound part, sitting inactive in the scene until a batch is made.")]
    [SerializeField] Carryable compound;

    // Whether the batch now in the world was mixed from the right digits. The run console asks.
    public bool LastBatchCorrect { get; private set; }
    public bool HasBatch { get; private set; }

    void Awake()
    {
        if (commitButton != null) commitButton.Pressed += Commit;
        foreach (var dial in dials)
            if (dial != null) dial.Changed += ShowDials;
    }

    void OnDestroy()
    {
        if (commitButton != null) commitButton.Pressed -= Commit;
        foreach (var dial in dials)
            if (dial != null) dial.Changed -= ShowDials;
    }

    void Start()
    {
        if (compound != null) compound.gameObject.SetActive(false);
        ShowDials();
    }

    void ShowDials()
    {
        // The dials' own marks are readable up close, but the digits belong on the screen too:
        // squinting at three knobs from one standing spot is fiddly, not tense.
        terminal?.Show("RS-MIXER", "", $"설정   {Entered()}", "", "확정 버튼");
    }

    string Entered()
    {
        var s = "";
        foreach (var dial in dials) s += dial != null ? dial.Value.ToString() : "-";
        return s;
    }

    void Commit()
    {
        if (outputSocket == null || compound == null) return;

        if (!outputSocket.IsEmpty || HasBatch)
        {
            terminal?.Show("RS-MIXER", "", "배출구 사용 중");
            return;
        }

        if (analyzer == null || !analyzer.HasResult)
        {
            // No spec to mix against. This is the one refusal, because without a reading there is
            // nothing for the digits to be right or wrong about.
            terminal?.PrintSequence("RS-MIXER", "", "오류", "판독값 없음", "검체 분석 먼저");
            return;
        }

        var entered = new int[dials.Length];
        for (var i = 0; i < dials.Length; i++) entered[i] = dials[i] != null ? dials[i].Value : -1;
        LastBatchCorrect = chart != null && chart.Matches(analyzer.Spec, analyzer.Index, entered);

        compound.gameObject.SetActive(true);
        outputSocket.MountDirect(compound);
        HasBatch = true;

        // The same four lines either way. The machine does not know it was given bad numbers.
        terminal?.PrintSequence("RS-MIXER", "", $"배합 완료  {Entered()}", "", "배출구에서 수거");
    }

    // Called by the run console once a batch has been consumed, so another can be mixed.
    public void ClearBatch()
    {
        HasBatch = false;
        if (compound != null) compound.gameObject.SetActive(false);
    }
}

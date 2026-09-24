using System.Collections;
using UnityEngine;

// Machine 1 of the chain: read the sample. Drop a vial in, pull the lever, wait, and the CRT
// names a spec row and an index column. It never says what to do with them.
public class SampleAnalyzer : MonoBehaviour
{
    [SerializeField] Socket sampleSocket;
    [SerializeField] ToggleLever runLever;
    [SerializeField] MachineTerminal terminal;
    [SerializeField] ProcedureChart chart;
    [Tooltip("Seconds the analysis takes. Long enough that the player looks around the room.")]
    [SerializeField] float analyzeDuration = 5f;

    bool running;

    public bool HasResult { get; private set; }
    public int Spec { get; private set; }
    public int Index { get; private set; }

    void Awake()
    {
        if (sampleSocket != null) sampleSocket.Changed += Refresh;
        if (runLever != null) runLever.Changed += OnLever;
    }

    void OnDestroy()
    {
        if (sampleSocket != null) sampleSocket.Changed -= Refresh;
        if (runLever != null) runLever.Changed -= OnLever;
    }

    void Start()
    {
        Refresh();
        terminal?.Show("RS-ANALYZER", "", "대기", "검체 투입");
    }

    // The lever is the machine's only way of saying "not yet": visible, aimable, and inert.
    void Refresh()
    {
        if (runLever != null)
            runLever.Enabled = !running && sampleSocket != null && !sampleSocket.IsEmpty;
    }

    void OnLever()
    {
        if (runLever == null || !runLever.IsOn || running) return;
        StartCoroutine(Analyze());
    }

    IEnumerator Analyze()
    {
        running = true;
        HasResult = false;
        Refresh();
        // Clamping the sample in is what makes the wait a commitment: the vial cannot be taken
        // back out while the machine holds it.
        sampleSocket?.SetLocked(true);

        terminal?.PrintSequence("RS-ANALYZER", "", "분석 중...");
        yield return new WaitForSeconds(analyzeDuration);

        if (chart != null)
        {
            Spec = Random.Range(0, Mathf.Max(1, chart.SpecCount));
            Index = Random.Range(0, Mathf.Max(1, chart.IndexCount));
        }
        HasResult = true;

        terminal?.PrintSequence(
            "RS-ANALYZER",
            "",
            "판독 완료",
            $"SPEC   {chart?.SpecLabel(Spec)}",
            $"IDX    {Index}",
            "",
            "대조표 참조");

        sampleSocket?.SetLocked(false);
        runLever?.ForceOff();
        running = false;
        Refresh();
    }
}

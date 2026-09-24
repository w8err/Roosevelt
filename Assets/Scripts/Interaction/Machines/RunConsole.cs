using System.Collections;
using UnityEngine;

// Machine 3 of the chain: load the batch, throw the lever, and watch the needle.
// This is where a wrong digit finally shows, and it shows on the gauge before it shows in words,
// so the player reads the room rather than a verdict. The machine has no failsafe: once the
// lever is down the run finishes on its own terms.
public class RunConsole : MonoBehaviour
{
    [SerializeField] Socket compoundSocket;
    [SerializeField] ToggleLever runLever;
    [SerializeField] NeedleGauge gauge;
    [SerializeField] MachineTerminal terminal;
    [Tooltip("Asked whether the loaded batch was mixed from the right digits.")]
    [SerializeField] CompoundMixer mixer;
    [Tooltip("Seconds the needle takes to reach its verdict.")]
    [SerializeField] float runDuration = 6f;
    [Tooltip("Where the needle settles on a good batch. Below the red band.")]
    [Range(0f, 1f)] [SerializeField] float stableReading = 0.55f;
    [Tooltip("Where a bad batch drives it. Past the red band.")]
    [Range(0f, 1f)] [SerializeField] float faultReading = 1f;

    bool running;

    void Awake()
    {
        if (compoundSocket != null) compoundSocket.Changed += Refresh;
        if (runLever != null) runLever.Changed += OnLever;
    }

    void OnDestroy()
    {
        if (compoundSocket != null) compoundSocket.Changed -= Refresh;
        if (runLever != null) runLever.Changed -= OnLever;
    }

    void Start()
    {
        gauge?.SnapTo(0f);
        Refresh();
        terminal?.Show("RS-REACTOR", "", "대기", "배합물 장전");
    }

    void Refresh()
    {
        if (runLever != null)
            runLever.Enabled = !running && compoundSocket != null && !compoundSocket.IsEmpty;
    }

    void OnLever()
    {
        if (runLever == null || !runLever.IsOn || running) return;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        running = true;
        Refresh();
        compoundSocket?.SetLocked(true);

        var correct = mixer != null && mixer.LastBatchCorrect;
        terminal?.PrintSequence("RS-REACTOR", "", "가동 중...");

        // The needle rises the same way for a good batch and a bad one until it passes the point
        // where the good one levels off. The tell is the last second, not the first.
        var peak = correct ? stableReading : faultReading;
        var elapsed = 0f;
        while (elapsed < runDuration)
        {
            elapsed += Time.deltaTime;
            gauge?.SetValue(Mathf.SmoothStep(0f, peak, Mathf.Clamp01(elapsed / runDuration)));
            yield return null;
        }
        gauge?.SetValue(peak);

        if (correct)
            terminal?.PrintSequence("RS-REACTOR", "", "가동 정상", $"압력   {Mathf.RoundToInt(stableReading * 100f)}%", "", "회차 완료");
        else
            terminal?.PrintSequence("RS-REACTOR", "", "경고", "압력 한계 초과", "배합비 부적합", "", "검체 손실");

        // The batch is spent either way, so a wrong run costs the whole chain again from the vial.
        var spent = compoundSocket != null ? compoundSocket.Mounted : null;
        compoundSocket?.SetLocked(false);
        if (spent != null)
        {
            compoundSocket.Eject();
            spent.gameObject.SetActive(false);
        }
        mixer?.ClearBatch();

        runLever?.ForceOff();
        running = false;
        Refresh();

        // The needle falls back on its own once the run is over, so the room settles.
        yield return new WaitForSeconds(1.5f);
        gauge?.SetValue(0f);
    }
}

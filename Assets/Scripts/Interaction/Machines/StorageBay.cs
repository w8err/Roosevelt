using UnityEngine;

// What a cabinet door is actually for: the slots behind it are only reachable while it stands open.
// One bay per door, so the fridge's two doors each govern their own shelves and the cart's drawers
// each govern their own row.
//
// The slots are ordinary Sockets, which already refuse the key while locked without hiding their
// prompt. So a shut door reads as "there is something in there and you cannot have it yet" rather
// than as nothing at all, and opening it needs no new interaction beyond the door itself.
public class StorageBay : MonoBehaviour
{
    [Tooltip("The doors that govern this bay. Any one of them standing open opens it: the fridge's " +
             "two leaves share one compartment, so shutting the left must not lock what the right exposes.")]
    [SerializeField] LabDoor[] doors;
    [Tooltip("Drawers that govern this bay, counted the same way as doors. A cart's row of slots " +
             "rides inside the drawer, so the slots travel with it and open with it.")]
    [SerializeField] Drawer[] drawers;
    [Tooltip("Every slot behind them. Locked while everything that governs the bay is shut.")]
    [SerializeField] Socket[] slots;
    [Tooltip("Optional interior lamp, lit only while the bay stands open.")]
    [SerializeField] Light interiorLight;

    void Awake()
    {
        foreach (var door in doors)
            if (door != null) door.Changed += Apply;
        foreach (var drawer in drawers)
            if (drawer != null) drawer.Changed += Apply;
    }

    void OnDestroy()
    {
        foreach (var door in doors)
            if (door != null) door.Changed -= Apply;
        foreach (var drawer in drawers)
            if (drawer != null) drawer.Changed -= Apply;
    }

    // Start rather than Awake: the sockets need their own Awake to have run, or SetLocked would be
    // overwritten by the startsLocked they read for themselves.
    void Start() => Apply();

    void Apply()
    {
        // No opening at all means nothing is in the way; otherwise any one of them standing open
        // opens the bay, because the fridge's two leaves share a single compartment.
        var governed = (doors?.Length ?? 0) + (drawers?.Length ?? 0);
        var open = governed == 0;
        foreach (var door in doors)
            if (door != null && door.IsOpen) open = true;
        foreach (var drawer in drawers)
            if (drawer != null && drawer.IsOpen) open = true;
        foreach (var slot in slots)
            if (slot != null) slot.SetLocked(!open);
        if (interiorLight != null) interiorLight.enabled = open;
    }
}

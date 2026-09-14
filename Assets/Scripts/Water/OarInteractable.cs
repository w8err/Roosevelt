using UnityEngine;

// Which oar this target pulls. Named for the oar rather than the resulting turn: pulling one oar swings
// the bow the OTHER way, so naming these Left/Right by outcome invites exactly the sign mistake that has
// already happened here twice. BoatRowing owns the mapping.
public enum OarStroke { Forward, PortOar, StarboardOar }

// One gaze target for rowing. Three of these sit on the boat: the port oar, the starboard oar, and an
// invisible box between the two oarlocks (where a rower's hands meet) that pulls both at once to go
// straight. Aim at one and press Interact for a single stroke; BoatRowing turns that into motion.
//
// The prompts name the oar being pulled, not the direction the boat will go, because the boat turns the
// opposite way and a prompt reading "왼쪽으로" on the oar that steers right would be a lie. Letting the
// player find that out is the point of choosing real rowing over the guessable version.
public class OarInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] OarStroke stroke;
    [SerializeField] BoatRowing boatRowing;

    [Tooltip("The boat this oar belongs to; its rider is what decides whether rowing is possible at all. " +
        "Left unset, it is looked up from the parents at runtime.")]
    [SerializeField] BoatInteractable boat;

    public string GetPrompt() => stroke switch
    {
        OarStroke.Forward => "노 젓기",
        OarStroke.PortOar => "왼쪽 노 젓기",
        OarStroke.StarboardOar => "오른쪽 노 젓기",
        _ => "?"
    };

    public bool CanInteract()
    {
        if (ScreenTransition.IsRunning || Dialogue.IsPlaying) return false;

        // Only someone actually sitting in the boat can row it. This asks BoatInteractable, which is the
        // one thing that knows who boarded. The first version called GetComponentInChildren<
        // FirstPersonController>() on the oar itself, which can never find the rider: the rider is
        // parented to the seat, a SIBLING of this oar, not a descendant of it — so rowing was disabled
        // unconditionally and the whole feature was dead.
        return Boat != null && Boat.HasRider;
    }

    public void Interact(PlayerInteraction player)
    {
        if (Rowing == null)
        {
            Debug.LogWarning($"[OarInteractable] {name}: no BoatRowing on this oar's boat");
            return;
        }

        switch (stroke)
        {
            case OarStroke.Forward: Rowing.RowForward(); break;
            case OarStroke.PortOar: Rowing.PullPortOar(); break;
            case OarStroke.StarboardOar: Rowing.PullStarboardOar(); break;
        }
    }

    BoatRowing Rowing => boatRowing != null ? boatRowing : boatRowing = GetComponentInParent<BoatRowing>();
    BoatInteractable Boat => boat != null ? boat : boat = GetComponentInParent<BoatInteractable>();
}

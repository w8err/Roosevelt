using UnityEngine;

// Keeps the finite ocean grid centred on a target (the boat) so the boat never rows off the edge.
// The water mesh is procedural (OceanGridMesh) in world space with the shader sampling waves in world space,
// so if the mesh moved by a non-quad amount, its vertices would land on different wave phases every frame
// and the surface would visibly crawl/shimmer. Snapping to whole quad sizes keeps every vertex on the same
// world lattice, preserving the wave sampling alignment frame-to-frame: a vertex at world X = 5 always
// samples the same wave phase, no matter how the mesh is repositioned (e.g. 5 -> 7 snaps to 6, 7 -> 9 snaps
// to 10, each landing on grid points where Y = Mathf.Round(Y / quadSize) * quadSize). Without this snap,
// the mesh edges would shimmer as they cross wave phases, and the whole surface would appear to flow relative
// to the world (visually wrong — the boat is moving, not the sea).
public class OceanFollow : MonoBehaviour
{
    [Tooltip("The target to follow (typically the boat).")]
    [SerializeField] Transform target;

    [Tooltip("Size of each quad in the grid. Must match the grid this sits on, or the snap lands between " +
        "lattice points and the waves shimmer at the edges. OceanTestBuilder fills this in from its own " +
        "Size/Segments (currently 150/75 = 2m) so the number is not written down twice; the default here " +
        "only covers a hand-placed one.")]
    [SerializeField] float quadSize = 2f;

    // LateUpdate, not Update: the boat's own position is written in Update (BoatRowing) and its height in
    // BoatBuoyancy's Update, so following in Update would chase last frame's position and the grid would
    // lag the boat by a frame whenever it was rowing.
    void LateUpdate() => SnapToTarget();

    // Public so an editor script can verify the follow without entering play mode, the same reason
    // BoatBuoyancy exposes Evaluate() and BoatRowing exposes Tick().
    public void SnapToTarget()
    {
        if (target == null) return;

        // Snap the ocean mesh to whole quad positions, keeping it centered on the target.
        var targetPos = target.position;
        var snappedX = Mathf.Round(targetPos.x / quadSize) * quadSize;
        var snappedZ = Mathf.Round(targetPos.z / quadSize) * quadSize;

        var meshPos = transform.position;
        meshPos.x = snappedX;
        meshPos.z = snappedZ;
        transform.position = meshPos;
    }
}

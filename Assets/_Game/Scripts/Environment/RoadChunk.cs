using UnityEngine;

/// <summary>
/// Defines the navigational type of a road segment.
/// </summary>
public enum RoadChunkType
{
    Straight,
    TurnLeft,
    TurnRight,
    Crossroads
}

/// <summary>
/// Attached to road segment prefabs.
/// Provides chunk metadata (type, length, intersection center) for procedural generation and turn detection.
/// </summary>
public class RoadChunk : MonoBehaviour
{
    [Header("Chunk Properties")]
    [Tooltip("The navigational type of this road segment.")]
    [SerializeField] private RoadChunkType chunkType = RoadChunkType.Straight;

    [Tooltip("Exact length of this chunk along the Z-axis in world units (default 44.75m).")]
    [SerializeField] private float chunkLength = 44.75f;

    [Header("Intersection Geometry & Tuning")]
    [Tooltip("Local Z coordinate of the intersection center within this chunk (default 19m). Editable for fine-tuning.")]
    [SerializeField] private float intersectionCenterZ = 19f;

    [Tooltip("Local offset for attaching or visualizing the left-turn side branch (-25.8, 0, 19).")]
    [SerializeField] private Vector3 leftBranchOffset = new Vector3(-25.8f, 0f, 19f);

    [Tooltip("Local offset for attaching or visualizing the right-turn side branch (25.8, 0, 19).")]
    [SerializeField] private Vector3 rightBranchOffset = new Vector3(25.8f, 0f, 19f);

    [Tooltip("Detection tolerance window (+/- meters around intersectionCenterZ) for evaluating player turn.")]
    [SerializeField] private float turnTriggerWindow = 4f;

    [Tooltip("Distance from intersection center (Z=19) to where the next chunk attaches along the branch after a turn (default 25.8m). Tune this to eliminate gaps or overlaps.")]
    [SerializeField] private float branchExitDistance = 25.8f;

    public RoadChunkType ChunkType => chunkType;
    public float ChunkLength => chunkLength;
    public bool IsIntersection => chunkType != RoadChunkType.Straight;
    public float IntersectionCenterZ { get => intersectionCenterZ; set => intersectionCenterZ = value; }
    public Vector3 LeftBranchOffset => leftBranchOffset;
    public Vector3 RightBranchOffset => rightBranchOffset;
    public float TurnTriggerWindow => turnTriggerWindow;
    public float BranchExitDistance { get => branchExitDistance; set => branchExitDistance = value; }

    private void Awake()
    {
        // Ensure WorldMover component is present so the chunk moves with the world
        if (GetComponent<WorldMover>() == null)
        {
            gameObject.AddComponent<WorldMover>();
        }

        // Ensure TurnTrigger is present on intersection chunks
        if (IsIntersection && GetComponent<TurnTrigger>() == null)
        {
            gameObject.AddComponent<TurnTrigger>();
        }

        // Prevent accidental obstacle tagging
        if (gameObject.CompareTag("Obstacle"))
        {
            gameObject.tag = "Untagged";
        }
    }

    /// <summary>
    /// Calculates the world-space distance from vehicle position (Z=0 by default) to this intersection's center.
    /// When this returns <= 0, the intersection center has reached or passed the vehicle.
    /// </summary>
    public float GetDistanceToCenter(float originZ = 0f)
    {
        return (transform.position.z + intersectionCenterZ) - originZ;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        // Draw bounding box
        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.6f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        if (IsIntersection)
        {
            // Draw intersection center line across the road
            Vector3 centerPos = transform.position + new Vector3(0f, 0.2f, intersectionCenterZ);
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(centerPos + Vector3.left * 10f, centerPos + Vector3.right * 10f);
            Gizmos.DrawWireSphere(centerPos, 1.5f);

            // Draw branch anchor points
            Gizmos.color = Color.cyan;
            if (chunkType == RoadChunkType.TurnLeft || chunkType == RoadChunkType.Crossroads)
            {
                Vector3 leftAnchor = transform.position + leftBranchOffset;
                Gizmos.DrawWireSphere(leftAnchor, 1.2f);
                Gizmos.DrawLine(centerPos, leftAnchor);
            }

            if (chunkType == RoadChunkType.TurnRight || chunkType == RoadChunkType.Crossroads)
            {
                Vector3 rightAnchor = transform.position + rightBranchOffset;
                Gizmos.DrawWireSphere(rightAnchor, 1.2f);
                Gizmos.DrawLine(centerPos, rightAnchor);
            }
        }
    }
#endif
}

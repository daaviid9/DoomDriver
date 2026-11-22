using UnityEngine;

/// <summary>
/// Defines the navigational type of a road segment.
/// </summary>
public enum RoadChunkType
{
    Straight,
    TurnLeft,
    TurnRight,
    Crossroads,
    YFork
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

    [Header("Y-Fork Branch Anchors")]
    [Tooltip("Local position of the left fork exit branch anchor.")]
    [SerializeField] private Vector3 leftForkExitPosition = new Vector3(-27.086f, 0f, 45.456f);

    [Tooltip("Local rotation (Euler angles) of the left fork exit branch anchor.")]
    [SerializeField] private Vector3 leftForkExitRotation = new Vector3(0f, -45f, 0f);

    [Tooltip("Local position of the right fork exit branch anchor.")]
    [SerializeField] private Vector3 rightForkExitPosition = new Vector3(27.086f, 0f, 45.456f);

    [Tooltip("Local rotation (Euler angles) of the right fork exit branch anchor.")]
    [SerializeField] private Vector3 rightForkExitRotation = new Vector3(0f, 45f, 0f);

    public RoadChunkType ChunkType => chunkType;
    public float ChunkLength => chunkLength;
    public bool IsIntersection => chunkType != RoadChunkType.Straight;
    public bool IsFork => chunkType == RoadChunkType.YFork;
    public float TurnAngle => (chunkType == RoadChunkType.YFork) ? 45.0f : 90.0f;
    public float IntersectionCenterZ { get => intersectionCenterZ; set => intersectionCenterZ = value; }
    public Vector3 LeftBranchOffset => leftBranchOffset;
    public Vector3 RightBranchOffset => rightBranchOffset;
    public float TurnTriggerWindow => turnTriggerWindow;
    public float BranchExitDistance { get => branchExitDistance; set => branchExitDistance = value; }
    public Vector3 LeftForkExitPosition { get => leftForkExitPosition; set => leftForkExitPosition = value; }
    public Vector3 LeftForkExitRotation { get => leftForkExitRotation; set => leftForkExitRotation = value; }
    public Vector3 RightForkExitPosition { get => rightForkExitPosition; set => rightForkExitPosition = value; }
    public Vector3 RightForkExitRotation { get => rightForkExitRotation; set => rightForkExitRotation = value; }

    // Attached preview road segments for side branches (visible before and during turn maneuver)
    private GameObject leftBranchChunk;
    private GameObject rightBranchChunk;

    public GameObject LeftBranchChunk => leftBranchChunk;
    public GameObject RightBranchChunk => rightBranchChunk;

    public void SetBranchChunk(DashboardNavigationSource.Maneuver direction, GameObject chunk)
    {
        if (direction == DashboardNavigationSource.Maneuver.Left)
        {
            leftBranchChunk = chunk;
        }
        else if (direction == DashboardNavigationSource.Maneuver.Right)
        {
            rightBranchChunk = chunk;
        }
    }

    public GameObject DetachBranchChunk(DashboardNavigationSource.Maneuver direction)
    {
        GameObject chunk = null;
        if (direction == DashboardNavigationSource.Maneuver.Left)
        {
            chunk = leftBranchChunk;
            leftBranchChunk = null;
        }
        else if (direction == DashboardNavigationSource.Maneuver.Right)
        {
            chunk = rightBranchChunk;
            rightBranchChunk = null;
        }
        return chunk;
    }

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

    /// <summary>
    /// Returns the local exit position for attaching preview chunks or continuing road generation.
    /// Supports both standard 90-degree side branches and extended Y-fork dual branch exits.
    /// </summary>
    public Vector3 GetBranchExitLocalPosition(DashboardNavigationSource.Maneuver direction)
    {
        if (chunkType == RoadChunkType.YFork)
        {
            return (direction == DashboardNavigationSource.Maneuver.Left)
                ? leftForkExitPosition
                : rightForkExitPosition;
        }

        float exitDistance = (branchExitDistance > 0.1f) ? branchExitDistance : 25.8f;

        return (direction == DashboardNavigationSource.Maneuver.Left)
            ? new Vector3(-exitDistance, 0f, intersectionCenterZ)
            : new Vector3(exitDistance, 0f, intersectionCenterZ);
    }

    /// <summary>
    /// Returns the local exit rotation for attaching preview chunks or continuing road generation.
    /// Supports both standard 90-degree side branches and extended Y-fork dual branch exits.
    /// </summary>
    public Quaternion GetBranchExitLocalRotation(DashboardNavigationSource.Maneuver direction)
    {
        if (chunkType == RoadChunkType.YFork)
        {
            Vector3 euler = (direction == DashboardNavigationSource.Maneuver.Left)
                ? leftForkExitRotation
                : rightForkExitRotation;
            return Quaternion.Euler(euler);
        }

        return (direction == DashboardNavigationSource.Maneuver.Left)
            ? Quaternion.Euler(0f, -90f, 0f)
            : Quaternion.Euler(0f, 90f, 0f);
    }

    /// <summary>
    /// Calculates the world-space position where the next road chunk attaches along the branch.
    /// Calling TransformPoint accurately maps this to world space under any position or rotation.
    /// </summary>
    public Vector3 GetBranchExitWorldPosition(DashboardNavigationSource.Maneuver direction)
    {
        return transform.TransformPoint(GetBranchExitLocalPosition(direction));
    }

    /// <summary>
    /// Calculates the world-space rotation for attaching road chunks along the branch.
    /// </summary>
    public Quaternion GetBranchExitWorldRotation(DashboardNavigationSource.Maneuver direction)
    {
        return transform.rotation * GetBranchExitLocalRotation(direction);
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
            Vector3 centerPos = transform.TransformPoint(new Vector3(0f, 0.2f, intersectionCenterZ));
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(centerPos + Vector3.left * 10f, centerPos + Vector3.right * 10f);
            Gizmos.DrawWireSphere(centerPos, 1.5f);

            // Draw branch anchor points
            Gizmos.color = Color.cyan;
            if (chunkType == RoadChunkType.TurnLeft || chunkType == RoadChunkType.Crossroads || chunkType == RoadChunkType.YFork)
            {
                Vector3 leftAnchor = GetBranchExitWorldPosition(DashboardNavigationSource.Maneuver.Left);
                Gizmos.DrawWireSphere(leftAnchor, 1.2f);
                Gizmos.DrawLine(centerPos, leftAnchor);
            }

            Gizmos.color = Color.green;
            if (chunkType == RoadChunkType.TurnRight || chunkType == RoadChunkType.Crossroads || chunkType == RoadChunkType.YFork)
            {
                Vector3 rightAnchor = GetBranchExitWorldPosition(DashboardNavigationSource.Maneuver.Right);
                Gizmos.DrawWireSphere(rightAnchor, 1.2f);
                Gizmos.DrawLine(centerPos, rightAnchor);
            }
        }
    }
#endif
}

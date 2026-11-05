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
/// Provides chunk metadata (type, length) for seamless procedural generation in EnvironmentSpawner.
/// </summary>
public class RoadChunk : MonoBehaviour
{
    [Header("Chunk Properties")]
    [Tooltip("The navigational type of this road segment.")]
    [SerializeField] private RoadChunkType chunkType = RoadChunkType.Straight;

    [Tooltip("Exact length of this chunk along the Z-axis in world units. Default is 44.75m matching modular city roads.")]
    [SerializeField] private float chunkLength = 44.75f;

    public RoadChunkType ChunkType => chunkType;
    public float ChunkLength => chunkLength;
    public bool IsIntersection => chunkType != RoadChunkType.Straight;

    private void Awake()
    {
        // Ensure WorldMover component is present so the chunk moves with the world
        if (GetComponent<WorldMover>() == null)
        {
            gameObject.AddComponent<WorldMover>();
        }

        // Prevent accidental obstacle tagging
        if (gameObject.CompareTag("Obstacle"))
        {
            gameObject.tag = "Untagged";
        }
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

        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.75f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
#endif
}

using UnityEngine;

/// <summary>
/// Defines the navigational type of a road segment.
/// </summary>
public enum RoadChunkType
{
    Straight,
    TurnLeft,
    TurnRight,
    TJunction,
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
}


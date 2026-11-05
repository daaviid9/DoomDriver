using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural spawner for modular road chunks and intersections.
/// Controls the forward sequence of road segments based on active DeliveryOrder or debug sequences.
/// Adheres to AGENTS.md conventions: Single Responsibility, Decoupled Events, and Zero Update allocations.
/// </summary>
public class EnvironmentSpawner : MonoBehaviour
{
    public static EnvironmentSpawner Instance { get; private set; }

    [Header("Road Segment Prefabs")]
    [Tooltip("Prefabs for straight road segments with scenery variations.")]
    [SerializeField] private GameObject[] straightPrefabs;

    [Tooltip("Prefabs for left-turn intersections.")]
    [SerializeField] private GameObject[] turnLeftPrefabs;

    [Tooltip("Prefabs for right-turn intersections.")]
    [SerializeField] private GameObject[] turnRightPrefabs;

    [Tooltip("Prefabs for T-junction intersections.")]
    [SerializeField] private GameObject[] tJunctionPrefabs;

    [Tooltip("Prefabs for 4-way crossroads.")]
    [SerializeField] private GameObject[] crossroadsPrefabs;

    [Header("Legacy / Fallback Prefabs")]
    [Tooltip("Retained for backwards compatibility with existing scene setups.")]
    public GameObject[] chunkPrefabs;

    [Header("Generation Settings")]
    [Tooltip("Default length along Z-axis for road chunks (44.75m standard for modular city roads).")]
    [SerializeField] private float defaultChunkLength = 44.75f;

    [Tooltip("Number of chunks visible simultaneously on screen.")]
    [SerializeField] private int numberOfChunksOnScreen = 5;

    [Tooltip("Starting Z position for the first spawned chunk (e.g. -50m behind player).")]
    [SerializeField] private float startZPosition = -50f;

    [Tooltip("Z position behind the player where chunks are safely recycled/destroyed.")]
    [SerializeField] private float destroyZPosition = -50f;

    [Header("Procedural Route & Testing")]
    [Tooltip("If true, generates a repeating sequence of straights and turns when no delivery order is active.")]
    [SerializeField] private bool useDebugSequence = true;

    [Tooltip("Number of straight chunks placed between intersections in debug mode.")]
    [SerializeField] private int debugStraightsBetweenTurns = 3;

    [Tooltip("Sequence of turns generated sequentially in debug mode.")]
    [SerializeField] private RoadChunkType[] debugTurnSequence = new RoadChunkType[]
    {
        RoadChunkType.TurnLeft,
        RoadChunkType.TurnRight,
        RoadChunkType.TJunction
    };

    // Active spawned chunks tracked in world
    private readonly List<GameObject> activeChunks = new List<GameObject>();
    private readonly Queue<RoadChunkType> plannedRouteQueue = new Queue<RoadChunkType>();

    private int debugTurnIndex = 0;
    private int currentStraightCounter = 0;

    // Events for decoupled observation (e.g. TurnTrigger, GPS UI)
    public event System.Action<RoadChunk> OnChunkSpawned;
    public event System.Action<RoadChunk> OnChunkDespawned;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Migrate legacy chunkPrefabs if straightPrefabs array is empty
        if ((straightPrefabs == null || straightPrefabs.Length == 0) && chunkPrefabs != null && chunkPrefabs.Length > 0)
        {
            straightPrefabs = chunkPrefabs;
        }
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnOrderSelectionStarted += HandleOrderSelectionReset;
            GameManager.Instance.OnDeliveryStarted += HandleDeliveryStarted;
        }

        InitializeRoad();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnOrderSelectionStarted -= HandleOrderSelectionReset;
            GameManager.Instance.OnDeliveryStarted -= HandleDeliveryStarted;
        }
    }

    private void Update()
    {
        // Don't spawn new chunks if delivery is failed
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DeliveryFailed) return;

        // Check if the oldest chunk has moved past destroy threshold
        if (activeChunks.Count > 0 && activeChunks[0] != null && activeChunks[0].transform.position.z < destroyZPosition)
        {
            DeleteOldestChunk();
            SpawnNextChunkInSequence();
        }
    }

    /// <summary>
    /// Clears and instantiates initial road segments at scene start or reset.
    /// </summary>
    public void InitializeRoad()
    {
        ClearActiveChunks();

        float spawnZ = startZPosition;
        for (int i = 0; i < numberOfChunksOnScreen; i++)
        {
            // Initial chunks in front of the vehicle are always straight for a smooth start
            RoadChunkType type = (i < 2) ? RoadChunkType.Straight : GetNextChunkTypeFromQueue();
            GameObject chunk = SpawnChunk(type, spawnZ);

            float length = defaultChunkLength;
            if (chunk != null && chunk.TryGetComponent<RoadChunk>(out var roadChunk))
            {
                length = roadChunk.ChunkLength;
            }

            spawnZ += length;
        }
    }

    /// <summary>
    /// Loads a pre-planned route sequence from a DeliveryOrder.
    /// </summary>
    public void SetRoute(IEnumerable<RoadChunkType> routeSequence)
    {
        plannedRouteQueue.Clear();
        if (routeSequence != null)
        {
            foreach (var chunkType in routeSequence)
            {
                plannedRouteQueue.Enqueue(chunkType);
            }
        }
    }

    /// <summary>
    /// Called when the player misses a turn ('Recalculating').
    /// Dynamically injects extra straight road chunks into the front of the route to extend the trip.
    /// </summary>
    /// <param name="extraStraightChunks">Number of extra straight segments to add (e.g. 3 chunks = ~134m).</param>
    public void AddRecalculationChunks(int extraStraightChunks = 3)
    {
        List<RoadChunkType> existingList = new List<RoadChunkType>(plannedRouteQueue);
        plannedRouteQueue.Clear();

        // Prepend extra straight chunks
        for (int i = 0; i < extraStraightChunks; i++)
        {
            plannedRouteQueue.Enqueue(RoadChunkType.Straight);
        }

        // Re-enqueue remaining route
        for (int i = 0; i < existingList.Count; i++)
        {
            plannedRouteQueue.Enqueue(existingList[i]);
        }

        Debug.Log($"[EnvironmentSpawner] Recalculating: Injected {extraStraightChunks} extra straight chunks.");
    }

    private void SpawnNextChunkInSequence()
    {
        if (activeChunks.Count == 0)
        {
            SpawnChunk(GetNextChunkTypeFromQueue(), startZPosition);
            return;
        }

        GameObject lastChunk = activeChunks[activeChunks.Count - 1];
        float lastChunkZ = lastChunk.transform.position.z;
        float lastChunkLength = defaultChunkLength;

        if (lastChunk.TryGetComponent<RoadChunk>(out var roadChunk))
        {
            lastChunkLength = roadChunk.ChunkLength;
        }

        float spawnZ = lastChunkZ + lastChunkLength;
        RoadChunkType nextType = GetNextChunkTypeFromQueue();
        SpawnChunk(nextType, spawnZ);
    }

    private RoadChunkType GetNextChunkTypeFromQueue()
    {
        if (plannedRouteQueue.Count > 0)
        {
            return plannedRouteQueue.Dequeue();
        }

        // Debug fallback generation
        if (useDebugSequence)
        {
            if (currentStraightCounter < debugStraightsBetweenTurns)
            {
                currentStraightCounter++;
                return RoadChunkType.Straight;
            }

            // Time for an intersection
            currentStraightCounter = 0;
            if (debugTurnSequence != null && debugTurnSequence.Length > 0)
            {
                RoadChunkType turn = debugTurnSequence[debugTurnIndex % debugTurnSequence.Length];
                debugTurnIndex++;
                return turn;
            }
        }

        return RoadChunkType.Straight;
    }

    private GameObject SpawnChunk(RoadChunkType type, float spawnZ)
    {
        GameObject prefabToSpawn = PickPrefabForType(type);

        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"[EnvironmentSpawner] No prefab configured for type {type}. Falling back to straight chunk.");
            prefabToSpawn = PickRandomPrefab(straightPrefabs);
        }

        if (prefabToSpawn == null)
        {
            Debug.LogError("[EnvironmentSpawner] No road prefabs assigned! Cannot spawn road segment.");
            return null;
        }

        Vector3 spawnPosition = new Vector3(0f, 0f, spawnZ);
        GameObject newChunk = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);

        // Ensure RoadChunk component exists
        if (!newChunk.TryGetComponent<RoadChunk>(out var roadChunk))
        {
            roadChunk = newChunk.AddComponent<RoadChunk>();
        }

        // Ensure WorldMover component exists
        if (newChunk.GetComponent<WorldMover>() == null)
        {
            newChunk.AddComponent<WorldMover>();
        }

        newChunk.tag = "Untagged";
        activeChunks.Add(newChunk);

        OnChunkSpawned?.Invoke(roadChunk);
        return newChunk;
    }

    private GameObject PickPrefabForType(RoadChunkType type)
    {
        switch (type)
        {
            case RoadChunkType.Straight:
                return PickRandomPrefab(straightPrefabs);

            case RoadChunkType.TurnLeft:
                return PickRandomPrefab(turnLeftPrefabs) ?? PickRandomPrefab(crossroadsPrefabs);

            case RoadChunkType.TurnRight:
                return PickRandomPrefab(turnRightPrefabs) ?? PickRandomPrefab(crossroadsPrefabs);

            case RoadChunkType.TJunction:
                return PickRandomPrefab(tJunctionPrefabs) ?? PickRandomPrefab(crossroadsPrefabs);

            case RoadChunkType.Crossroads:
                return PickRandomPrefab(crossroadsPrefabs);

            default:
                return PickRandomPrefab(straightPrefabs);
        }
    }

    private GameObject PickRandomPrefab(GameObject[] array)
    {
        if (array == null || array.Length == 0) return null;
        int index = Random.Range(0, array.Length);
        return array[index];
    }

    private void DeleteOldestChunk()
    {
        if (activeChunks.Count == 0) return;

        GameObject oldestChunk = activeChunks[0];
        activeChunks.RemoveAt(0);

        if (oldestChunk != null)
        {
            if (oldestChunk.TryGetComponent<RoadChunk>(out var roadChunk))
            {
                OnChunkDespawned?.Invoke(roadChunk);
            }
            Destroy(oldestChunk);
        }
    }

    private void ClearActiveChunks()
    {
        for (int i = 0; i < activeChunks.Count; i++)
        {
            if (activeChunks[i] != null)
            {
                Destroy(activeChunks[i]);
            }
        }
        activeChunks.Clear();
    }

    private void HandleOrderSelectionReset()
    {
        plannedRouteQueue.Clear();
        currentStraightCounter = 0;
        debugTurnIndex = 0;
        InitializeRoad();
    }

    private void HandleDeliveryStarted()
    {
        // When delivery starts, ensure road is seamless
    }
}

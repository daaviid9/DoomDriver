using System.Collections.Generic;
using UnityEngine;
using DoomDriver.Environment;

/// <summary>
/// Procedural spawner for modular road chunks and intersections.
/// Delegates route sequence planning to RoutePlanQueue and leaves GPS publishing to RouteController.
/// </summary>
public class EnvironmentSpawner : MonoBehaviour
{
    public static EnvironmentSpawner Instance { get; private set; }

    [Header("Road Segment Prefabs")]
    [Tooltip("Prefabs for straight road segments with scenery variations.")]
    [SerializeField] private GameObject[] straightPrefabs;

    [Tooltip("Prefabs for 45-degree Y-fork road segments.")]
    [SerializeField] private GameObject[] yForkPrefabs;

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

    [Header("Turn Attachment & Spacing")]
    [Tooltip("Default distance from the intersection center (Z=19) to where the next chunk attaches after a 90-degree turn (default 25.8m). Tune this to eliminate road gaps.")]
    [SerializeField] private float defaultBranchExitDistance = 25.8f;

    [Tooltip("Global fine-tuning offset in meters for adjusting distance between intersection and the newly spawned road after a turn. Positive moves road further away, negative brings it closer.")]
    [SerializeField] private float branchExitDistanceOffset = 0f;

    [Header("Procedural Route & Testing")]
    [Tooltip("If true, generates a repeating sequence of straights and turns when no delivery order is active.")]
    [SerializeField] private bool useDebugSequence = true;

    [Tooltip("Number of straight chunks placed between intersections in debug mode.")]
    [SerializeField] private int debugStraightsBetweenTurns = 3;

    [Tooltip("Sequence of turns generated sequentially in debug mode.")]
    [SerializeField] private RoadChunkType[] debugTurnSequence = new RoadChunkType[]
    {
        RoadChunkType.YFork
    };

    private readonly List<GameObject> activeChunks = new List<GameObject>();
    private readonly RoutePlanQueue routePlan = new RoutePlanQueue();

    public event System.Action<RoadChunk> OnChunkSpawned;
    public event System.Action<RoadChunk> OnChunkDespawned;

    private bool isWorldTurnInProgress = false;
    public bool IsWorldTurnInProgress { get => isWorldTurnInProgress; set => isWorldTurnInProgress = value; }
    public float BranchExitDistanceOffset { get => branchExitDistanceOffset; set => branchExitDistanceOffset = value; }

    public IReadOnlyList<GameObject> ActiveChunks => activeChunks;

    public bool TryGetQueuedTurnDistance(float carZ, out float distanceMeters)
    {
        float nextSpawnZ = startZPosition;
        for (int i = activeChunks.Count - 1; i >= 0; i--)
        {
            if (activeChunks[i] == null) continue;
            float len = activeChunks[i].TryGetComponent<RoadChunk>(out var rc) ? rc.ChunkLength : defaultChunkLength;
            nextSpawnZ = activeChunks[i].transform.position.z + len;
            break;
        }

        return routePlan.TryGetQueuedTurnDistance(nextSpawnZ, carZ, defaultChunkLength, out distanceMeters);
    }

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

        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DeliveryFailed) return;

        if (activeChunks.Count > 0 && activeChunks[0] != null && activeChunks[0].transform.position.z < destroyZPosition)
        {
            DeleteOldestChunk();
            if (!isWorldTurnInProgress)
            {
                SpawnNextChunkInSequence();
            }
        }
    }

    public void InitializeRoad()
    {
        ClearActiveChunks();

        bool isParkedInLobby = GameManager.Instance != null &&
                               (GameManager.Instance.CurrentState == GameState.DayBriefing ||
                                GameManager.Instance.CurrentState == GameState.OrderSelection);

        float spawnZ = startZPosition;
        for (int i = 0; i < numberOfChunksOnScreen; i++)
        {
            RoadChunkType type = (i < 2 || isParkedInLobby) ? RoadChunkType.Straight : GetNextChunkTypeFromQueue();
            GameObject chunk = SpawnChunk(type, spawnZ);

            float length = defaultChunkLength;
            if (chunk != null && chunk.TryGetComponent<RoadChunk>(out var roadChunk))
            {
                length = roadChunk.ChunkLength;
                if (roadChunk.IsFork) break;
            }

            spawnZ += length;
        }
    }

    public void SetRoute(IEnumerable<RoadChunkType> routeSequence)
    {
        routePlan.SetRoute(routeSequence);
    }

    public void ConfigureRouteFromTurns(float totalDistance, int[] turnSequence)
    {
        routePlan.ConfigureFromTurns(totalDistance, turnSequence, defaultChunkLength);
        Debug.Log($"[EnvironmentSpawner] Configured route for distance {totalDistance:F0}m with {turnSequence?.Length ?? 0} turns (Total chunks in queue: {routePlan.Count}).");
    }

    public void AddRecalculationChunks(int extraStraightChunks = 3)
    {
        routePlan.PrependRecalculationChunks(extraStraightChunks);
        Debug.Log($"[EnvironmentSpawner] Recalculating: Injected {extraStraightChunks} extra straight chunks.");
    }

    public void PruneChunksAheadOf(GameObject intersectionChunk)
    {
        if (intersectionChunk == null) return;
        int index = activeChunks.IndexOf(intersectionChunk);
        if (index < 0) return;

        for (int i = activeChunks.Count - 1; i > index; i--)
        {
            GameObject chunkToPrune = activeChunks[i];
            activeChunks.RemoveAt(i);
            if (chunkToPrune != null)
            {
                if (chunkToPrune.TryGetComponent<RoadChunk>(out var rc))
                {
                    OnChunkDespawned?.Invoke(rc);
                }
                Destroy(chunkToPrune);
            }
        }
    }

    public void ResumeGenerationAfterTurn(RoadChunk intersectionChunk, DashboardNavigationSource.Maneuver direction = DashboardNavigationSource.Maneuver.Left)
    {
        isWorldTurnInProgress = false;
        if (intersectionChunk == null) return;

        float spawnZ;
        GameObject branchChunk = intersectionChunk.DetachBranchChunk(direction);

        if (branchChunk != null)
        {
            branchChunk.transform.SetParent(null, true);

            if (!branchChunk.TryGetComponent<WorldMover>(out var mover))
            {
                mover = branchChunk.AddComponent<WorldMover>();
            }
            mover.enabled = true;

            if (!branchChunk.TryGetComponent<RoadChunk>(out var rc))
            {
                rc = branchChunk.AddComponent<RoadChunk>();
            }

            activeChunks.Add(branchChunk);
            OnChunkSpawned?.Invoke(rc);

            float chunkLength = (rc.ChunkLength > 0.1f) ? rc.ChunkLength : defaultChunkLength;
            spawnZ = branchChunk.transform.position.z + chunkLength;

            Debug.Log($"[EnvironmentSpawner] Promoted {direction} preview branch chunk to active road at Z={branchChunk.transform.position.z:F2}. Next spawn at Z={spawnZ:F2}.");
        }
        else
        {
            Vector3 worldBranchExit = intersectionChunk.GetBranchExitWorldPosition(direction);
            spawnZ = worldBranchExit.z + branchExitDistanceOffset;
        }

        while (activeChunks.Count < numberOfChunksOnScreen)
        {
            RoadChunkType nextType = GetNextChunkTypeFromQueue();
            GameObject newChunk = SpawnChunk(nextType, spawnZ);

            float length = defaultChunkLength;
            if (newChunk != null && newChunk.TryGetComponent<RoadChunk>(out var roadChunk))
            {
                length = roadChunk.ChunkLength;
                if (roadChunk.IsFork) break;
            }

            spawnZ += length;
        }
    }

    public bool HasActiveForkAhead()
    {
        for (int i = 0; i < activeChunks.Count; i++)
        {
            if (activeChunks[i] != null && activeChunks[i].TryGetComponent<RoadChunk>(out var rc))
            {
                if (rc.IsFork && rc.transform.position.z + rc.IntersectionCenterZ > -5f)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void SpawnNextChunkInSequence()
    {
        if (activeChunks.Count == 0)
        {
            SpawnChunk(GetNextChunkTypeFromQueue(), startZPosition);
            return;
        }

        if (HasActiveForkAhead()) return;

        GameObject lastChunk = activeChunks[activeChunks.Count - 1];
        RoadChunk lastRoadChunk = null;
        if (lastChunk != null && lastChunk.TryGetComponent(out lastRoadChunk) && lastRoadChunk.IsFork)
        {
            return;
        }

        float lastChunkZ = (lastChunk != null) ? lastChunk.transform.position.z : 0f;
        float lastChunkLength = (lastRoadChunk != null) ? lastRoadChunk.ChunkLength : defaultChunkLength;

        float spawnZ = lastChunkZ + lastChunkLength;
        SpawnChunk(GetNextChunkTypeFromQueue(), spawnZ);
    }

    private RoadChunkType GetNextChunkTypeFromQueue()
    {
        return routePlan.DequeueNext(useDebugSequence, debugStraightsBetweenTurns, debugTurnSequence);
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

        GameObject newChunk = Instantiate(prefabToSpawn, new Vector3(0f, 0f, spawnZ), Quaternion.identity);

        if (!newChunk.TryGetComponent<RoadChunk>(out var roadChunk))
        {
            roadChunk = newChunk.AddComponent<RoadChunk>();
        }

        ConfigureChunkManeuver(roadChunk, type);

        if (roadChunk.IsIntersection && newChunk.GetComponent<TurnTrigger>() == null)
        {
            newChunk.AddComponent<TurnTrigger>();
        }

        if (newChunk.GetComponent<WorldMover>() == null)
        {
            newChunk.AddComponent<WorldMover>();
        }

        newChunk.tag = "Untagged";
        activeChunks.Add(newChunk);

        if (roadChunk.IsIntersection)
        {
            AttachSideBranchPreviews(roadChunk);
        }

        OnChunkSpawned?.Invoke(roadChunk);
        return newChunk;
    }

    private void ConfigureChunkManeuver(RoadChunk roadChunk, RoadChunkType type)
    {
        if (!roadChunk.IsFork && !roadChunk.IsIntersection) return;

        if (type == RoadChunkType.TurnLeft)
        {
            roadChunk.TargetManeuver = DashboardNavigationSource.Maneuver.Left;
        }
        else if (type == RoadChunkType.TurnRight)
        {
            roadChunk.TargetManeuver = DashboardNavigationSource.Maneuver.Right;
        }
        else if (type == RoadChunkType.YFork)
        {
            roadChunk.TargetManeuver = (routePlan.DebugTurnIndex % 2 == 0)
                ? DashboardNavigationSource.Maneuver.ForkLeft
                : DashboardNavigationSource.Maneuver.ForkRight;
        }
    }

    private void AttachSideBranchPreviews(RoadChunk roadChunk)
    {
        if (roadChunk == null || straightPrefabs == null || straightPrefabs.Length == 0) return;

        if (roadChunk.ChunkType == RoadChunkType.TurnLeft || roadChunk.ChunkType == RoadChunkType.Crossroads || roadChunk.ChunkType == RoadChunkType.YFork)
        {
            AttachBranchPreview(roadChunk, DashboardNavigationSource.Maneuver.Left);
        }

        if (roadChunk.ChunkType == RoadChunkType.TurnRight || roadChunk.ChunkType == RoadChunkType.Crossroads || roadChunk.ChunkType == RoadChunkType.YFork)
        {
            AttachBranchPreview(roadChunk, DashboardNavigationSource.Maneuver.Right);
        }
    }

    private void AttachBranchPreview(RoadChunk intersectionChunk, DashboardNavigationSource.Maneuver direction)
    {
        GameObject prefab = PickRandomPrefab(straightPrefabs);
        if (prefab == null) return;

        Vector3 localPos = intersectionChunk.GetBranchExitLocalPosition(direction);
        if (intersectionChunk.ChunkType != RoadChunkType.YFork)
        {
            localPos.x += (direction == DashboardNavigationSource.Maneuver.Left ? -branchExitDistanceOffset : branchExitDistanceOffset);
        }

        Quaternion localRot = intersectionChunk.GetBranchExitLocalRotation(direction);

        GameObject previewChunk = Instantiate(prefab, intersectionChunk.transform);
        previewChunk.name = $"{prefab.name}_Branch_{direction}";
        previewChunk.transform.localPosition = localPos;
        previewChunk.transform.localRotation = localRot;
        previewChunk.transform.localScale = Vector3.one;

        if (previewChunk.TryGetComponent<WorldMover>(out var mover))
        {
            mover.enabled = false;
        }

        previewChunk.tag = "Untagged";
        intersectionChunk.SetBranchChunk(direction, previewChunk);
    }

    private GameObject PickPrefabForType(RoadChunkType type)
    {
        return type switch
        {
            RoadChunkType.Straight => PickRandomPrefab(straightPrefabs),
            RoadChunkType.TurnLeft or RoadChunkType.TurnRight or RoadChunkType.Crossroads or RoadChunkType.YFork => PickRandomPrefab(yForkPrefabs),
            _ => PickRandomPrefab(straightPrefabs)
        };
    }

    private GameObject PickRandomPrefab(GameObject[] array)
    {
        if (array == null || array.Length == 0) return null;
        return array[Random.Range(0, array.Length)];
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
        isWorldTurnInProgress = false;
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
        routePlan.ResetAll();
        InitializeRoad();
    }

    private void HandleDeliveryStarted()
    {
        if (DoomDriver.Economy.OrderManager.Instance != null && DoomDriver.Economy.OrderManager.Instance.SelectedOrder != null)
        {
            var order = DoomDriver.Economy.OrderManager.Instance.SelectedOrder;
            ConfigureRouteFromTurns(order.TotalDistance, order.TurnSequence);
            InitializeRoad();
            Debug.Log($"[EnvironmentSpawner] Loaded route from selected order '{order.RestaurantName}': {order.TotalDistance:F0}m, {order.TurnCount} turns.");
        }
    }
}

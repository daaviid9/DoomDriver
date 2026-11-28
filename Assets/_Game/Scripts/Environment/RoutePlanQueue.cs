using System.Collections.Generic;
using UnityEngine;

namespace DoomDriver.Environment
{
    /// <summary>
    /// Pure C# route sequence planner for EnvironmentSpawner.
    /// Manages the upcoming road chunk queue, turn-based route generation,
    /// recalculation chunk injection, and debug fallback sequences.
    /// </summary>
    public class RoutePlanQueue
    {
        private readonly Queue<RoadChunkType> plannedRouteQueue = new Queue<RoadChunkType>();
        private int debugTurnIndex = 0;
        private int currentStraightCounter = 0;

        public int Count => plannedRouteQueue.Count;
        public int DebugTurnIndex => debugTurnIndex;

        public void Clear()
        {
            plannedRouteQueue.Clear();
        }

        public void ResetAll()
        {
            plannedRouteQueue.Clear();
            currentStraightCounter = 0;
            debugTurnIndex = 0;
        }

        public void SetRoute(IEnumerable<RoadChunkType> routeSequence)
        {
            plannedRouteQueue.Clear();
            if (routeSequence == null) return;

            foreach (var chunkType in routeSequence)
            {
                plannedRouteQueue.Enqueue(chunkType);
            }
        }

        public void ConfigureFromTurns(float totalDistance, int[] turnSequence, float defaultChunkLength)
        {
            plannedRouteQueue.Clear();

            if (turnSequence == null || turnSequence.Length == 0)
            {
                int chunkCount = Mathf.Max(3, Mathf.RoundToInt(totalDistance / defaultChunkLength));
                for (int i = 0; i < chunkCount; i++)
                {
                    plannedRouteQueue.Enqueue(RoadChunkType.Straight);
                }
                return;
            }

            int totalChunks = Mathf.Max(turnSequence.Length * 3, Mathf.RoundToInt(totalDistance / defaultChunkLength));
            int chunksPerTurn = Mathf.Max(2, totalChunks / (turnSequence.Length + 1));

            for (int t = 0; t < turnSequence.Length; t++)
            {
                for (int s = 0; s < chunksPerTurn; s++)
                {
                    plannedRouteQueue.Enqueue(RoadChunkType.Straight);
                }

                RoadChunkType turnType = turnSequence[t] switch
                {
                    1 => RoadChunkType.TurnLeft,
                    2 => RoadChunkType.TurnRight,
                    _ => RoadChunkType.Straight
                };
                plannedRouteQueue.Enqueue(turnType);
            }

            for (int s = 0; s < chunksPerTurn; s++)
            {
                plannedRouteQueue.Enqueue(RoadChunkType.Straight);
            }
        }

        public void PrependRecalculationChunks(int extraStraightChunks)
        {
            var existingList = new List<RoadChunkType>(plannedRouteQueue);
            plannedRouteQueue.Clear();

            for (int i = 0; i < extraStraightChunks; i++)
            {
                plannedRouteQueue.Enqueue(RoadChunkType.Straight);
            }

            for (int i = 0; i < existingList.Count; i++)
            {
                plannedRouteQueue.Enqueue(existingList[i]);
            }
        }

        public RoadChunkType DequeueNext(bool useDebugSequence, int debugStraightsBetweenTurns, RoadChunkType[] debugTurnSequence)
        {
            if (plannedRouteQueue.Count > 0)
            {
                return plannedRouteQueue.Dequeue();
            }

            if (useDebugSequence)
            {
                if (currentStraightCounter < debugStraightsBetweenTurns)
                {
                    currentStraightCounter++;
                    return RoadChunkType.Straight;
                }

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

        public bool TryGetQueuedTurnDistance(float nextSpawnZ, float carZ, float defaultChunkLength, out float distanceMeters)
        {
            distanceMeters = 0f;
            if (plannedRouteQueue.Count == 0) return false;

            int straightCount = 0;
            foreach (var chunkType in plannedRouteQueue)
            {
                if (chunkType != RoadChunkType.Straight)
                {
                    const float defaultIntersectionCenterZ = 19f;
                    distanceMeters = Mathf.Max(0f, (nextSpawnZ - carZ) + (straightCount * defaultChunkLength) + defaultIntersectionCenterZ);
                    return true;
                }
                straightCount++;
            }

            return false;
        }
    }
}


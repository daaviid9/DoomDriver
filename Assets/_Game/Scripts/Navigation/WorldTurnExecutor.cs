using System.Collections;
using UnityEngine;

namespace DoomDriver.Navigation
{
    /// <summary>
    /// Executes the physical world pivot turn transition around an intersection chunk.
    /// Adheres to SoC: handles only the turn animation, chunk pruning/resumption, and vehicle/camera turn cues.
    /// </summary>
    public class WorldTurnExecutor : MonoBehaviour
    {
        public void ExecuteTurn(
            RoadChunk intersectionChunk,
            DashboardNavigationSource.Maneuver direction,
            float defaultForkDuration = 0.95f,
            float defaultSharpDuration = 1.1f)
        {
            if (intersectionChunk == null) return;
            StartCoroutine(WorldTurnRoutine(intersectionChunk, direction, defaultForkDuration, defaultSharpDuration));
        }

        private IEnumerator WorldTurnRoutine(
            RoadChunk intersectionChunk,
            DashboardNavigationSource.Maneuver direction,
            float defaultForkDuration,
            float defaultSharpDuration)
        {
            bool isFork = intersectionChunk.ChunkType == RoadChunkType.YFork;
            float turnAngle = intersectionChunk.TurnAngle;
            float fallbackDuration = isFork ? defaultForkDuration : defaultSharpDuration;
            float duration = intersectionChunk.TurnDuration > 0f ? intersectionChunk.TurnDuration : fallbackDuration;

            bool isLeft = direction == DashboardNavigationSource.Maneuver.Left ||
                          direction == DashboardNavigationSource.Maneuver.ForkLeft;
            float targetAngle = isLeft ? turnAngle : -turnAngle;

            if (EnvironmentSpawner.Instance != null)
            {
                EnvironmentSpawner.Instance.IsWorldTurnInProgress = true;
                EnvironmentSpawner.Instance.PruneChunksAheadOf(intersectionChunk.gameObject);
            }

            if (VehicleMovement.Instance != null)
            {
                VehicleMovement.Instance.StartTurnManeuver(direction, duration, isFork);
            }

            if (!isFork && CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(duration * 0.5f, 0.12f);
            }

            WorldMover worldMover = intersectionChunk.GetComponent<WorldMover>();
            if (worldMover != null)
            {
                worldMover.enabled = false;
            }

            Vector3 localCenter = new Vector3(0f, 0f, intersectionChunk.IntersectionCenterZ);
            Vector3 centerWorld = intersectionChunk.transform.TransformPoint(localCenter);

            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (PauseController.Instance != null && PauseController.Instance.IsPaused)
                {
                    yield return null;
                    continue;
                }

                float dt = Time.deltaTime;
                elapsed += dt;

                float forwardSpeed = WorldSpeedController.Instance != null ? WorldSpeedController.Instance.CurrentSpeed : 0f;
                centerWorld.z -= forwardSpeed * dt;

                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                float currentAngle = Mathf.Lerp(0f, targetAngle, smoothT);

                if (intersectionChunk != null)
                {
                    Quaternion rot = Quaternion.Euler(0f, currentAngle, 0f);
                    intersectionChunk.transform.rotation = rot;

                    Vector3 rotOffset = rot * localCenter;
                    intersectionChunk.transform.position = new Vector3(
                        0f - rotOffset.x,
                        centerWorld.y,
                        centerWorld.z - rotOffset.z
                    );
                }

                yield return null;
            }

            if (intersectionChunk != null)
            {
                Quaternion finalRot = Quaternion.Euler(0f, targetAngle, 0f);
                intersectionChunk.transform.rotation = finalRot;

                Vector3 finalRotOffset = finalRot * localCenter;
                intersectionChunk.transform.position = new Vector3(
                    0f - finalRotOffset.x,
                    centerWorld.y,
                    centerWorld.z - finalRotOffset.z
                );

                if (worldMover != null)
                {
                    worldMover.enabled = true;
                }
            }

            if (EnvironmentSpawner.Instance != null && intersectionChunk != null)
            {
                EnvironmentSpawner.Instance.ResumeGenerationAfterTurn(intersectionChunk, direction);
            }
        }
    }
}


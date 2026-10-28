using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

public class ObstacleSpawner : MonoBehaviour
{
    [Header("Obstacles - Categories")]
    public GameObject[] carPrefabs;
    public GameObject[] edgeObstacles;    // napr. smetiaky, stĺpy
    public GameObject[] barricadePrefabs; // napr. ploty, barely
    public GameObject[] edgeObstacles;    // e.g., trash cans, poles
    public GameObject[] barricadePrefabs; // e.g., fences, barriers

    [Header("Spawn Chances (Sum ideally 100%)")]
    [Range(0, 100)] public float carChance = 60f;
    [Range(0, 100)] public float edgeChance = 20f;
    [Range(0, 100)] public float barricadeChance = 20f;

    [Header("Spawn Settings")]
    [Tooltip("Distance in meters between generated obstacles (frequency). Smaller number means denser traffic.")]
    [FormerlySerializedAs("spawnDistance")]
    public float spawnInterval = 14.5f; // Interval between spawns

    [Tooltip("Distance in meters ahead where obstacles appear. Increase if they seem to pop up suddenly.")]
    [FormerlySerializedAs("spawnZ")]
    public float spawnDistanceForward = 160f; // Forward spawn distance

    public float laneDistance = 3f;
    
    [Tooltip("Height (Y) at which obstacles appear. Change to negative if they float above the ground.")]
    public float spawnYOffset = 0f;

    private float distanceTraveled;

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isGameOver) return;

        // Accumulate distance based on current world speed
        distanceTraveled += WorldMover.moveSpeed * Time.deltaTime;
        
        // If we have traveled the required distance, spawn an obstacle
        if (distanceTraveled >= spawnInterval)
        {
            SpawnSophisticatedObstacle();
            distanceTraveled = 0; // Reset distance to the next obstacle
        }
    }

    void SpawnSophisticatedObstacle()
    {
        float roll = Random.Range(0f, 100f);

        if (roll < carChance)
        {
            if (carPrefabs.Length == 0) return;
            int lane = Random.Range(0, 3); 
            SpawnSingle(carPrefabs, lane, ObstacleType.Car);
        }
        else if (roll < carChance + edgeChance)
        {
            if (edgeObstacles.Length == 0) return;
            int lane = Random.value > 0.5f ? 0 : 2;
            SpawnSingle(edgeObstacles, lane, ObstacleType.Edge);
        }
        else
        {
            if (barricadePrefabs.Length == 0) return;
            
            bool twoBarricades = Random.value < 0.4f;

            if (twoBarricades)
            {
                int safeLane = Random.Range(0, 3);
                for (int i = 0; i < 3; i++)
                {
                    if (i != safeLane)
                    {
                        SpawnSingle(barricadePrefabs, i, ObstacleType.Barricade);
                    }
                }
            }
            else
            {
                int lane = Random.Range(0, 3);
                SpawnSingle(barricadePrefabs, lane, ObstacleType.Barricade);
            }
        }
    }

    void SpawnSingle(GameObject[] array, int lane, ObstacleType type)
    {
        int prefabIndex = Random.Range(0, array.Length);
        GameObject prefabToSpawn = array[prefabIndex];

        if (prefabToSpawn == null)
        {
            Debug.LogWarning("[ObstacleSpawner] Warning! Missing model in obstacle array (found 'None'). Skipping spawn.");
            return;
        }

        // Calculate position
        Vector3 spawnPos = new Vector3((lane - 1) * laneDistance, spawnYOffset, spawnDistanceForward    );
        
        GameObject obstacle = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
        
        if (obstacle.GetComponent<WorldMover>() == null)
        {
            obstacle.AddComponent<WorldMover>();
        }
        
        // Dynamically assign type and damage
        ObstacleData data = obstacle.GetComponent<ObstacleData>();
        if (data == null) data = obstacle.AddComponent<ObstacleData>();
        
        data.obstacleType = type;
        
        // --- AUDIO SETUP FOR NPC ---
        if (data.engineLoop != null)
        {
            AudioSource source = obstacle.AddComponent<AudioSource>();
            source.clip = data.engineLoop;
            source.loop = true;
            source.playOnAwake = true;
            source.spatialBlend = 1f; // Full 3D
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 2f;
            source.maxDistance = 20f;
            source.volume = data.engineVolume; // Use volume set in ObstacleData
            source.Play();
        }

        // Set default damage as a fallback
        switch (type)
        {
            case ObstacleType.Car: data.damageAmount = 100; break;
            case ObstacleType.Barricade: data.damageAmount = 50; break;
            case ObstacleType.Edge: data.damageAmount = 30; break;
        }

        // Set tag so collisions with player work
        obstacle.tag = "Obstacle";
    }
}

using UnityEngine;
using System.Collections.Generic;

public class EnvironmentSpawner : MonoBehaviour
{
    [Header("Prefab Assignment")]
    [Tooltip("List of prefabs for road segments. If there are multiple, they will be chosen randomly.")]
    public GameObject[] chunkPrefabs;

    [Header("Generation Settings")]
    [Tooltip("Length of a single segment along the Z axis. Ensure this matches your 3D model exactly, otherwise gaps will appear.")]
    public float chunkLength = 20f;

    [Tooltip("How many segments should be visible on screen at once.")]
    public int numberOfChunksOnScreen = 6;

    [Tooltip("Z position where road generation starts (e.g., slightly behind the player so they don't see the end of the road).")]
    public float startZPosition = -20f;

    [Tooltip("Z position behind the player where objects are no longer visible and can be safely destroyed.")]
    public float destroyZPosition = -30f;

    // Store currently active segments
    private List<GameObject> activeChunks = new List<GameObject>();

    void Start()
    {
        // Initial generation of a series of road segments
        for (int i = 0; i < numberOfChunksOnScreen; i++)
        {
            SpawnChunk(startZPosition + (i * chunkLength));
        }
    }

    void Update()
    {
        // If the game is over, we won't generate new segments
        if (GameManager.Instance != null && GameManager.Instance.isGameOver) return;

        // Check if the oldest (first) road segment has moved beyond the destroyZPosition
        if (activeChunks.Count > 0 && activeChunks[0].transform.position.z < destroyZPosition)
        {
            // Delete the old segment as it's no longer visible to the player
            DeleteOldestChunk();
            
            // Determine the exact Z position of the newest segment that is farthest forward
            float lastChunkZ = activeChunks[activeChunks.Count - 1].transform.position.z;
            
            // Generate the next segment right after it
            SpawnChunk(lastChunkZ + chunkLength);
        }
    }

    private void SpawnChunk(float spawnZPoint)
    {
        if (chunkPrefabs == null || chunkPrefabs.Length == 0) return;

        // Choose a random segment from the list (ideal if you have varied trees, etc.)
        int prefabIndex = Random.Range(0, chunkPrefabs.Length);
        
        // Vytvorenie inštancie s nulovým posunom X, Y a s určeným Z posunom
        Vector3 spawnPosition = new Vector3(0, 0, spawnZPoint);
        GameObject prefabToSpawn = chunkPrefabs[prefabIndex];
        
        if (prefabToSpawn == null)
        {
            Debug.LogError($"[EnvironmentSpawner] Pozor! V políčku Chunk Prefabs na pozícii {prefabIndex} ti chýba Prefab (je tam 'None'). Zmaž prázdne políčka alebo tam vlož Prefab!");
            return;
        }

        GameObject newChunk = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
        
        // Ensure the segment has automatic movement if it wasn't added to the prefab
        if (newChunk.GetComponent<WorldMover>() == null)
        {
            newChunk.AddComponent<WorldMover>();
        }

        // Tag it with something other than "Obstacle" so WorldMover doesn't move and destroy it prematurely at Z=-15
        newChunk.tag = "Untagged"; // Alebo si pre to sprav tag "Environment"
        
        // Uloženie do pamäti (listu)
        activeChunks.Add(newChunk);
    }

    private void DeleteOldestChunk()
    {
        GameObject oldestChunk = activeChunks[0];
        activeChunks.RemoveAt(0);
        Destroy(oldestChunk);
    }
}

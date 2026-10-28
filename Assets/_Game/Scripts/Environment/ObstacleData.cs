using UnityEngine;

public enum ObstacleType { Car, Barricade, Edge }

public class ObstacleData : MonoBehaviour
{
    public ObstacleType obstacleType;
    
    // Basic damage (if not overridden by player script)
    public int damageAmount = 100;

    [Header("Audio")]
    public AudioClip engineLoop;
    [Range(0f, 1f)]
    public float engineVolume = 0.1f;
}

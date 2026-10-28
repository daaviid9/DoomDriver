using UnityEngine;
using System.Collections;

public class NotificationManager : MonoBehaviour
{
    [Header("Settings")]
    public AudioSource audioSource;
    public AudioClip[] notificationClips;
    public float minInterval = 10f;
    public float maxInterval = 30f;

    void Start()
    {
        if (notificationClips != null && notificationClips.Length > 0 && audioSource != null)
        {
            StartCoroutine(DistractionRoutine());
        }
    }

    void Update()
    {
        // Stop any lingering notification sound if we leave active delivery or pause
        if (GameManager.Instance != null && (GameManager.Instance.CurrentState != GameState.DeliveryActive || GameManager.Instance.isPaused))
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
    }

    IEnumerator DistractionRoutine()
    {
        while (true)
        {
            float waitTime = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(waitTime);

            // Only play message/distraction notifications during active delivery driving
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.DeliveryActive || GameManager.Instance.isPaused)
            {
                continue;
            }

            if (notificationClips != null && notificationClips.Length > 0 && audioSource != null)
            {
                AudioClip clip = notificationClips[Random.Range(0, notificationClips.Length)];
                if (clip != null)
                {
                    audioSource.PlayOneShot(clip);
                }
            }
        }
    }
}

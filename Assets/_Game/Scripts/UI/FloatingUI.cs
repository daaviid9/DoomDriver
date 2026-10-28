using UnityEngine;

public class FloatingUI : MonoBehaviour
{
    [Header("Breathing Settings")]
    public float amplitude = 10f; // How many pixels it moves up and down
    public float speed = 2f;      // How fast it floats

    private RectTransform rectTransform;
    private Vector2 startPos;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            // Store the original position
            startPos = rectTransform.anchoredPosition;
        }
    }

    void Update()
    {
        if (rectTransform != null)
        {
            // Use a sine wave for smooth gentle motion
            float newY = startPos.y + Mathf.Sin(Time.time * speed) * amplitude;
            
            // Set the new position
            rectTransform.anchoredPosition = new Vector2(startPos.x, newY);
        }
    }
}

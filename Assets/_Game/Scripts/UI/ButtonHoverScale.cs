using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Hover Effect (Pop-up)")]
    [Tooltip("1.1 means 10% larger when the mouse hovers over the button")]
    public float hoverScaleSize = 1.1f;
    [Tooltip("Speed at which the button changes size")]
    public float scaleSpeed = 15f;
    
    private Vector3 originalScale;
    private Vector3 targetScale;

    void Start()
    {
        // Store the original button size (usually 1x1x1)
        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    void Update()
    {
        // Smoothly lerp between the base size and the enlarged size
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
    }

    // This function is called automatically as soon as the cursor enters the button
    public void OnPointerEnter(PointerEventData eventData)
    {
        // Target is to enlarge
        targetScale = originalScale * hoverScaleSize;
    }

    // When the mouse exits
    public void OnPointerExit(PointerEventData eventData)
    {
        // Target is the original state
        targetScale = originalScale;
    }
}

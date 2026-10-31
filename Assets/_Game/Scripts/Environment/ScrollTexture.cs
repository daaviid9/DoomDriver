using UnityEngine;

public class ScrollTexture : MonoBehaviour
{
    [Tooltip("Speed of texture scroll (Higher number - cars go visually faster)")]
    public float scrollSpeed = 0.5f;
    
    private Renderer rend;
    private Material mat;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
        {
            mat = rend.material;
        }
    }

    void Update()
    {
        // Nehybeme textúrou, ak hráč prehral
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DeliveryFailed) return;

        if (mat != null)
        {
            Debug.Log("this is doing somehitng");
            // Vypočítame offset, ktorý neustále narastá s časom
            float yOffset = Time.time * scrollSpeed;
            
            // Nastavíme Offset Y (niekedy X, závisí od rotácie tvojho modelu cesty)
            // Použijeme += alebo -= podľa toho, akým smerom to chceme posúvať
            mat.mainTextureOffset = new Vector2(0, -yOffset);
        }
    }
}

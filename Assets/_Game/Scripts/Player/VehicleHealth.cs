using UnityEngine;

/// <summary>
/// Manages player vehicle health points (HP), damage, and destruction events.
/// Strictly decoupled from audio, VFX, and movement.
/// </summary>
public class VehicleHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDestroyed => currentHealth <= 0;

    /// <summary>
    /// Event fired whenever health changes. Passes (currentHealth, maxHealth).
    /// </summary>
    public event System.Action<int, int> OnHealthChanged;

    /// <summary>
    /// Event fired whenever damage is taken. Passes damage amount.
    /// </summary>
    public event System.Action<int> OnDamageTaken;

    /// <summary>
    /// Event fired when health reaches zero.
    /// </summary>
    public event System.Action OnVehicleDestroyed;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Start()
    {
        ResetHealth();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDeliveryStarted += ResetHealth;
            GameManager.Instance.OnOrderSelectionStarted += ResetHealth;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDeliveryStarted -= ResetHealth;
            GameManager.Instance.OnOrderSelectionStarted -= ResetHealth;
        }
    }

    public void TakeDamage(int dmg)
    {
        if (currentHealth <= 0 || dmg <= 0) return;

        currentHealth -= dmg;
        Debug.Log($"<color=red>[VEHICLE HEALTH]</color> Took {dmg} damage. Remaining HP: {currentHealth}");

        OnDamageTaken?.Invoke(dmg);

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            OnVehicleDestroyed?.Invoke();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.FailDelivery();
            }
        }

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}


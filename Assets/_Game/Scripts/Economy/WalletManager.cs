using UnityEngine;

/// <summary>
/// Manages the player's wallet, earnings, expenses, and persistence.
/// Follows Single Responsibility Principle (SRP) by isolating economy logic from game flow.
/// </summary>
public class WalletManager : MonoBehaviour
{
    public static WalletManager Instance { get; private set; }

    [Header("Starting Economy")]
    [Tooltip("Initial wallet balance given to new players")]
    [SerializeField] private float initialBalance = 50f;

    private float currentBalance;
    public float Balance => currentBalance;

    /// <summary>
    /// Event fired whenever the player's balance changes.
    /// UI and other systems subscribe to this event to update without tight coupling.
    /// </summary>
    public static event System.Action<float> OnBalanceChanged;

    private const string BalancePrefKey = "PlayerBalance";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        LoadBalance();
    }

    private void LoadBalance()
    {
        currentBalance = PlayerPrefs.GetFloat(BalancePrefKey, initialBalance);
    }

    private void SaveBalance()
    {
        PlayerPrefs.SetFloat(BalancePrefKey, currentBalance);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Adds earned money (base delivery payout, tip) to player wallet.
    /// </summary>
    public void AddFunds(float amount)
    {
        if (amount <= 0f) return;

        currentBalance += amount;
        SaveBalance();
        OnBalanceChanged?.Invoke(currentBalance);
        Debug.Log($"<color=green>[WALLET]</color> Added {amount:F1}€. Current Balance: {currentBalance:F1}€");
    }

    /// <summary>
    /// Deducts money for expenses (fuel, vehicle repair, rent).
    /// Returns false if player does not have sufficient funds.
    /// </summary>
    public bool DeductFunds(float amount)
    {
        if (amount <= 0f) return true;
        if (currentBalance < amount) return false;

        currentBalance -= amount;
        SaveBalance();
        OnBalanceChanged?.Invoke(currentBalance);
        Debug.Log($"<color=orange>[WALLET]</color> Deducted {amount:F1}€. Current Balance: {currentBalance:F1}€");
        return true;
    }

    /// <summary>
    /// Checks whether the player can afford a specific amount without deducting.
    /// </summary>
    public bool CanAfford(float amount)
    {
        return currentBalance >= amount;
    }
}

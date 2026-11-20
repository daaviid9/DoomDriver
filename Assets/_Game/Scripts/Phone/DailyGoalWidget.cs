using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DoomDriver.Phone
{
    /// <summary>
    /// Displays the Daily Goal widget on the Home Screen.
    /// Visualizes target earnings (e.g. € 200), current progress, percentage, and a filled progress bar.
    /// Reacts in real-time to WalletManager balance changes.
    /// </summary>
    public class DailyGoalWidget : MonoBehaviour
    {
        [Header("Goal Configuration")]
        [Tooltip("Daily financial target amount in Euros")]
        [SerializeField] private float goalAmount = 200f;

        [Header("UI Elements")]
        [Tooltip("Big bold goal target text (e.g. '€ 200')")]
        [SerializeField] private TMP_Text goalAmountText;

        [Tooltip("Progress ratio text (e.g. '€ 27.50 / € 200')")]
        [SerializeField] private TMP_Text progressRatioText;

        [Tooltip("Percentage completion text (e.g. '14% done')")]
        [SerializeField] private TMP_Text progressPercentageText;

        [Tooltip("Progress bar fill Image (must have Image Type = Filled)")]
        [SerializeField] private Image progressBarFill;

        private void OnEnable()
        {
            WalletManager.OnBalanceChanged += HandleBalanceChanged;
            Refresh();
        }

        private void OnDisable()
        {
            WalletManager.OnBalanceChanged -= HandleBalanceChanged;
        }

        private void Start()
        {
            Refresh();
        }

        /// <summary>
        /// Recalculates and updates all progress texts and fill amounts.
        /// </summary>
        public void Refresh()
        {
            float current = WalletManager.Instance != null ? WalletManager.Instance.Balance : 0f;
            UpdateUI(current);
        }

        private void HandleBalanceChanged(float currentBalance)
        {
            UpdateUI(currentBalance);
        }

        private void UpdateUI(float current)
        {
            float ratio = goalAmount > 0f ? Mathf.Clamp01(current / goalAmount) : 0f;

            if (progressBarFill != null)
            {
                progressBarFill.fillAmount = ratio;
            }

            if (goalAmountText != null)
            {
                goalAmountText.text = $"€ {goalAmount:F0}";
            }

            if (progressRatioText != null)
            {
                progressRatioText.text = $"€ {current:F2} / € {goalAmount:F0}";
            }

            if (progressPercentageText != null)
            {
                progressPercentageText.text = $"{Mathf.RoundToInt(ratio * 100f)}% done";
            }
        }
    }
}


using System;
using UnityEngine;
using DoomDriver.Economy;

namespace DoomDriver.Phone
{
    /// <summary>
    /// Manages the player's FOMO meter (0-100) during active deliveries.
    /// Adheres to SoC: handles only FOMO drain/refill logic and emits OnFomoChanged events.
    /// </summary>
    public class FomoMeter : MonoBehaviour
    {
        [Header("FOMO Settings")]
        [SerializeField] private float fomoValue = 100f;
        [SerializeField] private float drainRate = 8f;
        [SerializeField] private float refillRate = 20f;

        private bool isDeliveryActive = false;
        private bool isRefilling = false;

        public float Value => fomoValue;
        public float NormalizedValue => Mathf.Clamp01(fomoValue / 100f);

        public event Action<float> OnFomoChanged;

        public void SetState(bool deliveryActive, bool refilling)
        {
            isDeliveryActive = deliveryActive;
            isRefilling = refilling;

            if (!isDeliveryActive && !Mathf.Approximately(fomoValue, 100f))
            {
                fomoValue = 100f;
                OnFomoChanged?.Invoke(fomoValue);
            }
        }

        private void Update()
        {
            if (!isDeliveryActive) return;

            float previous = fomoValue;
            if (isRefilling)
            {
                fomoValue += refillRate * Time.deltaTime;
            }
            else
            {
                fomoValue -= drainRate * GetDrainMultiplier() * Time.deltaTime;
            }

            fomoValue = Mathf.Clamp(fomoValue, 0f, 100f);

            if (!Mathf.Approximately(previous, fomoValue))
            {
                OnFomoChanged?.Invoke(fomoValue);
            }
        }

        private static float GetDrainMultiplier()
        {
            return UpgradeManager.Instance != null ? UpgradeManager.Instance.FomoDrainMultiplier : 1f;
        }
    }
}

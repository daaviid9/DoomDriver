using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DoomDriver.Economy
{
    /// <summary>
    /// Owns the courier upgrade catalog, purchases (paid through WalletManager), PlayerPrefs persistence
    /// and the upgrade effects that gameplay systems query (FOMO drain multiplier, phone shake multiplier,
    /// bumper collision shields). The Energy Shot lasts one working shift of the upstream ShiftManager.
    /// </summary>
    public class UpgradeManager : MonoBehaviour
    {
        public static UpgradeManager Instance { get; private set; }

        public const float ENERGY_SHOT_FOMO_DRAIN_MULTIPLIER = 0.75f;
        public const float PHONE_MOUNT_SHAKE_MULTIPLIER = 0.3f;
        private const int BUMPER_SHIELDS_PER_DELIVERY = 1;

        private const string CATALOG_RESOURCES_PATH = "Upgrades";
        private const string OWNED_KEY_PREFIX = "Upgrade_Owned_";
        private const string CHARGES_KEY_PREFIX = "Upgrade_Charges_";
        private const string ENERGY_SHOT_ACTIVE_KEY = "Upgrade_EnergyShotActive";

        [Tooltip("Items sold in the shop. If empty, all CourierUpgradeItem assets in Resources/Upgrades are loaded.")]
        [SerializeField] private List<CourierUpgradeItem> catalog = new List<CourierUpgradeItem>();

        /// <summary>
        /// Fired after a purchase or when a consumable is used up. Static (like WalletManager.OnBalanceChanged)
        /// so views can subscribe before the manager exists.
        /// </summary>
        public static event Action OnUpgradesChanged;

        private readonly UpgradeInventory inventory = new UpgradeInventory();
        private bool isEnergyShotActive;
        private int bumperShieldsLeft;

        public IReadOnlyList<CourierUpgradeItem> Catalog => catalog;
        public bool IsEnergyShotActive => isEnergyShotActive;
        public int BumperShieldsLeft => bumperShieldsLeft;
        public float FomoDrainMultiplier => isEnergyShotActive ? ENERGY_SHOT_FOMO_DRAIN_MULTIPLIER : 1f;
        public float PhoneShakeMultiplier => IsOwned(UpgradeType.PhoneMount) ? PHONE_MOUNT_SHAKE_MULTIPLIER : 1f;

        // Isolated test scenes disable wallet persistence; upgrades follow the same switch.
        private bool ShouldPersist => WalletManager.Instance == null || WalletManager.Instance.PersistsBalance;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            LoadCatalogIfEmpty();
            Load();
        }

        private void OnEnable()
        {
            SubscribeToGameManager();
            ShiftManager.OnShiftStarted -= HandleShiftStarted;
            ShiftManager.OnShiftStarted += HandleShiftStarted;
            ShiftManager.OnDayAdvanced -= HandleDayAdvanced;
            ShiftManager.OnDayAdvanced += HandleDayAdvanced;
        }

        private void Start()
        {
            SubscribeToGameManager();

            // A shot saved as active only survives a restart if that shift is still running.
            if (isEnergyShotActive && !IsShiftActive) EndEnergyShot();
        }

        private void OnDisable()
        {
            UnsubscribeFromGameManager();
            ShiftManager.OnShiftStarted -= HandleShiftStarted;
            ShiftManager.OnDayAdvanced -= HandleDayAdvanced;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool IsOwned(UpgradeType type) => inventory.IsOwned(type);

        public int GetCharges(UpgradeType type) => inventory.GetCharges(type);

        public PurchaseResult TryPurchase(CourierUpgradeItem item)
        {
            if (item == null) return PurchaseResult.InvalidItem;
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.DaySummary)
            {
                return PurchaseResult.ShopClosed;
            }
            if (WalletManager.Instance == null)
            {
                Debug.LogWarning("<color=yellow>[UpgradeManager]</color> Cannot purchase: WalletManager is missing.");
                return PurchaseResult.InvalidItem;
            }

            PurchaseResult result = inventory.TryPurchase(item.UpgradeType, item.IsConsumable, item.Price, WalletManager.Instance.DeductFunds);
            Debug.Log($"<color=lime>[UpgradeManager]</color> Purchase '{item.Name}' ({item.Price:F0}€): {result}");

            if (result == PurchaseResult.Success)
            {
                Save();
                OnUpgradesChanged?.Invoke();

                // Bought mid-shift: drink it right away so the effect shows on the very next delivery.
                if (item.UpgradeType == UpgradeType.EnergyShot && IsShiftActive) TryActivateEnergyShot();
            }

            return result;
        }

        /// <summary>
        /// Called by VehicleHealth before applying damage. Returns true when Bumper Armor absorbs the hit.
        /// </summary>
        public bool TryAbsorbCollision()
        {
            if (bumperShieldsLeft <= 0) return false;

            bumperShieldsLeft--;
            Debug.Log($"<color=lime>[UpgradeManager]</color> Bumper Armor absorbed a collision. Shields left: {bumperShieldsLeft}");
            return true;
        }

        private void LoadCatalogIfEmpty()
        {
            catalog.RemoveAll(item => item == null);
            if (catalog.Count > 0) return;

            catalog = Resources.LoadAll<CourierUpgradeItem>(CATALOG_RESOURCES_PATH)
                .OrderBy(item => item.UpgradeType)
                .ToList();

            if (catalog.Count == 0)
            {
                Debug.LogWarning($"<color=yellow>[UpgradeManager]</color> No CourierUpgradeItem assets found in Resources/{CATALOG_RESOURCES_PATH}. Shop will be empty.");
            }
        }

        private void SubscribeToGameManager()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.OnDeliveryStarted -= HandleDeliveryStarted;
            GameManager.Instance.OnDeliveryStarted += HandleDeliveryStarted;
            GameManager.Instance.OnOrderSelectionStarted -= HandleOrderSelectionStarted;
            GameManager.Instance.OnOrderSelectionStarted += HandleOrderSelectionStarted;
            GameManager.Instance.OnDeliveryCompleted -= HandleDeliveryEnded;
            GameManager.Instance.OnDeliveryCompleted += HandleDeliveryEnded;
            GameManager.Instance.OnDeliveryFailed -= HandleDeliveryEnded;
            GameManager.Instance.OnDeliveryFailed += HandleDeliveryEnded;
            GameManager.Instance.OnDaySummaryStarted -= HandleShiftEnded;
            GameManager.Instance.OnDaySummaryStarted += HandleShiftEnded;
        }

        private void UnsubscribeFromGameManager()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.OnDeliveryStarted -= HandleDeliveryStarted;
            GameManager.Instance.OnOrderSelectionStarted -= HandleOrderSelectionStarted;
            GameManager.Instance.OnDeliveryCompleted -= HandleDeliveryEnded;
            GameManager.Instance.OnDeliveryFailed -= HandleDeliveryEnded;
            GameManager.Instance.OnDaySummaryStarted -= HandleShiftEnded;
        }

        private static bool HasShiftManager => ShiftManager.Instance != null;
        private static bool IsShiftActive => HasShiftManager && ShiftManager.Instance.IsShiftActive;

        // Energy Shot: one charge is used when the shift starts (or right after buying it mid-shift)
        // and lasts until the shift ends (DaySummary) or the next day begins.
        private void HandleShiftStarted() => TryActivateEnergyShot();

        private void HandleShiftEnded() => EndEnergyShot();

        private void HandleDayAdvanced(int newDay) => EndEnergyShot();

        private void TryActivateEnergyShot()
        {
            if (isEnergyShotActive || !inventory.TryConsumeCharge(UpgradeType.EnergyShot)) return;

            isEnergyShotActive = true;
            Debug.Log("<color=lime>[UpgradeManager]</color> Caffeinated Hell Energy Shot active: FOMO drains 25% slower this shift.");
            Save();
            OnUpgradesChanged?.Invoke();
        }

        private void EndEnergyShot()
        {
            if (!isEnergyShotActive) return;

            isEnergyShotActive = false;
            Debug.Log("<color=lime>[UpgradeManager]</color> Energy Shot wore off.");
            Save();
        }

        private void HandleDeliveryStarted()
        {
            bumperShieldsLeft = IsOwned(UpgradeType.BumperArmor) ? BUMPER_SHIELDS_PER_DELIVERY : 0;

            // Isolated test scenes without a ShiftManager: the shot lasts one delivery instead.
            if (!HasShiftManager) TryActivateEnergyShot();
        }

        private void HandleDeliveryEnded() => bumperShieldsLeft = 0;

        private void HandleOrderSelectionStarted()
        {
            bumperShieldsLeft = 0;
            if (!HasShiftManager) EndEnergyShot();
        }

        private void Load()
        {
            if (!ShouldPersist) return;

            foreach (UpgradeType type in Enum.GetValues(typeof(UpgradeType)))
            {
                bool hasPermanent = PlayerPrefs.GetInt(OWNED_KEY_PREFIX + type, 0) == 1;
                int charges = PlayerPrefs.GetInt(CHARGES_KEY_PREFIX + type, 0);
                inventory.Restore(type, hasPermanent, charges);
            }

            isEnergyShotActive = PlayerPrefs.GetInt(ENERGY_SHOT_ACTIVE_KEY, 0) == 1;
        }

        private void Save()
        {
            if (!ShouldPersist) return;

            foreach (UpgradeType type in Enum.GetValues(typeof(UpgradeType)))
            {
                PlayerPrefs.SetInt(OWNED_KEY_PREFIX + type, inventory.HasPermanent(type) ? 1 : 0);
                PlayerPrefs.SetInt(CHARGES_KEY_PREFIX + type, inventory.GetCharges(type));
            }

            PlayerPrefs.SetInt(ENERGY_SHOT_ACTIVE_KEY, isEnergyShotActive ? 1 : 0);

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Clears all owned upgrades, charges, active Energy Shot state, and their PlayerPrefs keys.
        /// </summary>
        [ContextMenu("Reset All Upgrades")]
        public void ResetUpgrades()
        {
            inventory.Clear();
            isEnergyShotActive = false;
            bumperShieldsLeft = 0;

            if (ShouldPersist)
            {
                foreach (UpgradeType type in Enum.GetValues(typeof(UpgradeType)))
                {
                    PlayerPrefs.DeleteKey(OWNED_KEY_PREFIX + type);
                    PlayerPrefs.DeleteKey(CHARGES_KEY_PREFIX + type);
                }

                PlayerPrefs.DeleteKey(ENERGY_SHOT_ACTIVE_KEY);
                PlayerPrefs.Save();
            }

            Debug.Log("<color=#EF4444>[UpgradeManager]</color> All upgrades and charges reset.");
            OnUpgradesChanged?.Invoke();
        }
    }
}

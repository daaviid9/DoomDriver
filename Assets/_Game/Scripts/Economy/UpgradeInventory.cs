using System;
using System.Collections.Generic;

namespace DoomDriver.Economy
{
    public enum PurchaseResult
    {
        Success,
        AlreadyOwned,
        InsufficientFunds,
        InvalidItem,
        ShopClosed
    }

    /// <summary>
    /// Pure bookkeeping of owned upgrades and consumable charges.
    /// Has no Unity dependencies so the purchase rules can be covered by EditMode unit tests.
    /// </summary>
    public sealed class UpgradeInventory
    {
        private readonly HashSet<UpgradeType> permanentUpgrades = new HashSet<UpgradeType>();
        private readonly Dictionary<UpgradeType, int> charges = new Dictionary<UpgradeType, int>();

        public bool HasPermanent(UpgradeType type) => permanentUpgrades.Contains(type);

        public int GetCharges(UpgradeType type) => charges.TryGetValue(type, out int count) ? count : 0;

        public bool IsOwned(UpgradeType type) => HasPermanent(type) || GetCharges(type) > 0;

        /// <summary>
        /// Buys one upgrade. Payment runs only after the ownership rules pass, so a rejected
        /// purchase never takes money. tryPay must return false when funds are insufficient.
        /// </summary>
        public PurchaseResult TryPurchase(UpgradeType type, bool isConsumable, float price, Func<float, bool> tryPay)
        {
            if (tryPay == null || price < 0f) return PurchaseResult.InvalidItem;
            if (!isConsumable && HasPermanent(type)) return PurchaseResult.AlreadyOwned;
            if (!tryPay(price)) return PurchaseResult.InsufficientFunds;

            if (isConsumable)
            {
                charges[type] = GetCharges(type) + 1;
            }
            else
            {
                permanentUpgrades.Add(type);
            }

            return PurchaseResult.Success;
        }

        /// <summary>
        /// Uses up one charge of a consumable. Returns false when none is left.
        /// </summary>
        public bool TryConsumeCharge(UpgradeType type)
        {
            int count = GetCharges(type);
            if (count <= 0) return false;

            charges[type] = count - 1;
            return true;
        }

        /// <summary>
        /// Restores saved state for one upgrade type (used when loading PlayerPrefs).
        /// </summary>
        public void Restore(UpgradeType type, bool hasPermanent, int chargeCount)
        {
            if (hasPermanent) permanentUpgrades.Add(type);
            else permanentUpgrades.Remove(type);

            charges[type] = Math.Max(0, chargeCount);
        }

        /// <summary>
        /// Clears all owned permanent upgrades and consumable charges.
        /// </summary>
        public void Clear()
        {
            permanentUpgrades.Clear();
            charges.Clear();
        }
    }
}

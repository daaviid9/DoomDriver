using UnityEngine;

namespace DoomDriver.Economy
{
    /// <summary>
    /// Static shop definition of one courier upgrade (name, price, icon, effect type).
    /// Runtime ownership is NOT stored on the asset (assets are read-only in builds);
    /// it lives in UpgradeManager and is exposed here through IsPurchased.
    /// Assets placed in a Resources/Upgrades folder are picked up automatically by UpgradeManager.
    /// </summary>
    [CreateAssetMenu(fileName = "CourierUpgradeItem", menuName = "DoomDriver/Courier Upgrade Item")]
    public class CourierUpgradeItem : ScriptableObject
    {
        [SerializeField] private string displayName = "New Upgrade";
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField, Min(0f)] private float price = 10f;
        [SerializeField] private Sprite icon;
        [SerializeField] private UpgradeType upgradeType;

        [Tooltip("Consumables are bought per charge and used up (e.g. Energy Shot). Others are bought once and kept forever.")]
        [SerializeField] private bool isConsumable;

        public string Name => displayName;
        public string Description => description;
        public float Price => price;
        public Sprite Icon => icon;
        public UpgradeType UpgradeType => upgradeType;
        public bool IsConsumable => isConsumable;

        /// <summary>
        /// True when the player owns this upgrade (or holds at least one charge of a consumable).
        /// </summary>
        public bool IsPurchased => UpgradeManager.Instance != null && UpgradeManager.Instance.IsOwned(upgradeType);
    }
}

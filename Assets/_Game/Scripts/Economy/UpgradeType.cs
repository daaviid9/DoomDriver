namespace DoomDriver.Economy
{
    /// <summary>
    /// All upgrades sold in the KurierShop phone app.
    /// Values are serialized as ints inside CourierUpgradeItem assets: append new entries at the end only.
    /// </summary>
    public enum UpgradeType
    {
        PhoneMount,
        EnergyShot,
        BumperArmor,
        CoconutFreshener,
        RacingStripes
    }
}

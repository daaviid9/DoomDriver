using NUnit.Framework;
using DoomDriver.Economy;

/// <summary>
/// EditMode tests for the courier upgrade purchase rules (Window > General > Test Runner > EditMode).
/// </summary>
public class UpgradeInventoryTests
{
    private float balance;

    private bool Pay(float amount)
    {
        if (balance < amount) return false;
        balance -= amount;
        return true;
    }

    [SetUp]
    public void SetUp()
    {
        balance = 100f;
    }

    [Test]
    public void TryPurchase_PermanentWithEnoughFunds_OwnsItAndDeductsPrice()
    {
        var inventory = new UpgradeInventory();

        var result = inventory.TryPurchase(UpgradeType.BumperArmor, false, 80f, Pay);

        Assert.AreEqual(PurchaseResult.Success, result);
        Assert.IsTrue(inventory.IsOwned(UpgradeType.BumperArmor));
        Assert.AreEqual(20f, balance, 0.001f);
    }

    [Test]
    public void TryPurchase_InsufficientFunds_DoesNotOwnOrCharge()
    {
        var inventory = new UpgradeInventory();
        balance = 10f;

        var result = inventory.TryPurchase(UpgradeType.PhoneMount, false, 40f, Pay);

        Assert.AreEqual(PurchaseResult.InsufficientFunds, result);
        Assert.IsFalse(inventory.IsOwned(UpgradeType.PhoneMount));
        Assert.AreEqual(10f, balance, 0.001f);
    }

    [Test]
    public void TryPurchase_PermanentAlreadyOwned_RejectsWithoutCharging()
    {
        var inventory = new UpgradeInventory();
        inventory.TryPurchase(UpgradeType.CoconutFreshener, false, 10f, Pay);

        var result = inventory.TryPurchase(UpgradeType.CoconutFreshener, false, 10f, Pay);

        Assert.AreEqual(PurchaseResult.AlreadyOwned, result);
        Assert.AreEqual(90f, balance, 0.001f);
    }

    [Test]
    public void TryPurchase_Consumable_StacksCharges()
    {
        var inventory = new UpgradeInventory();

        inventory.TryPurchase(UpgradeType.EnergyShot, true, 15f, Pay);
        inventory.TryPurchase(UpgradeType.EnergyShot, true, 15f, Pay);

        Assert.AreEqual(2, inventory.GetCharges(UpgradeType.EnergyShot));
        Assert.AreEqual(70f, balance, 0.001f);
    }

    [Test]
    public void TryConsumeCharge_UsesOneChargeAndFailsWhenEmpty()
    {
        var inventory = new UpgradeInventory();
        inventory.TryPurchase(UpgradeType.EnergyShot, true, 15f, Pay);

        Assert.IsTrue(inventory.TryConsumeCharge(UpgradeType.EnergyShot));
        Assert.IsFalse(inventory.IsOwned(UpgradeType.EnergyShot));
        Assert.IsFalse(inventory.TryConsumeCharge(UpgradeType.EnergyShot));
    }

    [Test]
    public void TryPurchase_NullPaymentOrNegativePrice_IsInvalid()
    {
        var inventory = new UpgradeInventory();

        Assert.AreEqual(PurchaseResult.InvalidItem, inventory.TryPurchase(UpgradeType.PhoneMount, false, 40f, null));
        Assert.AreEqual(PurchaseResult.InvalidItem, inventory.TryPurchase(UpgradeType.PhoneMount, false, -1f, Pay));
        Assert.AreEqual(100f, balance, 0.001f);
    }

    [Test]
    public void Restore_SetsOwnershipAndClampsNegativeCharges()
    {
        var inventory = new UpgradeInventory();

        inventory.Restore(UpgradeType.PhoneMount, true, 0);
        inventory.Restore(UpgradeType.EnergyShot, false, -3);

        Assert.IsTrue(inventory.HasPermanent(UpgradeType.PhoneMount));
        Assert.AreEqual(0, inventory.GetCharges(UpgradeType.EnergyShot));
    }
}

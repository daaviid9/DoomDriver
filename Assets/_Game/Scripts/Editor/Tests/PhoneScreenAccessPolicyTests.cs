using NUnit.Framework;
using DoomDriver.Phone;

/// <summary>
/// EditMode tests for PhoneScreenAccessPolicy (Window > General > Test Runner > EditMode).
/// </summary>
public class PhoneScreenAccessPolicyTests
{
    [Test]
    public void ShopApp_InDaySummary_IsAllowed()
    {
        Assert.IsTrue(PhoneScreenAccessPolicy.CanOpen(PhoneScreen.ShopApp, GameState.DaySummary));
    }

    [Test]
    public void ShopApp_WithoutGameManager_IsAllowedForIsolatedTestScenes()
    {
        Assert.IsTrue(PhoneScreenAccessPolicy.CanOpen(PhoneScreen.ShopApp, null));
    }

    [TestCase(GameState.DayBriefing)]
    [TestCase(GameState.ShiftActive)]
    [TestCase(GameState.OrderSelection)]
    [TestCase(GameState.DeliveryActive)]
    [TestCase(GameState.DeliveryCompleted)]
    [TestCase(GameState.DeliveryFailed)]
    public void ShopApp_OutsideDaySummary_IsDenied(GameState state)
    {
        Assert.IsFalse(PhoneScreenAccessPolicy.CanOpen(PhoneScreen.ShopApp, state));
    }

    [TestCase(GameState.DayBriefing)]
    [TestCase(GameState.ShiftActive)]
    [TestCase(GameState.OrderSelection)]
    [TestCase(GameState.DeliveryActive)]
    [TestCase(GameState.DeliveryCompleted)]
    [TestCase(GameState.DeliveryFailed)]
    [TestCase(GameState.DaySummary)]
    public void BillsApp_InAnyState_IsNeverAllowed(GameState state)
    {
        Assert.IsFalse(PhoneScreenAccessPolicy.CanOpen(PhoneScreen.BillsApp, state));
    }

    [Test]
    public void BillsApp_WithoutGameManager_IsAlsoDenied()
    {
        Assert.IsFalse(PhoneScreenAccessPolicy.CanOpen(PhoneScreen.BillsApp, null));
    }

    [TestCase(PhoneScreen.Home)]
    [TestCase(PhoneScreen.CourierApp)]
    [TestCase(PhoneScreen.DoomTok)]
    public void OtherScreens_InOrderSelection_AreAllowed(PhoneScreen screen)
    {
        Assert.IsTrue(PhoneScreenAccessPolicy.CanOpen(screen, GameState.OrderSelection));
    }
}

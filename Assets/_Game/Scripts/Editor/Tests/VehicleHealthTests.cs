using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for VehicleHealth damage paths (Window > General > Test Runner > EditMode).
/// </summary>
public class VehicleHealthTests
{
    private GameObject vehicle;
    private VehicleHealth health;

    [SetUp]
    public void SetUp()
    {
        vehicle = new GameObject("TestVehicle");
        health = vehicle.AddComponent<VehicleHealth>();
        health.ResetHealth();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(vehicle);
    }

    [Test]
    public void TakeFatalDamage_FromFullHealth_DestroysVehicle()
    {
        bool destroyedRaised = false;
        health.OnVehicleDestroyed += () => destroyedRaised = true;

        health.TakeFatalDamage();

        Assert.IsTrue(health.IsDestroyed);
        Assert.AreEqual(0, health.CurrentHealth);
        Assert.IsTrue(destroyedRaised);
    }

    [Test]
    public void TakeDamage_PartialHit_KeepsVehicleAlive()
    {
        health.TakeDamage(30);

        Assert.AreEqual(health.MaxHealth - 30, health.CurrentHealth);
        Assert.IsFalse(health.IsDestroyed);
    }

    [Test]
    public void TakeFatalDamage_WhenAlreadyDestroyed_DoesNothing()
    {
        health.TakeFatalDamage();
        int destroyedCount = 0;
        health.OnVehicleDestroyed += () => destroyedCount++;

        health.TakeFatalDamage();

        Assert.AreEqual(0, destroyedCount);
    }
}

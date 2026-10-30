using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Updates the HUD health slider. Listens directly to VehicleHealth events.
/// Follows Separation of Concerns: does not depend on steering, audio, or collision logic.
/// </summary>
[RequireComponent(typeof(Slider))]
public class UIHealthBar : MonoBehaviour
{
    private Slider healthSlider;
    private VehicleHealth vehicleHealth;

    private void Start()
    {
        healthSlider = GetComponent<Slider>();

        vehicleHealth = Object.FindFirstObjectByType<VehicleHealth>();
        if (vehicleHealth != null)
        {
            healthSlider.maxValue = vehicleHealth.MaxHealth;
            healthSlider.value = vehicleHealth.CurrentHealth;
            vehicleHealth.OnHealthChanged += UpdateHealthBar;
        }
        else
        {
            Debug.LogWarning("[HealthBar] VehicleHealth not found in scene!");
        }
    }

    private void UpdateHealthBar(int newHealth, int maxHealth)
    {
        healthSlider.maxValue = maxHealth;
        healthSlider.value = newHealth;
    }

    private void OnDestroy()
    {
        if (vehicleHealth != null)
        {
            vehicleHealth.OnHealthChanged -= UpdateHealthBar;
        }
    }
}

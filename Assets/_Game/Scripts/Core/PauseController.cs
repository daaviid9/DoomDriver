using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages pause state, game timescale, and global audio pausing.
/// Decoupled from GameManager to adhere to Separation of Concerns.
/// </summary>
public class PauseController : MonoBehaviour
{
    public static PauseController Instance { get; private set; }

    public bool IsPaused { get; private set; } = false;

    /// <summary>
    /// Event fired whenever the pause state changes.
    /// UI subscribes to this to show/hide the pause panel.
    /// </summary>
    public event System.Action<bool> OnPauseToggled;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    /// <summary>
    /// Toggles the pause state on/off unless the game has ended in failure.
    /// </summary>
    public void TogglePause()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.DeliveryFailed) return;

        SetPaused(!IsPaused);
    }

    /// <summary>
    /// Sets the pause state explicitly.
    /// </summary>
    public void SetPaused(bool paused)
    {
        IsPaused = paused;

        Time.timeScale = IsPaused ? 0f : 1f;
        AudioListener.pause = IsPaused;

        OnPauseToggled?.Invoke(IsPaused);
    }

    /// <summary>
    /// Resumes the game (unpauses).
    /// </summary>
    public void Resume()
    {
        SetPaused(false);
    }
}


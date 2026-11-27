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
    public bool IsTutorialPaused { get; private set; } = false;

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
        if (IsTutorialPaused) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // If phone OS is open and currently inside a sub-app, Escape returns to Home instead of pausing
            if (DoomDriver.Phone.PhoneOSController.Instance != null &&
                DoomDriver.Phone.PhoneOSController.Instance.CurrentScreen != DoomDriver.Phone.PhoneScreen.Home)
            {
                return;
            }

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
        IsTutorialPaused = false;
        IsPaused = paused;

        Time.timeScale = IsPaused ? 0f : 1f;
        AudioListener.pause = IsPaused;

        OnPauseToggled?.Invoke(IsPaused);
    }

    /// <summary>
    /// Silently pauses or resumes the game for tutorial popups without opening the Pause menu UI.
    /// </summary>
    public void SetTutorialPaused(bool paused)
    {
        IsTutorialPaused = paused;
        IsPaused = paused;

        Time.timeScale = IsPaused ? 0f : 1f;
        AudioListener.pause = IsPaused;
    }

    /// <summary>
    /// Resumes the game (unpauses).
    /// </summary>
    public void Resume()
    {
        SetPaused(false);
    }
}


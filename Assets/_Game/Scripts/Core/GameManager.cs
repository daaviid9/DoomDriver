using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Game State")]
    public bool isGameOver = false;
    public bool isPaused = false;

    [Header("Score System")]
    public float currentScore = 0f;
    private float highscore = 0f;
    [Tooltip("UI Text for current score")]
    public TMP_Text scoreText;
    [Tooltip("UI Text for highscore")]
    public TMP_Text highscoreText;
    [Tooltip("UI Text for final score after game over")]
    public TMP_Text finalDistanceText;

    [Header("Difficulty Over Time")]
    [Tooltip("How much the game speed increases each second")]
    public float speedIncreaseRate = 0.1f;
    [Tooltip("Maximum allowed world speed (limit)")]
    public float maxSpeed = 30f;

    [Header("UI Panels")]
    public GameObject gameOverPanel;
    public GameObject pausePanel;
    [Header("Scene Settings")]
    [Tooltip("Name of the scene to return to (e.g., LoadingScreenMenu)")]
    public string mainMenuSceneName = "LoadingScreenMenu";

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        // Load highscore on start
        highscore = PlayerPrefs.GetFloat("Highscore", 0f);
        // Highscore UI is temporarily hidden
        // if (highscoreText != null)
        // {
        //     highscoreText.text = "Highscore: " + Mathf.FloorToInt(highscore).ToString();
        // }
    }

    private void Start()
    {
        // Ensure world movement speed is set when the scene loads. Prevents the issue where returning from Game Over leaves speed at 0.
        WorldMover.moveSpeed = 12f;
    }

    private void Update()
    {
        if (isGameOver) return;

        // Check for pause toggle (uses New Input System, as the project fully uses it)
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }

        // If the game is paused, score and obstacles will not increase here
        if (isPaused) return;

        // 1. Increase score based on distance travelled over time
        currentScore += WorldMover.moveSpeed * Time.deltaTime;

        if (scoreText != null)
        {
            scoreText.text = Mathf.FloorToInt(currentScore).ToString() + " m";
        }

        if (finalDistanceText != null)
        {
            finalDistanceText.text = "You managed to scroll for " + Mathf.FloorToInt(currentScore).ToString() + " meters";
        }

        // 2. Gradual game acceleration
        if (WorldMover.moveSpeed < maxSpeed)
        {
            WorldMover.moveSpeed += speedIncreaseRate * Time.deltaTime;
            // Clamp maximum speed
            if (WorldMover.moveSpeed > maxSpeed)
            {
                WorldMover.moveSpeed = maxSpeed;
            }
        }
    }

    public void GameOver()
    {
        if (isGameOver) return; // Prevent double trigger

        isGameOver = true;
        Debug.Log("Game Over!");

        // Save highscore if the player beat their record
        if (currentScore > highscore)
        {
            highscore = currentScore;
            // Highscore saving to PlayerPrefs and UI update is currently disabled
            // PlayerPrefs.SetFloat("Highscore", highscore);
            // PlayerPrefs.Save();
            // if (highscoreText != null)
            // {
            //     highscoreText.text = "Highscore: " + Mathf.FloorToInt(highscore).ToString();
            // }
        }

        // Show UI
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        // Pause the world speed via the static variable
        WorldMover.moveSpeed = 0f;
    }

    public void TogglePause()
    {
        if (isGameOver) return; // Cannot pause after Game Over

        isPaused = !isPaused;

        // Activate/deactivate Pause UI panel
        if (pausePanel != null)
        {
            pausePanel.SetActive(isPaused);
        }

        // Freeze / unfreeze time in Unity
        Time.timeScale = isPaused ? 0f : 1f;

        // Turn on/off all sounds globally (AudioListener.pause)
        AudioListener.pause = isPaused;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f; // Return time to normal if the player was paused
        AudioListener.pause = false; // Re-enable audio so sounds play again
        // Reset speed and reload
        WorldMover.moveSpeed = 12f; 
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ExitGame()
    {
        Time.timeScale = 1f; // Return time to normal if the player was paused
        AudioListener.pause = false; // Re-enable audio so sounds play again
        // Restore speed for a new game and load the menu scene
        WorldMover.moveSpeed = 12f; 
        SceneManager.LoadScene(mainMenuSceneName);
    }
}

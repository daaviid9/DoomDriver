using UnityEngine;
using UnityEngine.UI;

namespace DoomDriver.UI
{
    /// <summary>
    /// Pure presentation (View) controller for the Pause Menu panel.
    /// Binds Resume, Main Menu, and Quit buttons and delegates actions to PauseController and GameManager.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;

        private void Awake()
        {
            BindButton(resumeButton, OnResumeClicked);
            BindButton(mainMenuButton, OnMainMenuClicked);
            BindButton(quitButton, OnQuitClicked);
        }

        public void OnResumeClicked()
        {
            if (PauseController.Instance != null)
            {
                PauseController.Instance.Resume();
            }
        }

        public void OnMainMenuClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ExitGame();
            }
        }

        public void OnQuitClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.QuitGame();
            }
            else
            {
                Time.timeScale = 1f;
                AudioListener.pause = false;
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }
    }
}


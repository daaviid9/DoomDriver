using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using DoomDriver.Core;

namespace DoomDriver.UI
{
    /// <summary>
    /// Pure presentation (View) for the onboarding tutorial card and optional backdrops.
    /// Subscribes to TutorialManager events; performs no gameplay or persistence logic.
    /// </summary>
    public class TutorialUI : MonoBehaviour
    {
        [Header("Card References")]
        [SerializeField] private GameObject cardRoot;
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;

        [Header("Buttons")]
        [SerializeField] private Button continueButton;

        [Header("Optional Backdrops")]
        [Tooltip("Dark overlay placed on a Screen-Space Camera canvas behind the 3D phone during phone steps.")]
        [SerializeField] private GameObject phoneWorldBackdrop;

        [Tooltip("Dark overlay shown during paused driving tutorial steps.")]
        [SerializeField] private GameObject drivingBackdrop;

        [Header("Card Positions (Anchored Position)")]
        [Tooltip("Position of the card during Phone steps (kept to the side so it doesn't cover the phone).")]
        [SerializeField] private Vector2 phoneSidePosition = new Vector2(-420f, 0f);

        [Tooltip("Position of the card during Driving steps (centered on screen).")]
        [SerializeField] private Vector2 drivingCenterPosition = Vector2.zero;

        [SerializeField] private float inputCooldownSeconds = 0.25f;

        private bool isShowing;
        private bool allowKeyboardContinue;
        private float shownRealtime;

        private void Awake()
        {
            if (cardRect == null && cardRoot != null)
            {
                cardRect = cardRoot.GetComponent<RectTransform>();
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(HandleContinueClicked);
                continueButton.onClick.AddListener(HandleContinueClicked);
            }

            Hide();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            Subscribe();
            if (TutorialManager.Instance != null && TutorialManager.Instance.CurrentStep != TutorialStep.None)
            {
                HandlePromptShown(TutorialManager.Instance.CurrentPrompt);
            }
        }

        private void Subscribe()
        {
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnPromptShown -= HandlePromptShown;
                TutorialManager.Instance.OnPromptShown += HandlePromptShown;

                TutorialManager.Instance.OnPromptHidden -= Hide;
                TutorialManager.Instance.OnPromptHidden += Hide;
            }
        }

        private void OnDisable()
        {
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnPromptShown -= HandlePromptShown;
                TutorialManager.Instance.OnPromptHidden -= Hide;
            }
        }

        private void Update()
        {
            if (!isShowing || !allowKeyboardContinue) return;
            if (Time.unscaledTime - shownRealtime < inputCooldownSeconds) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame ||
                                     keyboard.numpadEnterKey.wasPressedThisFrame ||
                                     keyboard.spaceKey.wasPressedThisFrame))
            {
                HandleContinueClicked();
            }
        }

        private void HandlePromptShown(TutorialPrompt prompt)
        {
            isShowing = true;
            allowKeyboardContinue = prompt.IsDrivingStep;
            shownRealtime = Time.unscaledTime;

            if (titleText != null) titleText.text = prompt.Title;
            if (bodyText != null) bodyText.text = prompt.Message;

            if (cardRect != null)
            {
                cardRect.anchoredPosition = prompt.IsDrivingStep ? drivingCenterPosition : phoneSidePosition;
            }

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(true);
            }

            if (phoneWorldBackdrop != null) phoneWorldBackdrop.SetActive(!prompt.IsDrivingStep);
            if (drivingBackdrop != null) drivingBackdrop.SetActive(prompt.IsDrivingStep);
            if (cardRoot != null) cardRoot.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Hide()
        {
            isShowing = false;
            allowKeyboardContinue = false;

            if (cardRoot != null) cardRoot.SetActive(false);
            if (phoneWorldBackdrop != null) phoneWorldBackdrop.SetActive(false);
            if (drivingBackdrop != null) drivingBackdrop.SetActive(false);
        }

        private void HandleContinueClicked()
        {
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.CompleteCurrentPrompt();
            }
        }
    }
}

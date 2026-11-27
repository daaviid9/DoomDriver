using System;
using System.Collections;
using UnityEngine;
using DoomDriver.Phone;

namespace DoomDriver.Core
{
    public enum TutorialStep
    {
        None,
        StartShiftBriefing,
        CourierAppOrders,
        DrivingControls,
        DrivingFomo,
        OngoingShiftProgress,
        BonusOvertime,
        EndShiftSummary
    }

    [Serializable]
    public struct TutorialPrompt
    {
        public TutorialStep Step;
        public string Title;
        public string Message;
        public bool IsDrivingStep;
    }

    /// <summary>
    /// Coordinates the first-time player onboarding tutorial inside the main scene.
    /// Adheres to SRP: tracks step completion and reacts to game/shift/phone events,
    /// while delegating all rendering to TutorialUI (View).
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        private const string KEY_TUTORIAL_COMPLETED = "DoomDriver_TutorialCompleted";

        [Header("Developer & Playtest Controls")]
        [Tooltip("Uncheck this in Unity Inspector to disable the tutorial while testing other features.")]
        [SerializeField] private bool enableTutorial = true;

        [Tooltip("If checked, ignores saved PlayerPrefs in Unity Editor so the tutorial plays every time.")]
        [SerializeField] private bool alwaysShowInEditor = false;

        [Header("Trigger Settings")]
        [Tooltip("Delay after starting delivery before pausing to show driving controls (lets phone hand lower).")]
        [SerializeField] private float drivingControlsDelay = 0.55f;

        [Tooltip("FOMO value threshold (0-100) that triggers the phone/reels tutorial popup.")]
        [SerializeField] private float fomoTriggerThreshold = 55f;

        [Header("Scene References (Optional - Auto-resolved if empty)")]
        [SerializeField] private PhoneSystem phoneSystem;

        public event Action<TutorialPrompt> OnPromptShown;
        public event Action OnPromptHidden;

        public bool IsTutorialActive => enableTutorial && !isCompleted;
        public TutorialStep CurrentStep { get; private set; } = TutorialStep.None;
        public TutorialPrompt CurrentPrompt { get; private set; }

        private bool isCompleted;
        private bool shownBriefing;
        private bool shownCourierApp;
        private bool shownDrivingControls;
        private bool shownDrivingFomo;
        private bool shownOngoing;
        private bool shownBonus;
        private bool shownEndShift;

        private Coroutine delayedStepRoutine;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (phoneSystem == null)
            {
                phoneSystem = FindFirstObjectByType<PhoneSystem>();
            }

            LoadCompletionState();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            Subscribe();
            EvaluateInitialState();
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopDelayedStep();
        }

        private void Update()
        {
            if (!IsTutorialActive) return;

            // Check FOMO threshold during active driving once driving controls have been explained
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.DeliveryActive &&
                shownDrivingControls &&
                !shownDrivingFomo &&
                CurrentStep == TutorialStep.None &&
                phoneSystem != null &&
                phoneSystem.FomoValue <= fomoTriggerThreshold)
            {
                ShowStep(TutorialStep.DrivingFomo);
            }
        }

        [ContextMenu("Reset Tutorial Progress")]
        public void ResetTutorialProgress()
        {
            PlayerPrefs.DeleteKey(KEY_TUTORIAL_COMPLETED);
            PlayerPrefs.Save();

            isCompleted = false;
            shownBriefing = false;
            shownCourierApp = false;
            shownDrivingControls = false;
            shownDrivingFomo = false;
            shownOngoing = false;
            shownBonus = false;
            shownEndShift = false;
            CurrentStep = TutorialStep.None;

            Debug.Log("<color=#38BDF8>[TutorialManager]</color> Tutorial progress reset.");
        }

        public void CompleteCurrentPrompt()
        {
            if (CurrentStep == TutorialStep.None) return;

            if (PauseController.Instance != null && PauseController.Instance.IsTutorialPaused)
            {
                PauseController.Instance.SetTutorialPaused(false);
            }

            HideCurrentPrompt();
        }

        private void LoadCompletionState()
        {
            if (!enableTutorial)
            {
                isCompleted = true;
                return;
            }

#if UNITY_EDITOR
            if (alwaysShowInEditor)
            {
                isCompleted = false;
                return;
            }
#endif
            isCompleted = PlayerPrefs.GetInt(KEY_TUTORIAL_COMPLETED, 0) == 1;
        }

        private void MarkTutorialCompleted()
        {
            isCompleted = true;
            PlayerPrefs.SetInt(KEY_TUTORIAL_COMPLETED, 1);
            PlayerPrefs.Save();
        }

        private void Subscribe()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            }

            if (PhoneOSController.Instance != null)
            {
                PhoneOSController.Instance.OnScreenChanged -= HandlePhoneScreenChanged;
                PhoneOSController.Instance.OnScreenChanged += HandlePhoneScreenChanged;
            }

            ShiftManager.OnShiftStarted -= HandleShiftStarted;
            ShiftManager.OnShiftStarted += HandleShiftStarted;

            ShiftManager.OnDayAdvanced -= HandleDayAdvanced;
            ShiftManager.OnDayAdvanced += HandleDayAdvanced;
        }

        private void Unsubscribe()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }

            if (PhoneOSController.Instance != null)
            {
                PhoneOSController.Instance.OnScreenChanged -= HandlePhoneScreenChanged;
            }

            ShiftManager.OnShiftStarted -= HandleShiftStarted;
            ShiftManager.OnDayAdvanced -= HandleDayAdvanced;
        }

        private void EvaluateInitialState()
        {
            if (GameManager.Instance == null) return;

            bool shiftActive = ShiftManager.Instance != null && ShiftManager.Instance.IsShiftActive;
            Debug.Log($"<color=#38BDF8>[TutorialManager]</color> Init -> Active: {IsTutorialActive}, GameState: {GameManager.Instance.CurrentState}, ShiftActive: {shiftActive}");

            if (!IsTutorialActive) return;
            HandleGameStateChanged(GameManager.Instance.CurrentState);
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (!IsTutorialActive) return;

            StopDelayedStep();

            switch (state)
            {
                case GameState.DayBriefing:
                    if (!shownBriefing)
                    {
                        ShowStep(TutorialStep.StartShiftBriefing);
                    }
                    break;

                case GameState.OrderSelection:
                    EvaluateHomeOrCourierStep();
                    break;

                case GameState.DeliveryActive:
                    HideCurrentPrompt();
                    if (!shownDrivingControls)
                    {
                        delayedStepRoutine = StartCoroutine(ShowDrivingControlsDelayed(drivingControlsDelay));
                    }
                    break;

                case GameState.DeliveryCompleted:
                case GameState.DeliveryFailed:
                    HideCurrentPrompt();
                    break;

                case GameState.DaySummary:
                    if (!shownEndShift)
                    {
                        ShowStep(TutorialStep.EndShiftSummary);
                    }
                    break;
            }
        }

        private void HandlePhoneScreenChanged(PhoneScreen screen)
        {
            if (!IsTutorialActive) return;

            if (screen == PhoneScreen.CourierApp)
            {
                if (!shownCourierApp)
                {
                    ShowStep(TutorialStep.CourierAppOrders);
                }
                else if (CurrentStep == TutorialStep.OngoingShiftProgress || CurrentStep == TutorialStep.BonusOvertime)
                {
                    HideCurrentPrompt();
                }
            }
            else if (screen == PhoneScreen.Home)
            {
                if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.OrderSelection)
                {
                    EvaluateHomeOrCourierStep();
                }
            }
            else
            {
                HideCurrentPrompt();
            }
        }

        private void HandleShiftStarted()
        {
            if (!IsTutorialActive) return;

            if (CurrentStep == TutorialStep.StartShiftBriefing)
            {
                HideCurrentPrompt();
            }
        }

        private void HandleDayAdvanced(int newDay)
        {
            if (!IsTutorialActive) return;

            if (CurrentStep == TutorialStep.EndShiftSummary)
            {
                HideCurrentPrompt();
            }

            if (shownEndShift)
            {
                MarkTutorialCompleted();
            }
        }

        private void EvaluateHomeOrCourierStep()
        {
            var phone = PhoneOSController.Instance;
            var shift = ShiftManager.Instance;

            if (phone != null && phone.CurrentScreen == PhoneScreen.CourierApp)
            {
                if (!shownCourierApp)
                {
                    ShowStep(TutorialStep.CourierAppOrders);
                }
                return;
            }

            if (shift != null && shift.IsShiftActive)
            {
                if (shift.IsQuotaMet && !shownBonus)
                {
                    ShowStep(TutorialStep.BonusOvertime);
                }
                else if (shift.DeliveriesCompletedToday > 0 && !shift.IsQuotaMet && !shownOngoing)
                {
                    ShowStep(TutorialStep.OngoingShiftProgress);
                }
            }
        }

        private IEnumerator ShowDrivingControlsDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            delayedStepRoutine = null;

            if (IsTutorialActive &&
                GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.DeliveryActive &&
                !shownDrivingControls)
            {
                ShowStep(TutorialStep.DrivingControls);
            }
        }

        private void StopDelayedStep()
        {
            if (delayedStepRoutine != null)
            {
                StopCoroutine(delayedStepRoutine);
                delayedStepRoutine = null;
            }
        }

        private void ShowStep(TutorialStep step)
        {
            CurrentStep = step;
            TutorialPrompt prompt = BuildPrompt(step);
            CurrentPrompt = prompt;

            switch (step)
            {
                case TutorialStep.StartShiftBriefing: shownBriefing = true; break;
                case TutorialStep.CourierAppOrders: shownCourierApp = true; break;
                case TutorialStep.DrivingControls: shownDrivingControls = true; break;
                case TutorialStep.DrivingFomo: shownDrivingFomo = true; break;
                case TutorialStep.OngoingShiftProgress: shownOngoing = true; break;
                case TutorialStep.BonusOvertime: shownBonus = true; break;
                case TutorialStep.EndShiftSummary: shownEndShift = true; break;
            }

            if (prompt.IsDrivingStep && PauseController.Instance != null)
            {
                PauseController.Instance.SetTutorialPaused(true);
            }

            OnPromptShown?.Invoke(prompt);
        }

        private void HideCurrentPrompt()
        {
            if (CurrentStep == TutorialStep.None) return;
            CurrentStep = TutorialStep.None;
            OnPromptHidden?.Invoke();
        }

        private static TutorialPrompt BuildPrompt(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.StartShiftBriefing:
                    return new TutorialPrompt
                    {
                        Step = step,
                        Title = "MORNING DISPATCH",
                        Message = "Here is your shift overview: today's delivery quota, daily bills due tonight, and your overtime bonus.\n\nClick <b>START SHIFT</b> on your phone to clock in!",
                        IsDrivingStep = false
                    };

                case TutorialStep.CourierAppOrders:
                    return new TutorialPrompt
                    {
                        Step = step,
                        Title = "CHOOSE AN ORDER",
                        Message = "Orders come in <b>3 difficulties</b> (Easy, Medium, Hard). You can choose any of them. Longer routes pay more, but carry higher risk.\n\nEach order has a distance and a <b>time limit</b> - being late drains your payout, and if it runs out, the customer cancels the order!",
                        IsDrivingStep = false
                    };

                case TutorialStep.DrivingControls:
                    return new TutorialPrompt
                    {
                        Step = step,
                        Title = "DRIVING & GPS",
                        Message = "Use <b>A / D</b> (or Left / Right arrows) to steer and <b>W / S</b> to control your speed.\n\nWatch the <b>dashboard GPS</b> for upcoming turns and avoid traffic!",
                        IsDrivingStep = true
                    };

                case TutorialStep.DrivingFomo:
                    return new TutorialPrompt
                    {
                        Step = step,
                        Title = "FIGHTING BOREDOM",
                        Message = "Your focus is fading and eyes are starting to feel heavy!\n\nHold <b>SPACE</b> to raise your phone and scroll DoomTok reels to refill your dopamine. Just don't crash while watching!",
                        IsDrivingStep = true
                    };

                case TutorialStep.OngoingShiftProgress:
                    return new TutorialPrompt
                    {
                        Step = step,
                        Title = "SHIFT PROGRESS",
                        Message = "Nice delivery! Here on the shift card you can track your completed orders and today's earnings.\n\nOpen <b>Quick-Bite</b> when you're ready for the next run.",
                        IsDrivingStep = false
                    };

                case TutorialStep.BonusOvertime:
                    return new TutorialPrompt
                    {
                        Step = step,
                        Title = "QUOTA MET — OVERTIME!",
                        Message = "You hit today's quota! You can now <b>CLOCK OUT</b> to end the day, or take extra orders for a <b>+25% Speed Tip bonus</b>.",
                        IsDrivingStep = false
                    };

                case TutorialStep.EndShiftSummary:
                    return new TutorialPrompt
                    {
                        Step = step,
                        Title = "END OF SHIFT",
                        Message = "Here is your daily summary: shift earnings minus daily bills.\n<b>Doom Garage</b> is open now. You can buy car upgrades before tomorrow. \n\n<b>Advance to Next Day</b> when you're ready!",
                        IsDrivingStep = false
                    };

                default:
                    return default;
            }
        }
    }
}

using System;
using UnityEngine;
using DoomDriver.Environment;

namespace DoomDriver.Core
{
    /// <summary>
    /// Holds the active daily shift briefing state (day index, dispatcher message, weather condition)
    /// and synchronizes weather atmosphere presets (fog density, windshield rain, wet road reflections)
    /// with WeatherManager. Provides a decoupled integration contract for ShiftBriefingUI (Dev 5).
    /// </summary>
    public class DailyShiftBriefing : MonoBehaviour
    {
        public static DailyShiftBriefing Instance { get; private set; }

        [Header("Shift Schedule")]
        [SerializeField] private int currentDay = 1;
        [SerializeField] private bool advanceWeatherOnLobbyReturn = true;

        private static readonly string[] DayNames =
        {
            "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
        };

        public int CurrentDay => currentDay;
        public string CurrentDayName => DayNames[(Mathf.Max(1, currentDay) - 1) % DayNames.Length];
        public WeatherCondition CurrentWeatherCondition { get; private set; } = WeatherCondition.LightRain;
        public string CurrentBriefingHeadline { get; private set; } = string.Empty;

        /// <summary>
        /// Fired whenever the daily shift briefing updates (day number, day name, briefing text, weather condition).
        /// </summary>
        public event Action<int, string, string, WeatherCondition> OnBriefingUpdated;

        private bool hasCompletedFirstLobbyEntry;

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

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnOrderSelectionStarted += HandleLobbyEntered;
            }

            if (WeatherManager.Instance != null)
            {
                SyncFromWeatherManager(WeatherManager.Instance.CurrentCondition, WeatherManager.Instance.CurrentPreset);
                WeatherManager.Instance.OnWeatherChanged += SyncFromWeatherManager;
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnOrderSelectionStarted -= HandleLobbyEntered;
            }

            if (WeatherManager.Instance != null)
            {
                WeatherManager.Instance.OnWeatherChanged -= SyncFromWeatherManager;
            }

            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Applies a dispatcher briefing text (e.g. from ShiftBriefingUI) and updates fog, rain, and wet roads.
        /// </summary>
        public void SetDispatcherBriefing(string briefingText, bool instant = false)
        {
            CurrentBriefingHeadline = briefingText ?? string.Empty;

            if (WeatherManager.Instance != null)
            {
                CurrentWeatherCondition = WeatherManager.Instance.ApplyBriefingCondition(CurrentBriefingHeadline, instant);
            }
            else
            {
                CurrentWeatherCondition = WeatherManager.ParseConditionFromBriefing(CurrentBriefingHeadline);
            }

            OnBriefingUpdated?.Invoke(currentDay, CurrentDayName, CurrentBriefingHeadline, CurrentWeatherCondition);
        }

        /// <summary>
        /// Advances to the next shift day and selects its corresponding weather atmosphere condition.
        /// </summary>
        public void AdvanceToNextDay(bool instant = false)
        {
            currentDay++;
            if (WeatherManager.Instance != null)
            {
                WeatherManager.Instance.SelectDailyWeather(currentDay, instant);
            }
        }

        private void HandleLobbyEntered()
        {
            if (!hasCompletedFirstLobbyEntry)
            {
                hasCompletedFirstLobbyEntry = true;
                return;
            }

            if (advanceWeatherOnLobbyReturn)
            {
                AdvanceToNextDay(instant: false);
            }
        }

        private void SyncFromWeatherManager(WeatherCondition condition, WeatherPreset preset)
        {
            CurrentWeatherCondition = condition;
            string prefix = $"{CurrentDayName}: ";
            string body = preset != null ? preset.BriefingHeadline : condition.ToString();
            CurrentBriefingHeadline = body.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? body : $"{prefix}{body}";
            OnBriefingUpdated?.Invoke(currentDay, CurrentDayName, CurrentBriefingHeadline, CurrentWeatherCondition);
        }
    }
}

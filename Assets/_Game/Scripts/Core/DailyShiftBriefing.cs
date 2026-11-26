using System;
using UnityEngine;
using DoomDriver.Environment;

namespace DoomDriver.Core
{
    /// <summary>
    /// Holds the active daily briefing state (day name, dispatcher message, weather condition)
    /// and synchronizes weather atmosphere presets (fog density, windshield rain, wet road reflections)
    /// with WeatherManager. The day index itself is owned by ShiftManager; weather changes when it advances.
    /// </summary>
    public class DailyShiftBriefing : MonoBehaviour
    {
        public static DailyShiftBriefing Instance { get; private set; }

        private static readonly string[] DayNames =
        {
            "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
        };

        public int CurrentDay => ShiftManager.Instance != null ? ShiftManager.Instance.CurrentDay : 1;
        public string CurrentDayName => DayNameFor(CurrentDay);
        public WeatherCondition CurrentWeatherCondition { get; private set; } = WeatherCondition.LightRain;
        public string CurrentBriefingHeadline { get; private set; } = string.Empty;

        /// <summary>
        /// Fired whenever the daily shift briefing updates (day number, day name, briefing text, weather condition).
        /// </summary>
        public event Action<int, string, string, WeatherCondition> OnBriefingUpdated;

        private bool isSubscribedToDayAdvanced;

        public static string DayNameFor(int day) => DayNames[(Mathf.Max(1, day) - 1) % DayNames.Length];

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
            // ShiftManager is created by GameManager.EnsureCompanionControllers; do not add a second one here.
        }

        private void Start()
        {
            ShiftManager.OnDayAdvanced += HandleDayAdvanced;
            isSubscribedToDayAdvanced = true;

            if (WeatherManager.Instance != null)
            {
                WeatherManager.Instance.OnWeatherChanged += SyncFromWeatherManager;

                // A saved game resumes on a later day: show that day's weather instead of the scene default.
                if (CurrentDay > 1)
                {
                    WeatherManager.Instance.SelectDailyWeather(CurrentDay, instant: true);
                }
                else
                {
                    SyncFromWeatherManager(WeatherManager.Instance.CurrentCondition, WeatherManager.Instance.CurrentPreset);
                }
            }
        }

        private void OnDestroy()
        {
            // Static event: always detach, otherwise a reloaded scene keeps calling a destroyed briefing.
            if (isSubscribedToDayAdvanced)
            {
                ShiftManager.OnDayAdvanced -= HandleDayAdvanced;
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
            string text = briefingText ?? string.Empty;

            if (WeatherManager.Instance != null)
            {
                // ApplyWeather raises OnWeatherChanged, which overwrites the headline with the preset text.
                CurrentWeatherCondition = WeatherManager.Instance.ApplyBriefingCondition(text, instant);
            }
            else
            {
                CurrentWeatherCondition = WeatherManager.ParseConditionFromBriefing(text);
            }

            CurrentBriefingHeadline = text;
            OnBriefingUpdated?.Invoke(CurrentDay, CurrentDayName, CurrentBriefingHeadline, CurrentWeatherCondition);
        }

        private void HandleDayAdvanced(int day)
        {
            if (WeatherManager.Instance != null)
            {
                WeatherManager.Instance.SelectDailyWeather(day, instant: false);
            }
        }

        private void SyncFromWeatherManager(WeatherCondition condition, WeatherPreset preset)
        {
            CurrentWeatherCondition = condition;
            string prefix = $"{CurrentDayName}: ";
            string body = preset != null ? preset.BriefingHeadline : condition.ToString();
            CurrentBriefingHeadline = body.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? body : $"{prefix}{body}";
            OnBriefingUpdated?.Invoke(CurrentDay, CurrentDayName, CurrentBriefingHeadline, CurrentWeatherCondition);
        }
    }
}

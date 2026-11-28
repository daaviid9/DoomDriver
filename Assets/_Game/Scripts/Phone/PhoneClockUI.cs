using System;
using UnityEngine;
using TMPro;

namespace DoomDriver.Phone
{
    /// <summary>
    /// Displays the smartphone status bar digital clock.
    /// Updates TMP_Text only when the minute changes to avoid per-frame string allocations.
    /// </summary>
    public class PhoneClockUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text clockText;

        private int lastMinute = -1;

        public void Bind(TMP_Text targetText)
        {
            clockText = targetText;
            lastMinute = -1;
            UpdateClockIfChanged();
        }

        private void OnEnable()
        {
            lastMinute = -1;
            UpdateClockIfChanged();
        }

        private void Update()
        {
            UpdateClockIfChanged();
        }

        private void UpdateClockIfChanged()
        {
            if (clockText == null) return;

            DateTime now = DateTime.Now;
            if (now.Minute == lastMinute) return;

            lastMinute = now.Minute;
            clockText.text = $"{now.Hour:D2}:{now.Minute:D2}";
        }
    }
}


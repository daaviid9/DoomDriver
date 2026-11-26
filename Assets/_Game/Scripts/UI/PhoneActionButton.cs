using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DoomDriver.Phone;

namespace DoomDriver.UI
{
    /// <summary>
    /// Single call-to-action button on a phone overlay (e.g. "Start Shift", "Clock Out &amp; Sleep").
    /// Implements IPhoneNavigable so PhoneInputNavigator confirms it with Enter/Space; mouse click works too.
    /// </summary>
    public sealed class PhoneActionButton : MonoBehaviour, IPhoneNavigable, IPointerEnterHandler, IPointerClickHandler
    {
        private const float FOCUSED_SCALE = PhoneUIStyle.FOCUSED_SCALE;
        private const float SCALE_SPEED = 14f;

        public event Action<IPhoneNavigable> OnHovered;

        private Action onPressed;
        private Outline outline;
        private bool isFocused;
        private float currentScale = 1f;

        public void Initialize(Action pressed)
        {
            onPressed = pressed;
            outline = GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            outline.effectColor = PhoneUIStyle.ButtonOutline;
            SetFocused(false);
        }

        public void SetFocused(bool focused)
        {
            isFocused = focused;
            // Same focus cue as the order card's Accept button: the indigo outline thickens.
            if (outline != null) outline.effectDistance = focused ? PhoneUIStyle.ButtonOutlineFocused : PhoneUIStyle.ButtonOutlineNormal;
        }

        public void Select()
        {
            onPressed?.Invoke();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnHovered?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Select();
        }

        private void Update()
        {
            float target = isFocused ? FOCUSED_SCALE : 1f;
            if (Mathf.Approximately(currentScale, target)) return;

            // Unscaled: the lobby may be paused while the overlay is open.
            currentScale = Mathf.MoveTowards(currentScale, target, SCALE_SPEED * Time.unscaledDeltaTime);
            transform.localScale = new Vector3(currentScale, currentScale, 1f);
        }
    }
}

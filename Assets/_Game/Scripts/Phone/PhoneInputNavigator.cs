using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace DoomDriver.Phone
{
    /// <summary>
    /// Modular, decoupled navigation controller for the smartphone interface ("DoomOS").
    /// Operates entirely on IPhoneNavigable elements, allowing it to navigate seamlessly
    /// across Order Cards, Home Screen App Icons, Shop Items, and Dialogs.
    /// </summary>
    public class PhoneInputNavigator : MonoBehaviour
    {
        [Header("Navigation Settings")]
        [Tooltip("Whether moving past the last element wraps around to the first, and vice versa")]
        [SerializeField] private bool wrapAround = true;

        [Tooltip("Whether keyboard navigation input is currently enabled")]
        [SerializeField] private bool enableKeyboardInput = true;

        [Tooltip("Default index to select when a new list of items is provided")]
        [SerializeField] private int defaultSelectedIndex = 0;

        [Header("Grid Layout (Optional)")]
        [Tooltip("Number of columns for 2D grid navigation (1 for vertical lists, 2 for 2x2 grid)")]
        [SerializeField] private int gridColumns = 1;

        public int GridColumns => gridColumns;

        /// <summary>
        /// Event fired when the user presses Back (ESC / Backspace).
        /// Used by PhoneOS to return to the Home Screen or close the current app.
        /// </summary>
        public event System.Action OnBackRequested;

        /// <summary>
        /// Event fired when the active selection index changes.
        /// </summary>
        public event System.Action<int, IPhoneNavigable> OnSelectionChanged;

        private readonly List<IPhoneNavigable> activeItems = new List<IPhoneNavigable>();
        private int selectedIndex = -1;
        private bool isNavigationActive = false;

        public int SelectedIndex => selectedIndex;
        public IPhoneNavigable SelectedItem => (selectedIndex >= 0 && selectedIndex < activeItems.Count) ? activeItems[selectedIndex] : null;
        public int ItemCount => activeItems.Count;

        private void OnEnable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnOrderSelectionStarted += HandleOrderSelectionStarted;
                GameManager.Instance.OnDeliveryStarted += HandleDeliveryStarted;
            }
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnOrderSelectionStarted -= HandleOrderSelectionStarted;
                GameManager.Instance.OnOrderSelectionStarted += HandleOrderSelectionStarted;

                GameManager.Instance.OnDeliveryStarted -= HandleDeliveryStarted;
                GameManager.Instance.OnDeliveryStarted += HandleDeliveryStarted;
            }

            if (GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.OrderSelection)
            {
                SetNavigationActive(true);
            }
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnOrderSelectionStarted -= HandleOrderSelectionStarted;
                GameManager.Instance.OnDeliveryStarted -= HandleDeliveryStarted;
            }

            UnsubscribeFromItems();
        }

        private void Update()
        {
            if (!enableKeyboardInput || !isNavigationActive || activeItems.Count == 0) return;

            HandleInput();
        }

        private void HandleInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool rightPressed = keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame;
            bool leftPressed = keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame;
            bool downPressed = keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame;
            bool upPressed = keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame;

            // Confirm selection: Enter / Space
            bool confirmPressed = keyboard.enterKey.wasPressedThisFrame ||
                                  keyboard.numpadEnterKey.wasPressedThisFrame ||
                                  keyboard.spaceKey.wasPressedThisFrame;

            // Back / Exit app: ESC / Backspace
            bool backPressed = keyboard.escapeKey.wasPressedThisFrame ||
                               keyboard.backspaceKey.wasPressedThisFrame;

            if (rightPressed)
            {
                Navigate(1);
            }
            else if (leftPressed)
            {
                Navigate(-1);
            }
            else if (downPressed)
            {
                Navigate(gridColumns);
            }
            else if (upPressed)
            {
                Navigate(-gridColumns);
            }
            else if (confirmPressed)
            {
                ConfirmSelection();
            }
            else if (backPressed)
            {
                OnBackRequested?.Invoke();
            }
        }

        /// <summary>
        /// Registers a new collection of navigable elements for the active screen/app.
        /// Can be called with order cards, app icons, shop items, or any IPhoneNavigable list.
        /// </summary>
        public void SetNavigableItems<T>(IReadOnlyList<T> items, int initialIndex = 0, int columns = 1) where T : IPhoneNavigable
        {
            gridColumns = Mathf.Max(1, columns);
            UnsubscribeFromItems();
            activeItems.Clear();
            selectedIndex = -1;

            if (items != null && items.Count > 0)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] != null)
                    {
                        activeItems.Add(items[i]);
                        items[i].OnHovered += HandleItemHovered;
                    }
                }

                int targetIndex = Mathf.Clamp(initialIndex >= 0 ? initialIndex : defaultSelectedIndex, 0, activeItems.Count - 1);
                SetSelectedIndex(targetIndex);
            }
        }

        /// <summary>
        /// Clears all currently registered items.
        /// </summary>
        public void ClearNavigableItems()
        {
            UnsubscribeFromItems();
            activeItems.Clear();
            selectedIndex = -1;
        }

        /// <summary>
        /// Moves selection forward or backward by the specified step offset.
        /// </summary>
        public void Navigate(int step)
        {
            if (activeItems.Count == 0) return;

            int count = activeItems.Count;
            int newIndex;

            if (selectedIndex < 0)
            {
                newIndex = 0;
            }
            else
            {
                newIndex = selectedIndex + step;
                if (wrapAround)
                {
                    newIndex = (newIndex % count + count) % count;
                }
                else
                {
                    newIndex = Mathf.Clamp(newIndex, 0, count - 1);
                }
            }

            SetSelectedIndex(newIndex);
        }

        /// <summary>
        /// Focuses a specific item by index and unfocuses the previous one.
        /// </summary>
        public void SetSelectedIndex(int index)
        {
            if (activeItems.Count == 0) return;
            if (index < 0 || index >= activeItems.Count) return;

            if (selectedIndex == index)
            {
                activeItems[selectedIndex]?.SetFocused(true);
                return;
            }

            // Unfocus previous
            if (selectedIndex >= 0 && selectedIndex < activeItems.Count && activeItems[selectedIndex] != null)
            {
                activeItems[selectedIndex].SetFocused(false);
            }

            selectedIndex = index;

            // Focus new
            if (activeItems[selectedIndex] != null)
            {
                activeItems[selectedIndex].SetFocused(true);
                OnSelectionChanged?.Invoke(selectedIndex, activeItems[selectedIndex]);
            }
        }

        /// <summary>
        /// Executes the primary action of the currently focused item.
        /// </summary>
        public void ConfirmSelection()
        {
            if (selectedIndex >= 0 && selectedIndex < activeItems.Count && activeItems[selectedIndex] != null)
            {
                Debug.Log($"<color=cyan>[PhoneInputNavigator]</color> Executing Select() on item index: {selectedIndex}");
                activeItems[selectedIndex].Select();
            }
        }

        /// <summary>
        /// Enables or disables navigation input handling.
        /// </summary>
        public void SetNavigationActive(bool active)
        {
            isNavigationActive = active;

            if (active && activeItems.Count > 0 && selectedIndex < 0)
            {
                SetSelectedIndex(Mathf.Clamp(defaultSelectedIndex, 0, activeItems.Count - 1));
            }
            else if (!active && selectedIndex >= 0 && selectedIndex < activeItems.Count)
            {
                activeItems[selectedIndex]?.SetFocused(false);
                selectedIndex = -1;
            }
        }

        private void HandleItemHovered(IPhoneNavigable item)
        {
            int index = activeItems.IndexOf(item);
            if (index >= 0)
            {
                isNavigationActive = true;
                SetSelectedIndex(index);
            }
        }

        private void UnsubscribeFromItems()
        {
            for (int i = 0; i < activeItems.Count; i++)
            {
                if (activeItems[i] != null)
                {
                    activeItems[i].OnHovered -= HandleItemHovered;
                }
            }
        }

        private void HandleOrderSelectionStarted()
        {
            SetNavigationActive(true);
        }

        private void HandleDeliveryStarted()
        {
            SetNavigationActive(false);
        }
    }
}

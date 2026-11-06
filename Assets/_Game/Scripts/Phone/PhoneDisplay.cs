using System.Collections;
using UnityEngine;

namespace DoomDriver.Phone
{
    /// <summary>
    /// Controls the physical screen material on the phone 3D mesh.
    /// Strictly adheres to Single Responsibility Principle (SRP) and Separation of Concerns (SoC)
    /// by subscribing directly to GameManager events without tight coupling to PhoneSystem.
    /// </summary>
    public class PhoneDisplay : MonoBehaviour
    {
        [Header("Mesh Renderer")]
        [Tooltip("MeshRenderer of the phone model (e.g. phone512)")]
        [SerializeField] private MeshRenderer phoneRenderer;
        [Tooltip("Submesh index of the phone screen (usually 1)")]
        [SerializeField] private int screenMaterialIndex = 1;

        [Header("Materials")]
        [Tooltip("Material used for Reels video player (e.g. screen512)")]
        [SerializeField] private Material reelsMaterial;

        [Tooltip("Material used in lobby / courier app mode (e.g. Black.mat)")]
        [SerializeField] private Material backgroundMaterial;

        [Header("Transition Settings")]
        [Tooltip("Delay in seconds before switching to reels material on delivery start (allows hand to lower smoothly with black screen)")]
        [SerializeField] private float deliveryStartSwitchDelay = 0.35f;

        private Coroutine switchRoutine;

        private void Awake()
        {
            if (phoneRenderer == null)
            {
                phoneRenderer = GetComponent<MeshRenderer>() ?? GetComponentInChildren<MeshRenderer>();
            }
        }

        private void OnEnable()
        {
            SubscribeToGameManager();
        }

        private void Start()
        {
            // Guaranteed subscription on first run (in case GameManager was not yet initialized during OnEnable)
            SubscribeToGameManager();

            // Set initial screen material based on current game state
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.OrderSelection)
            {
                ShowBlackScreen();
            }
            else
            {
                ShowReels();
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromGameManager();
            if (switchRoutine != null)
            {
                StopCoroutine(switchRoutine);
                switchRoutine = null;
            }
        }

        private void SubscribeToGameManager()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnOrderSelectionStarted -= HandleOrderSelectionStarted;
                GameManager.Instance.OnOrderSelectionStarted += HandleOrderSelectionStarted;

                GameManager.Instance.OnDeliveryStarted -= HandleDeliveryStarted;
                GameManager.Instance.OnDeliveryStarted += HandleDeliveryStarted;
            }
        }

        private void UnsubscribeFromGameManager()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnOrderSelectionStarted -= HandleOrderSelectionStarted;
                GameManager.Instance.OnDeliveryStarted -= HandleDeliveryStarted;
            }
        }

        private void HandleOrderSelectionStarted()
        {
            if (switchRoutine != null)
            {
                StopCoroutine(switchRoutine);
                switchRoutine = null;
            }
            ShowBlackScreen();
        }

        private void HandleDeliveryStarted()
        {
            if (switchRoutine != null) StopCoroutine(switchRoutine);
            // Delay material switch so the hand lowers down smoothly with black screen
            // without flashing the video in front of the player's face
            switchRoutine = StartCoroutine(ShowReelsDelayed(deliveryStartSwitchDelay));
        }

        private IEnumerator ShowReelsDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            ShowReels();
            switchRoutine = null;
        }

        public void ShowReels() => SetMaterial(reelsMaterial);
        public void ShowBlackScreen() => SetMaterial(backgroundMaterial);

        private void SetMaterial(Material mat)
        {
            if (phoneRenderer == null)
            {
                Debug.LogWarning("<color=yellow>[PhoneDisplay]</color> phoneRenderer is not assigned!");
                return;
            }

            if (mat == null)
            {
                Debug.LogWarning("<color=yellow>[PhoneDisplay]</color> Target material is null!");
                return;
            }

            var mats = phoneRenderer.sharedMaterials;
            if (screenMaterialIndex >= 0 && screenMaterialIndex < mats.Length)
            {
                mats[screenMaterialIndex] = mat;
                phoneRenderer.sharedMaterials = mats;
                Debug.Log($"<color=cyan>[PhoneDisplay]</color> Screen material [{screenMaterialIndex}] set to '{mat.name}'.");
            }
            else
            {
                Debug.LogError($"<color=red>[PhoneDisplay]</color> screenMaterialIndex {screenMaterialIndex} out of bounds (length: {mats.Length}).");
            }
        }
    }
}

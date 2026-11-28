using System;
using UnityEngine;

namespace DoomDriver.Player
{
    /// <summary>
    /// Reads player vehicle steering input via Unity's New Input System (InputSystem_Actions).
    /// Adheres to SoC: owns input lifecycle and exposes MoveInput and OnMovePerformed.
    /// </summary>
    public class VehicleInputReader : MonoBehaviour
    {
        private InputSystem_Actions controls;
        private Vector2 moveInput = Vector2.zero;

        public Vector2 MoveInput => moveInput;
        public event Action<Vector2> OnMovePerformed;

        private void Awake()
        {
            controls = new InputSystem_Actions();

            controls.Player.Move.performed += ctx =>
            {
                moveInput = ctx.ReadValue<Vector2>();
                OnMovePerformed?.Invoke(moveInput);
            };

            controls.Player.Move.canceled += _ =>
            {
                moveInput = Vector2.zero;
            };
        }

        private void OnEnable()
        {
            controls?.Enable();
        }

        private void OnDisable()
        {
            controls?.Disable();
        }

        public void ResetInput()
        {
            moveInput = Vector2.zero;
        }
    }
}


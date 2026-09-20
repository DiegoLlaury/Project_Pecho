using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Traduit les actions du nouveau Input System en état d'entrée joueur.</summary>
public sealed class PlayerInputHandler : MonoBehaviour, IPlayerInput
{
    public Vector2 MoveInput { get; private set; }
    public bool IsRunning { get; private set; }

    public event Action JumpPressed;
    public event Action InteractPressed;

    private PlayerInputActions inputActions;

    private void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Player.Move.performed += HandleMovePerformed;
        inputActions.Player.Move.canceled += HandleMoveCanceled;
        inputActions.Player.Sprint.performed += HandleSprintPerformed;
        inputActions.Player.Sprint.canceled += HandleSprintCanceled;
        inputActions.Player.Jump.performed += HandleJumpPerformed;
        inputActions.Player.Interact.performed += HandleInteractPerformed;
        inputActions.Player.Enable();
    }

    private void OnDisable()
    {
        inputActions.Player.Disable();
        inputActions.Player.Move.performed -= HandleMovePerformed;
        inputActions.Player.Move.canceled -= HandleMoveCanceled;
        inputActions.Player.Sprint.performed -= HandleSprintPerformed;
        inputActions.Player.Sprint.canceled -= HandleSprintCanceled;
        inputActions.Player.Jump.performed -= HandleJumpPerformed;
        inputActions.Player.Interact.performed -= HandleInteractPerformed;
        MoveInput = Vector2.zero;
        IsRunning = false;
    }

    private void OnDestroy()
    {
        inputActions?.Dispose();
    }

    private void HandleMovePerformed(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }

    private void HandleMoveCanceled(InputAction.CallbackContext context)
    {
        MoveInput = Vector2.zero;
    }

    private void HandleSprintPerformed(InputAction.CallbackContext context)
    {
        IsRunning = true;
    }

    private void HandleSprintCanceled(InputAction.CallbackContext context)
    {
        IsRunning = false;
    }

    private void HandleJumpPerformed(InputAction.CallbackContext context)
    {
        JumpPressed?.Invoke();
    }

    private void HandleInteractPerformed(InputAction.CallbackContext context)
    {
        InteractPressed?.Invoke();
    }
}

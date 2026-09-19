using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// Single owner of PlayerInputActions. Translates raw input into IPlayerInput.
public class PlayerInputHandler : MonoBehaviour, IPlayerInput
{
    public Vector2 MoveInput { get; private set; }
    public bool IsRunning { get; private set; }

    public event Action JumpPressed;
    public event Action InteractPressed;

    private PlayerInputActions inputActions;

    void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    void OnEnable()
    {
        inputActions.Player.Enable();

        inputActions.Player.Move.performed += ctx => MoveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => MoveInput = Vector2.zero;

        inputActions.Player.Sprint.performed += ctx => IsRunning = true;
        inputActions.Player.Sprint.canceled += ctx => IsRunning = false;

        inputActions.Player.Jump.performed += ctx => JumpPressed?.Invoke();
        inputActions.Player.Interact.performed += ctx => InteractPressed?.Invoke();
    }

    void OnDisable()
    {
        inputActions.Player.Disable();
    }
}
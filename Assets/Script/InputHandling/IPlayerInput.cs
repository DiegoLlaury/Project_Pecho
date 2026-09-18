using System;
using UnityEngine;

/// <summary>
/// How to use:
///     # Reference interface:
///     private IPlayerInput input;
///     
///     # Fetch handler:
///     void Awake() 
///     {
///         input = GetComponent<PlayerInputHandler>();
///     }
///     
///     # Assign input:
///     void OnEnable() => input.ActionNamePressed += OnInteractionPressed;
///     void OnDisable() => input.InteractPressed -= OnInteractPressed;
///     
/// </summary>
public interface IPlayerInput
{
    Vector2 MoveInput { get; }
    bool IsRunning { get; }

    event Action JumpPressed;
    event Action InteractPressed;
}

using UnityEngine;
using UnityEngine.InputSystem;

public sealed class FishingInputReader : MonoBehaviour
{
    private const float MinimumInputMagnitude = 0.001f;

    public Vector2 FightInput { get; private set; }

    public bool HasFightInput => FightInput.sqrMagnitude > MinimumInputMagnitude;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            FightInput = Vector2.zero;
            return;
        }

        float horizontalInput = ReadAxis(
            keyboard.leftArrowKey.isPressed,
            keyboard.rightArrowKey.isPressed);

        float verticalInput = ReadAxis(
            keyboard.downArrowKey.isPressed,
            keyboard.upArrowKey.isPressed);

        FightInput = new Vector2(horizontalInput, verticalInput);

        if (FightInput.sqrMagnitude > 1f)
        {
            FightInput.Normalize();
        }
    }

    /// <summary>
    /// Retourne un axe compris entre -1 et 1 depuis deux états de touches.
    /// </summary>
    private static float ReadAxis(bool negativePressed, bool positivePressed)
    {
        if (negativePressed == positivePressed)
        {
            return 0f;
        }

        return positivePressed ? -1f : 1f;
    }
}

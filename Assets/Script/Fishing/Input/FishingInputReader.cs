using UnityEngine;
using UnityEngine.InputSystem;

public sealed class FishingInputReader : MonoBehaviour
{
    /// <summary>
    /// Effort de traction, de 0 à 1 (flèche haut).
    /// </summary>
    public float PullInput { get; private set; }

    /// <summary>
    /// Relâchement du fil, de 0 à 1 (flèche bas).
    /// </summary>
    public float ReleaseInput { get; private set; }

    /// <summary>
    /// Direction latérale, de -1 (gauche) à 1 (droite).
    /// </summary>
    public float LateralInput { get; private set; }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            PullInput = 0f;
            ReleaseInput = 0f;
            LateralInput = 0f;
            return;
        }

        bool up = keyboard.upArrowKey.isPressed;
        bool down = keyboard.downArrowKey.isPressed;

        PullInput = up && !down ? 0f : 1f;
        ReleaseInput = down && !up ? 0f : 1f;

        LateralInput = ReadAxis(
            keyboard.leftArrowKey.isPressed,
            keyboard.rightArrowKey.isPressed);
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

        return positivePressed ? 1f : -1f;
    }
}

/// <summary>Suit une action maintenue qui doit commencer pendant une fenêtre ATB.</summary>
public sealed class ATBContinuousBoostTracker
{
    private bool wasInputActive;
    private bool isBoostArmed;

    /// <summary>Retourne le multiplicateur courant et désarme le boost dès que la vague quitte le curseur.</summary>
    public float Evaluate(
        bool isInputActive,
        ATBTimingController timingController)
    {
        if (!isInputActive)
        {
            wasInputActive = false;
            isBoostArmed = false;
            return 1f;
        }

        if (!wasInputActive)
        {
            isBoostArmed = timingController != null && timingController.IsBoostWindowActive;
            if (isBoostArmed)
            {
                timingController.ReportContinuousActionSuccess();
            }
        }

        wasInputActive = true;

        if (isBoostArmed &&
            timingController != null &&
            timingController.IsBoostWindowActive)
        {
            return timingController.ContinuousActionMultiplier;
        }

        isBoostArmed = false;
        return 1f;
    }

    /// <summary>Réinitialise l'état de maintien.</summary>
    public void Reset()
    {
        wasInputActive = false;
        isBoostArmed = false;
    }
}

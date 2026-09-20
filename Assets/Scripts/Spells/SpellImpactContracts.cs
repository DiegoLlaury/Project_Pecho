public interface ISpendableEndurance
{
    float CurrentEndurance { get; }

    bool TrySpendEndurance(float amount);

    void RestoreEndurance(float amount);
}

public interface IEnduranceReceiver
{
    void ApplyEnduranceDamage(float amount);
}

public interface IFishingTensionReceiver
{
    void ApplyTensionSpike(float normalizedAmount);
}

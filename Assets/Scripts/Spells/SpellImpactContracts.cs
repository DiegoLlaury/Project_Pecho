public interface IEnduranceReceiver
{
    void ApplyEnduranceDamage(float amount);
}

public interface IFishingTensionReceiver
{
    void ApplyTensionSpike(float normalizedAmount);
}

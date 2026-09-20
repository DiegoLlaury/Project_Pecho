public interface ISpendableEndurance
{
    float CurrentEndurance { get; }

    /// <summary>Débite la ressource lorsque son solde couvre le coût.</summary>
    bool TrySpendEndurance(float amount);

    /// <summary>Restaure une quantité précédemment débitée.</summary>
    void RestoreEndurance(float amount);
}

public interface IEnduranceReceiver
{
    /// <summary>Applique une perte d'endurance.</summary>
    void ApplyEnduranceDamage(float amount);
}

public interface IFishingTensionReceiver
{
    /// <summary>Applique une surtension normalisée à la ligne.</summary>
    void ApplyTensionSpike(float normalizedAmount);
}

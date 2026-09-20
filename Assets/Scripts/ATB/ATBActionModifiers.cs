using UnityEngine;

/// <summary>Multiplicateurs immuables capturés au déclenchement d'une action.</summary>
public readonly struct ATBActionModifiers
{
    public static ATBActionModifiers Normal => new ATBActionModifiers(1f, 1f, false);

    public float PotencyMultiplier { get; }
    public float CooldownMultiplier { get; }
    public bool IsBoosted { get; }

    public ATBActionModifiers(
        float potencyMultiplier,
        float cooldownMultiplier,
        bool isBoosted)
    {
        PotencyMultiplier = Mathf.Max(0f, potencyMultiplier);
        CooldownMultiplier = Mathf.Max(0f, cooldownMultiplier);
        IsBoosted = isBoosted;
    }
}

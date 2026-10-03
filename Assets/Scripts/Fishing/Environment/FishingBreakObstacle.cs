using UnityEngine;

/// <summary>Identifie une racine de décor exploitable par ses colliders solides enfants.</summary>
[DisallowMultipleComponent]
public sealed class FishingBreakObstacle : MonoBehaviour
{
    private const float DefaultMinimumImpactSpeed = 2.5f;
    private const float DefaultEnduranceCostRatio = 0.08f;
    private const float DefaultCooldown = 4f;
    private const float DefaultKnockbackSpeed = 2f;
    private const float MinimumValue = 0f;
    private const float MaximumEnduranceCostRatio = 1f;

    [Tooltip("Vitesse entrante minimale, projetée sur la normale du contact, en m/s.")]
    [SerializeField, Min(MinimumValue)] private float minimumImpactSpeed = DefaultMinimumImpactSpeed;
    [Tooltip("Part de l'endurance maximale consommée par un impact qualifié par la session.")]
    [SerializeField, Range(MinimumValue, MaximumEnduranceCostRatio)]
    private float enduranceCostRatio = DefaultEnduranceCostRatio;
    [Tooltip("Délai minimal entre deux breaks sur cette racine, après séparation complète.")]
    [SerializeField, Min(MinimumValue)] private float cooldown = DefaultCooldown;
    [Tooltip("Vitesse de recul légère le long de la normale, appliquée par la session.")]
    [SerializeField, Min(MinimumValue)] private float knockbackSpeed = DefaultKnockbackSpeed;

    /// <summary>Vitesse normale minimale requise pour qualifier un impact, en m/s.</summary>
    public float MinimumImpactSpeed => SanitizeNonNegative(minimumImpactSpeed, DefaultMinimumImpactSpeed);

    /// <summary>Coût unique d'un impact accepté, en fraction de l'endurance maximale.</summary>
    public float EnduranceCostRatio => Mathf.Min(
        MaximumEnduranceCostRatio,
        SanitizeNonNegative(enduranceCostRatio, DefaultEnduranceCostRatio));

    /// <summary>Cooldown de la racine, dont le suivi appartient exclusivement à la session.</summary>
    public float Cooldown => SanitizeNonNegative(cooldown, DefaultCooldown);

    /// <summary>Vitesse de recul demandée à la session, sans force ni débit autonome.</summary>
    public float KnockbackSpeed => SanitizeNonNegative(knockbackSpeed, DefaultKnockbackSpeed);

    private void OnValidate()
    {
        minimumImpactSpeed = MinimumImpactSpeed;
        enduranceCostRatio = EnduranceCostRatio;
        cooldown = Cooldown;
        knockbackSpeed = KnockbackSpeed;
    }

    private static float SanitizeNonNegative(float value, float fallback)
    {
        return float.IsNaN(value) || float.IsInfinity(value)
            ? fallback
            : Mathf.Max(MinimumValue, value);
    }
}

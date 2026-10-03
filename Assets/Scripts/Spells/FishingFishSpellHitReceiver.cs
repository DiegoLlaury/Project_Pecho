using UnityEngine;

public sealed class FishingFishSpellHitReceiver : MonoBehaviour, IEnduranceReceiver
{
    private const float MinimumElementalBreakPower = 0f;

    [SerializeField] private FishingSessionController fishingSessionController;

    /// <summary>Transmet les dégâts d'endurance des sorts du joueur au poisson de la session.</summary>
    public void ApplyEnduranceDamage(float amount)
    {
        if (fishingSessionController != null)
        {
            fishingSessionController.ApplyFishEnduranceDamage(amount);
        }
    }

    /// <summary>Transmet uniquement la puissance élémentaire déjà modifiée par le timing ATB.</summary>
    public void ApplyElementalImpact(FishElementWeakness element, float breakPower)
    {
        if (fishingSessionController == null || element == FishElementWeakness.None ||
            breakPower <= MinimumElementalBreakPower || float.IsNaN(breakPower) || float.IsInfinity(breakPower))
        {
            return;
        }

        fishingSessionController.ApplyElementalBreak(element, breakPower);
    }

    /// <summary>Transmet l'attraction temporaire du tourbillon à la session.</summary>
    public void ApplyWhirlwind(float duration, float pullAcceleration)
    {
        if (fishingSessionController != null)
        {
            fishingSessionController.ApplyWhirlwind(duration, pullAcceleration);
        }
    }
}

using UnityEngine;

public sealed class FishingFishSpellHitReceiver : MonoBehaviour, IEnduranceReceiver
{
    [SerializeField] private FishingSessionController fishingSessionController;

    /// <summary>Transmet les dégâts d'endurance des sorts du joueur au poisson de la session.</summary>
    public void ApplyEnduranceDamage(float amount)
    {
        if (fishingSessionController != null)
        {
            fishingSessionController.ApplyFishEnduranceDamage(amount);
        }
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

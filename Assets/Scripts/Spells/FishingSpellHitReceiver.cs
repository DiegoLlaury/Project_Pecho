using UnityEngine;

public sealed class FishingSpellHitReceiver : MonoBehaviour, IEnduranceReceiver, IFishingTensionReceiver
{
    [SerializeField] private CombatEndurance combatEndurance;
    [SerializeField] private FishingSessionController fishingSessionController;

    /// <summary>Transmet les dégâts d'endurance à la ressource de combat du joueur.</summary>
    public void ApplyEnduranceDamage(float amount)
    {
        if (combatEndurance != null)
        {
            combatEndurance.ApplyEnduranceDamage(amount);
        }
    }

    /// <summary>Transmet la surtension magique à la session de pêche active.</summary>
    public void ApplyTensionSpike(float normalizedAmount)
    {
        if (fishingSessionController != null)
        {
            fishingSessionController.ApplyTensionSpike(normalizedAmount);
        }
    }
}

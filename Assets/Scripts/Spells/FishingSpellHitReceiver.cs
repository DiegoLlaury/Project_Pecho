using UnityEngine;

public sealed class FishingSpellHitReceiver : MonoBehaviour, IEnduranceReceiver, IFishingTensionReceiver
{
    [SerializeField] private CombatEndurance combatEndurance;
    [SerializeField] private FishingSessionController fishingSessionController;

    private void OnEnable()
    {
        if (combatEndurance != null)
        {
            combatEndurance.Depleted += HandleEnduranceDepleted;
        }

        if (fishingSessionController != null)
        {
            fishingSessionController.StateChanged += HandleSessionStateChanged;
        }
    }

    private void OnDisable()
    {
        if (combatEndurance != null)
        {
            combatEndurance.Depleted -= HandleEnduranceDepleted;
        }

        if (fishingSessionController != null)
        {
            fishingSessionController.StateChanged -= HandleSessionStateChanged;
        }
    }

    /// <summary>Transmet les dégâts d'endurance à la ressource de combat du joueur.</summary>
    public void ApplyEnduranceDamage(float amount)
    {
        combatEndurance?.ApplyEnduranceDamage(amount);
    }

    /// <summary>Transmet la surtension magique à la session de pêche active.</summary>
    public void ApplyTensionSpike(float normalizedAmount)
    {
        fishingSessionController?.ApplyTensionSpike(normalizedAmount);
    }

    private void HandleEnduranceDepleted()
    {
        fishingSessionController?.EndFromPlayerExhaustion();
    }

    private void HandleSessionStateChanged(FishingState state)
    {
        if (state == FishingState.Active)
        {
            combatEndurance?.ResetEndurance();
        }
    }
}

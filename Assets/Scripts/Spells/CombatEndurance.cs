using System;
using UnityEngine;

public sealed class CombatEndurance : MonoBehaviour, IEnduranceReceiver
{
    private const float MinimumMaximumEndurance = 1f;

    [SerializeField, Min(MinimumMaximumEndurance)] private float maximumEndurance = 100f;
    [SerializeField] private bool resetOnEnable = true;

    public float CurrentEndurance { get; private set; }
    public float NormalizedEndurance => CurrentEndurance / Mathf.Max(MinimumMaximumEndurance, maximumEndurance);

    public event Action Depleted;

    private void OnEnable()
    {
        if (resetOnEnable)
        {
            ResetEndurance();
        }
    }

    /// <summary>Retire de l'endurance sans descendre sous zéro.</summary>
    public void ApplyEnduranceDamage(float amount)
    {
        float previousEndurance = CurrentEndurance;
        CurrentEndurance = Mathf.Max(0f, CurrentEndurance - Mathf.Max(0f, amount));

        if (previousEndurance > 0f && CurrentEndurance <= 0f)
        {
            Depleted?.Invoke();
        }
    }

    /// <summary>Restaure l'endurance à sa valeur maximale.</summary>
    public void ResetEndurance()
    {
        CurrentEndurance = Mathf.Max(MinimumMaximumEndurance, maximumEndurance);
    }
}

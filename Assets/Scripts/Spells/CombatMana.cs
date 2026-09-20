using UnityEngine;

public sealed class CombatMana : MonoBehaviour
{
    private const float MinimumMaximumMana = 1f;

    [SerializeField, Min(MinimumMaximumMana)] private float maximumMana = 100f;
    [SerializeField, Min(0f)] private float regenerationPerSecond = 8f;
    [SerializeField] private bool resetOnEnable = true;

    public float CurrentMana { get; private set; }
    public float NormalizedMana => CurrentMana / Mathf.Max(MinimumMaximumMana, maximumMana);

    private void OnEnable()
    {
        if (resetOnEnable)
        {
            ResetMana();
        }
    }

    private void Update()
    {
        CurrentMana = Mathf.Min(maximumMana, CurrentMana + regenerationPerSecond * Time.deltaTime);
    }

    /// <summary>Retourne vrai et débite le mana lorsque le solde couvre le coût.</summary>
    public bool TrySpend(float amount)
    {
        float cost = Mathf.Max(0f, amount);
        if (cost > CurrentMana)
        {
            return false;
        }

        CurrentMana -= cost;
        return true;
    }

    /// <summary>Indique si le coût demandé peut être payé.</summary>
    public bool CanSpend(float amount)
    {
        return Mathf.Max(0f, amount) <= CurrentMana;
    }

    /// <summary>Restaure du mana sans dépasser la capacité maximale.</summary>
    public void Restore(float amount)
    {
        CurrentMana = Mathf.Min(
            Mathf.Max(MinimumMaximumMana, maximumMana),
            CurrentMana + Mathf.Max(0f, amount));
    }

    /// <summary>Restaure entièrement la réserve de mana.</summary>
    public void ResetMana()
    {
        CurrentMana = Mathf.Max(MinimumMaximumMana, maximumMana);
    }
}

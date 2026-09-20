using System;
using UnityEngine;

public sealed class FishIdentificationState : MonoBehaviour
{
    [SerializeField] private FishIdentityDefinition identity;

    public FishIdentityDefinition Identity => identity;
    public bool IsRevealed { get; private set; }
    public event Action Revealed;

    /// <summary>Révèle définitivement les informations du poisson pour la session courante.</summary>
    public void Reveal()
    {
        if (IsRevealed)
        {
            return;
        }

        IsRevealed = true;
        Revealed?.Invoke();
    }
}

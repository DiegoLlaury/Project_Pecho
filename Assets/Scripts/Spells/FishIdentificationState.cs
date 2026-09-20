using System;
using UnityEngine;

public sealed class FishIdentificationState : MonoBehaviour
{
    [SerializeField] private FishIdentityDefinition identity;

    public FishIdentityDefinition Identity => identity;
    public bool IsRevealed { get; private set; }
    public event Action Revealed;
    public event Action IdentificationChanged;

    /// <summary>Révèle définitivement les informations du poisson pour la session courante.</summary>
    public void Reveal()
    {
        if (IsRevealed)
        {
            return;
        }

        IsRevealed = true;
        Revealed?.Invoke();
        IdentificationChanged?.Invoke();
    }

    /// <summary>Masque les informations révélées pour une nouvelle session.</summary>
    public void ResetIdentification()
    {
        if (!IsRevealed)
        {
            return;
        }

        IsRevealed = false;
        IdentificationChanged?.Invoke();
    }
}

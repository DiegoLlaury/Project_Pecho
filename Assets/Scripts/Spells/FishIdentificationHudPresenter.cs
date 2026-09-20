using TMPro;
using UnityEngine;

public sealed class FishIdentificationHudPresenter : MonoBehaviour
{
    [SerializeField] private FishIdentificationState identificationState;
    [SerializeField] private TMP_Text informationText;

    private void Awake()
    {
        Refresh();
    }

    private void OnEnable()
    {
        if (identificationState != null)
        {
            identificationState.Revealed += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (identificationState != null)
        {
            identificationState.Revealed -= Refresh;
        }
    }

    /// <summary>Actualise les informations révélées au-dessus de l'endurance.</summary>
    public void Refresh()
    {
        if (informationText == null)
        {
            return;
        }

        FishIdentityDefinition identity = identificationState != null
            ? identificationState.Identity
            : null;
        bool visible = identificationState != null && identificationState.IsRevealed && identity != null;
        informationText.gameObject.SetActive(visible);

        if (visible)
        {
            informationText.text =
                $"{identity.displayName}\n" +
                $"Faiblesse : {GetWeaknessLabel(identity.elementalWeakness)}\n" +
                $"Nature : {GetNatureLabel(identity.nature)}";
        }
    }

    private static string GetWeaknessLabel(FishElementWeakness weakness)
    {
        return weakness switch
        {
            FishElementWeakness.Fire => "Feu",
            FishElementWeakness.Lightning => "Éclair",
            FishElementWeakness.Water => "Eau",
            FishElementWeakness.Wind => "Vent",
            _ => "Aucune"
        };
    }

    private static string GetNatureLabel(FishNature nature)
    {
        return nature switch
        {
            FishNature.Aggressive => "Agressif",
            FishNature.Cunning => "Rusé",
            FishNature.Resilient => "Résistant",
            _ => "Calme"
        };
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Présente les ressources de la session et les actions du poisson sans modifier le combat ni l'ATB.
/// </summary>
public sealed class FishingCombatHudPresenter : MonoBehaviour
{
    private const string ProgressPropertyName = "_Progress";
    private const string RuntimeMaterialSuffix = " (Runtime)";
    private const string StruggleLabel = "Résiste";
    private const string DodgeLabel = "Dérobade";
    private const string WindupLabel = "Prépare une ruée";
    private const string BurstLabel = "Ruée";
    private const string CastingLabel = "Incantation";
    private const string RecoveryLabel = "Récupération";
    private const string ShoreReboundLabel = "Rebond de berge";
    private const string ElementalBreakLabel = "Break élémentaire";
    private const string ShoreBreakLabel = "Break berge";
    private const string EnvironmentBreakLabel = "Break décor";
    private const string UnknownBreakLabel = "Break";
    private const string CatchableLabel = "Capturable";
    private const string ExhaustedLabel = "Épuisé";
    private const string CaughtLabel = "Capturé";
    private const string EscapedLabel = "Échappé";
    private static readonly int ProgressPropertyId = Shader.PropertyToID(ProgressPropertyName);

    [Header("Required References")]
    [SerializeField] private FishingSessionController fishingSessionController;
    [SerializeField] private Image tensionProgressBar;
    [SerializeField] private Image enduranceProgressBar;

    [Header("Optional Fish Combat UI")]
    [Tooltip("Jauge de faiblesse élémentaire accumulée, de zéro à un.")]
    [SerializeField] private Image elementalBreakProgressBar;
    [Tooltip("Durée restante du break du poisson, de un à zéro ; distincte du risque de rupture du fil.")]
    [SerializeField] private Image fishBreakProgressBar;
    [SerializeField] private TMP_Text fishActionLabel;

    private Material tensionRuntimeMaterial;
    private Material enduranceRuntimeMaterial;
    private Material elementalBreakRuntimeMaterial;
    private Material fishBreakRuntimeMaterial;
    private Material tensionOriginalMaterial;
    private Material enduranceOriginalMaterial;
    private Material elementalBreakOriginalMaterial;
    private Material fishBreakOriginalMaterial;
    private string currentActionLabel;

    private void Awake()
    {
        ResolveFishingSessionController();
        CreateRuntimeMaterials();
        RefreshPresentation();
    }

    private void Start()
    {
        // Un seul nouvel essai après les Awake ; aucune recherche globale par frame.
        ResolveFishingSessionController();
        RefreshPresentation();
    }

    private void LateUpdate()
    {
        RefreshPresentation();
    }

    /// <summary>
    /// Rafraîchit tension, endurance, jauges optionnelles et libellé sans recréer les matériaux.
    /// La progression de break représente sa durée restante, pas la rupture de la ligne.
    /// </summary>
    public void RefreshPresentation()
    {
        if (fishingSessionController == null)
        {
            SetProgress(elementalBreakProgressBar, elementalBreakRuntimeMaterial, 0f);
            SetProgress(fishBreakProgressBar, fishBreakRuntimeMaterial, 0f);
            SetActionLabel(string.Empty);
            return;
        }

        SetProgress(tensionProgressBar, tensionRuntimeMaterial, fishingSessionController.NormalizedTension);
        SetProgress(enduranceProgressBar, enduranceRuntimeMaterial, fishingSessionController.NormalizedEndurance);
        SetProgress(elementalBreakProgressBar, elementalBreakRuntimeMaterial,
            fishingSessionController.NormalizedElementalBreak);
        SetProgress(fishBreakProgressBar, fishBreakRuntimeMaterial,
            fishingSessionController.NormalizedFishBreakRemaining);
        SetActionLabel(GetActionLabel());
    }

    private void CreateRuntimeMaterials()
    {
        tensionRuntimeMaterial = CreateRuntimeMaterial(tensionProgressBar, out tensionOriginalMaterial);
        enduranceRuntimeMaterial = CreateRuntimeMaterial(enduranceProgressBar, out enduranceOriginalMaterial);
        elementalBreakRuntimeMaterial = CreateRuntimeMaterial(elementalBreakProgressBar, out elementalBreakOriginalMaterial);
        fishBreakRuntimeMaterial = CreateRuntimeMaterial(fishBreakProgressBar, out fishBreakOriginalMaterial);
    }

    private static Material CreateRuntimeMaterial(Image progressBar, out Material originalMaterial)
    {
        originalMaterial = progressBar != null ? progressBar.material : null;
        if (originalMaterial == null || !originalMaterial.HasProperty(ProgressPropertyId))
        {
            return null;
        }

        Material runtimeMaterial = new Material(originalMaterial)
        {
            name = originalMaterial.name + RuntimeMaterialSuffix
        };
        progressBar.material = runtimeMaterial;
        return runtimeMaterial;
    }

    private static void SetProgress(Image progressBar, Material progressMaterial, float normalizedProgress)
    {
        if (progressBar == null)
        {
            return;
        }

        float progress = Mathf.Clamp01(normalizedProgress);
        if (progressMaterial != null)
        {
            progressMaterial.SetFloat(ProgressPropertyId, progress);
        }
        else
        {
            // Une Image Filled standard convient aussi, sans matériau spécifique ni warning.
            progressBar.fillAmount = progress;
        }
    }

    private string GetActionLabel()
    {
        if (fishingSessionController.State == FishingState.Caught)
        {
            return CaughtLabel;
        }
        if (fishingSessionController.State == FishingState.Escaped)
        {
            return EscapedLabel;
        }

        FishCombatState state = fishingSessionController.CurrentFishState;
        if (state == FishCombatState.Break)
        {
            switch (fishingSessionController.LastFishBreakCause)
            {
                case FishBreakCause.Elemental: return ElementalBreakLabel;
                case FishBreakCause.Shore: return ShoreBreakLabel;
                case FishBreakCause.Environment: return EnvironmentBreakLabel;
                default: return UnknownBreakLabel;
            }
        }
        if (state == FishCombatState.ShoreRebound)
        {
            return ShoreReboundLabel;
        }
        if (fishingSessionController.CanCatchFish)
        {
            return CatchableLabel;
        }

        switch (state)
        {
            case FishCombatState.LateralDodge: return DodgeLabel;
            case FishCombatState.BurstWindup: return WindupLabel;
            case FishCombatState.Burst: return BurstLabel;
            case FishCombatState.CastSpell: return CastingLabel;
            case FishCombatState.Recovery: return RecoveryLabel;
            case FishCombatState.Exhausted: return ExhaustedLabel;
            default: return StruggleLabel;
        }
    }

    private void SetActionLabel(string label)
    {
        if (fishActionLabel == null || (currentActionLabel == label && fishActionLabel.text == label))
        {
            return;
        }

        currentActionLabel = label;
        fishActionLabel.text = label;
    }

    private void ResolveFishingSessionController()
    {
        if (fishingSessionController == null)
        {
            fishingSessionController = FindFirstObjectByType<FishingSessionController>();
        }
    }

    private void OnDestroy()
    {
        DestroyRuntimeMaterial(tensionProgressBar, tensionRuntimeMaterial, tensionOriginalMaterial);
        DestroyRuntimeMaterial(enduranceProgressBar, enduranceRuntimeMaterial, enduranceOriginalMaterial);
        DestroyRuntimeMaterial(elementalBreakProgressBar, elementalBreakRuntimeMaterial, elementalBreakOriginalMaterial);
        DestroyRuntimeMaterial(fishBreakProgressBar, fishBreakRuntimeMaterial, fishBreakOriginalMaterial);
    }

    private static void DestroyRuntimeMaterial(Image progressBar, Material runtimeMaterial, Material originalMaterial)
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (progressBar != null && progressBar.material == runtimeMaterial)
        {
            progressBar.material = originalMaterial;
        }
        Destroy(runtimeMaterial);
    }
}

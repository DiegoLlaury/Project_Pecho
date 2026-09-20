using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Updates the fishing combat HUD using the active fishing session values.
/// </summary>
public sealed class FishingCombatHudPresenter : MonoBehaviour
{
    private const string ProgressPropertyName = "_Progress";
    private const string RuntimeMaterialSuffix = " (Runtime)";

    [Header("Required References")]
    [SerializeField] private FishingSessionController fishingSessionController;
    [SerializeField] private Image tensionProgressBar;
    [SerializeField] private Image enduranceProgressBar;

    private Material tensionRuntimeMaterial;
    private Material enduranceRuntimeMaterial;

    private void Awake()
    {
        ResolveFishingSessionController();
        CreateRuntimeMaterials();
        RefreshPresentation();
    }

    private void LateUpdate()
    {
        RefreshPresentation();
    }

    /// <summary>
    /// Immediately updates both material-based progress bars.
    /// </summary>
    public void RefreshPresentation()
    {
        ResolveFishingSessionController();

        if (fishingSessionController == null)
        {
            return;
        }

        SetProgress(
            tensionRuntimeMaterial,
            fishingSessionController.NormalizedTension);

        SetProgress(
            enduranceRuntimeMaterial,
            fishingSessionController.NormalizedEndurance);
    }

    private void CreateRuntimeMaterials()
    {
        tensionRuntimeMaterial = CreateRuntimeMaterial(tensionProgressBar);
        enduranceRuntimeMaterial = CreateRuntimeMaterial(enduranceProgressBar);
    }

    private static Material CreateRuntimeMaterial(Image progressBar)
    {
        if (progressBar == null || progressBar.material == null)
        {
            return null;
        }

        Material runtimeMaterial = new Material(progressBar.material)
        {
            name = progressBar.material.name + RuntimeMaterialSuffix
        };

        progressBar.material = runtimeMaterial;
        return runtimeMaterial;
    }

    private static void SetProgress(
        Material progressMaterial,
        float normalizedProgress)
    {
        if (progressMaterial == null)
        {
            return;
        }

        progressMaterial.SetFloat(
            ProgressPropertyName,
            Mathf.Clamp01(normalizedProgress));
    }

    private void ResolveFishingSessionController()
    {
        if (fishingSessionController == null)
        {
            fishingSessionController =
                FindFirstObjectByType<FishingSessionController>();
        }
    }

    private void OnDestroy()
    {
        DestroyRuntimeMaterial(tensionRuntimeMaterial);
        DestroyRuntimeMaterial(enduranceRuntimeMaterial);
    }

    private static void DestroyRuntimeMaterial(Material runtimeMaterial)
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }
}

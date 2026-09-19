using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public sealed class FishingLinePresenter : MonoBehaviour
{
    private const int StartPointIndex = 0;
    private const int EndPointIndex = 1;
    private const int LinePointCount = 2;
    private const float MinimumLineWidth = 0.01f;
    private const float MaximumLineWidth = 0.08f;

    [Header("References")]
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Line Appearance")]
    [SerializeField] private Color relaxedColor = Color.white;
    [SerializeField] private Color tenseColor = Color.red;
    [SerializeField] private float relaxedWidth = MinimumLineWidth;
    [SerializeField] private float tenseWidth = MaximumLineWidth;

    private Transform rodTip;
    private Transform hookedFish;
    private bool isConfigured;
    private bool hasLoggedConfigurationWarning;

    private void Reset()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    private void Awake()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        ValidateLineRenderer();
    }

    /// <summary>
    /// Configure les deux extrémités de la ligne de pêche.
    /// </summary>
    public void Configure(Transform configuredRodTip, Transform configuredHookedFish)
    {
        rodTip = configuredRodTip;
        hookedFish = configuredHookedFish;

        isConfigured = ValidateConfiguration();

        if (!isConfigured)
        {
            return;
        }

        lineRenderer.positionCount = LinePointCount;
        lineRenderer.enabled = true;
        UpdateLinePositions();
    }

    /// <summary>
    /// Met à jour l'apparence de la ligne selon une tension normalisée.
    /// </summary>
    public void SetLineTension(float normalizedTension)
    {
        if (lineRenderer == null)
        {
            return;
        }

        float clampedTension = Mathf.Clamp01(normalizedTension);

        Color lineColor = Color.Lerp(
            relaxedColor,
            tenseColor,
            clampedTension);

        float lineWidth = Mathf.Lerp(
            Mathf.Max(MinimumLineWidth, relaxedWidth),
            Mathf.Max(MinimumLineWidth, tenseWidth),
            clampedTension);

        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
    }

    private void LateUpdate()
    {
        if (!isConfigured)
        {
            return;
        }

        UpdateLinePositions();
    }

    private bool ValidateLineRenderer()
    {
        if (lineRenderer != null)
        {
            return true;
        }

        if (!hasLoggedConfigurationWarning)
        {
            Debug.LogWarning(
                $"{nameof(FishingLinePresenter)} on '{name}' requires a " +
                $"{nameof(LineRenderer)} component.",
                this);

            hasLoggedConfigurationWarning = true;
        }

        enabled = false;
        return false;
    }

    private bool ValidateConfiguration()
    {
        bool hasValidConfiguration =
            lineRenderer != null &&
            rodTip != null &&
            hookedFish != null;

        if (hasValidConfiguration)
        {
            return true;
        }

        if (!hasLoggedConfigurationWarning)
        {
            Debug.LogWarning(
                $"{nameof(FishingLinePresenter)} on '{name}' requires a " +
                "LineRenderer, RodTip, and HookedFish.",
                this);

            hasLoggedConfigurationWarning = true;
        }

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }

        return false;
    }

    private void UpdateLinePositions()
    {
        lineRenderer.SetPosition(StartPointIndex, rodTip.position);
        lineRenderer.SetPosition(EndPointIndex, hookedFish.position);
    }

    private void OnValidate()
    {
        relaxedWidth = Mathf.Max(MinimumLineWidth, relaxedWidth);
        tenseWidth = Mathf.Max(MinimumLineWidth, tenseWidth);
    }
}

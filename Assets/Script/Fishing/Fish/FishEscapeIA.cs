using UnityEngine;

public sealed class FishEscapeAI : MonoBehaviour
{
    private const float MinimumDirectionMagnitude = 0.001f;
    private const float DefaultVariationFrequency = 0.8f;
    private const float DefaultVariationStrength = 0.35f;
    private const float PhaseMultiplier = 0.61803398875f;

    [SerializeField] private float variationFrequency = DefaultVariationFrequency;
    [SerializeField, Range(0f, 1f)] private float variationStrength = DefaultVariationStrength;
    [SerializeField] private int behaviorSeed;

    private float phaseOffset;

    private void Awake()
    {
        phaseOffset = behaviorSeed * PhaseMultiplier;
    }

    /// <summary>
    /// Retourne une direction horizontale normalisée qui éloigne le poisson du pêcheur.
    /// </summary>
    public Vector3 GetEscapeDirection(Vector3 anglerPosition)
    {
        Vector3 directionAwayFromAngler = transform.position - anglerPosition;
        directionAwayFromAngler.y = 0f;

        if (directionAwayFromAngler.sqrMagnitude < MinimumDirectionMagnitude)
        {
            directionAwayFromAngler = transform.forward;
            directionAwayFromAngler.y = 0f;
        }

        directionAwayFromAngler.Normalize();

        Vector3 lateralDirection =
            Vector3.Cross(Vector3.up, directionAwayFromAngler).normalized;

        float variation = Mathf.Sin(
            Time.time * variationFrequency + phaseOffset) * variationStrength;

        Vector3 escapeDirection =
            directionAwayFromAngler + lateralDirection * variation;

        return escapeDirection.normalized;
    }

    private void OnValidate()
    {
        variationFrequency = Mathf.Max(0f, variationFrequency);
        variationStrength = Mathf.Clamp01(variationStrength);
    }
}

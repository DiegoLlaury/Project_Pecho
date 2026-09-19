using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Décide où le poisson veut nager. Trois comportements additionnés (steering) :
/// 1. Fuir la berge : cap constant vers le large, le plus loin possible de la berge.
/// 2. Errer : dérive latérale douce et non périodique (bruit de Perlin).
/// 3. Éviter les bords gauche/droit : poussée douce vers le centre, jamais totale.
/// Gère aussi les ruées (voir Tick).
/// </summary>
public sealed class FishEscapeAI : MonoBehaviour
{
    private const float MinimumDirectionMagnitude = 0.001f;
    private const float MinimumExtent = 0.01f;
    private const float NoiseSeedStride = 17.31f;
    private const float NoiseSeedShift = 0.37f;

    [Header("Wander")]
    [FormerlySerializedAs("variationFrequency")]
    [SerializeField, Min(0f)] private float wanderFrequency = 0.8f;
    [FormerlySerializedAs("variationStrength")]
    [SerializeField, Range(0f, 1f)] private float wanderStrength = 0.35f;
    [Tooltip("Donner une valeur différente à chaque poisson pour qu'ils n'errent pas tous pareil.")]
    [SerializeField] private int behaviorSeed;

    [Header("Side Walls")]
    [Tooltip("Part de la demi-largeur, depuis le bord, où l'évitement commence (0.35 = les 35 % les plus proches du bord).")]
    [SerializeField, Range(0.05f, 1f)] private float sideAvoidZone = 0.35f;
    [Tooltip("Force max de la poussée vers le centre. 1 = aussi forte que la fuite vers le large ; < 1 = le poisson peut encore longer le bord.")]
    [SerializeField, Range(0f, 1f)] private float sideAvoidStrength = 0.6f;

    private struct WaterFrame
    {
        public Vector3 awayFromBank;
        public Vector3 lateralAxis;
        public float lateralOffset;
        public float halfLateralExtent;
    }

    private FishDefinition fishDefinition;
    private BoxCollider waterBounds;
    private float noiseOffset;
    private float burstTimeRemaining;
    private float timeUntilNextBurst;

    private Vector3 lastFishPosition;
    private Vector3 lastEscapeDirection;

    /// <summary>
    /// Vrai pendant une ruée : le poisson tire à pleine force, quoi que fasse le joueur.
    /// </summary>
    public bool IsBursting => burstTimeRemaining > 0f;

    /// <summary>
    /// Multiplicateur d'effort courant : 1 en temps normal, burstForceMultiplier pendant une ruée.
    /// </summary>
    public float EffortMultiplier =>
        IsBursting && fishDefinition != null
            ? fishDefinition.burstForceMultiplier
            : 1f;

    /// <summary>
    /// Initialise le comportement avec le profil du poisson et son volume de nage.
    /// </summary>
    public void Configure(FishDefinition definition, BoxCollider configuredWaterBounds)
    {
        fishDefinition = definition;
        waterBounds = configuredWaterBounds;

        noiseOffset = behaviorSeed * NoiseSeedStride + NoiseSeedShift;
        burstTimeRemaining = 0f;
        timeUntilNextBurst = RollBurstInterval();
    }

    /// <summary>
    /// Fait avancer le timer de ruée. Appelé une fois par pas de simulation par la session.
    /// </summary>
    public void Tick(float deltaTime, float normalizedEndurance)
    {
        if (fishDefinition == null)
        {
            return;
        }

        if (IsBursting)
        {
            burstTimeRemaining -= deltaTime;

            if (burstTimeRemaining <= 0f)
            {
                burstTimeRemaining = 0f;
                timeUntilNextBurst = RollBurstInterval();
            }

            return;
        }

        timeUntilNextBurst -= deltaTime;

        if (timeUntilNextBurst > 0f ||
            normalizedEndurance < fishDefinition.burstMinEndurance)
        {
            return;
        }

        burstTimeRemaining = fishDefinition.burstDuration;
    }

    /// <summary>
    /// Retourne une direction horizontale normalisée : vers le large, avec dérive latérale
    /// et poussée douce loin des bords gauche/droit.
    /// </summary>
    public Vector3 GetEscapeDirection(Vector3 fishPosition, Vector3 anglerPosition)
    {
        WaterFrame frame = BuildWaterFrame(fishPosition, anglerPosition);

        float lateralSteering = Mathf.Clamp(
            ComputeWanderSteering() + ComputeSideWallSteering(frame),
            -1f,
            1f);

        Vector3 direction =
            frame.awayFromBank +
            frame.lateralAxis * lateralSteering;

        if (direction.sqrMagnitude < MinimumDirectionMagnitude)
        {
            direction = frame.awayFromBank;
        }

        lastFishPosition = fishPosition;
        lastEscapeDirection = direction.normalized;

        return lastEscapeDirection;
    }

    /// <summary>
    /// Repère de nage : la berge est le côté de la zone d'eau le plus proche du pêcheur.
    /// "Loin de la berge" est donc l'axe opposé, et "latéral" l'axe perpendiculaire.
    /// Sans zone d'eau connue, on retombe sur "s'éloigner du pêcheur" sans évitement de bords.
    /// </summary>
    private WaterFrame BuildWaterFrame(Vector3 fishPosition, Vector3 anglerPosition)
    {
        WaterFrame frame = new WaterFrame();

        if (waterBounds == null)
        {
            Vector3 awayFromAngler = fishPosition - anglerPosition;
            awayFromAngler.y = 0f;

            if (awayFromAngler.sqrMagnitude < MinimumDirectionMagnitude)
            {
                awayFromAngler = transform.forward;
                awayFromAngler.y = 0f;
            }

            frame.awayFromBank = awayFromAngler.normalized;
            frame.lateralAxis = Vector3.Cross(Vector3.up, frame.awayFromBank);
            frame.halfLateralExtent = 0f;
            return frame;
        }

        Bounds bounds = waterBounds.bounds;
        Vector3 anglerOffset = anglerPosition - bounds.center;

        float xRatio = Mathf.Abs(anglerOffset.x) / Mathf.Max(MinimumExtent, bounds.extents.x);
        float zRatio = Mathf.Abs(anglerOffset.z) / Mathf.Max(MinimumExtent, bounds.extents.z);

        bool bankOnXAxis = xRatio > zRatio;

        if (bankOnXAxis)
        {
            frame.awayFromBank = new Vector3(-Mathf.Sign(anglerOffset.x), 0f, 0f);
            frame.lateralAxis = Vector3.forward;
            frame.lateralOffset = fishPosition.z - bounds.center.z;
            frame.halfLateralExtent = bounds.extents.z;
        }
        else
        {
            frame.awayFromBank = new Vector3(0f, 0f, -Mathf.Sign(anglerOffset.z));
            frame.lateralAxis = Vector3.right;
            frame.lateralOffset = fishPosition.x - bounds.center.x;
            frame.halfLateralExtent = bounds.extents.x;
        }

        return frame;
    }

    /// <summary>
    /// Dérive latérale lente, entre -wanderStrength et +wanderStrength (bruit de Perlin : pas de cycle répétitif).
    /// </summary>
    private float ComputeWanderSteering()
    {
        float noise = Mathf.PerlinNoise(Time.time * wanderFrequency, noiseOffset) * 2f - 1f;
        return noise * wanderStrength;
    }

    /// <summary>
    /// Poussée vers le centre qui croît en douceur dans les derniers sideAvoidZone du bord.
    /// Plafonnée à sideAvoidStrength : le poisson peut encore frôler le bord.
    /// </summary>
    private float ComputeSideWallSteering(WaterFrame frame)
    {
        if (frame.halfLateralExtent <= MinimumExtent)
        {
            return 0f;
        }

        float normalizedOffset = frame.lateralOffset / frame.halfLateralExtent;

        float proximity = Mathf.InverseLerp(
            1f - sideAvoidZone,
            1f,
            Mathf.Abs(normalizedOffset));

        float push = Mathf.SmoothStep(0f, 1f, proximity) * sideAvoidStrength;

        return -Mathf.Sign(normalizedOffset) * push;
    }

    private float RollBurstInterval()
    {
        if (fishDefinition == null)
        {
            return float.MaxValue;
        }

        return Random.Range(
            fishDefinition.minTimeBetweenBursts,
            fishDefinition.maxTimeBetweenBursts);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(lastFishPosition, lastEscapeDirection * 2f);
    }
}
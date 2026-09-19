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

    [Header("Global Water Boundaries")]
    [Tooltip(
    "Force qui ramène le poisson vers l'intérieur lorsqu'il approche " +
    "de n'importe quel bord."
)]
    [SerializeField, Range(0f, 3f)]
    private float boundaryAvoidanceStrength = 1.5f;

    [SerializeField, Range(0.05f, 0.5f)]
    private float boundaryAvoidanceZone = 0.2f;

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
    public Vector3 GetEscapeDirection(
    Vector3 fishPosition,
    Vector3 anglerPosition)
    {
        WaterFrame frame =
            BuildWaterFrame(
                fishPosition,
                anglerPosition);

        float lateralSteering =
            Mathf.Clamp(
                ComputeWanderSteering() +
                ComputeSideWallSteering(frame),
                -1f,
                1f);

        Vector3 direction =
            frame.awayFromBank +
            frame.lateralAxis *
            lateralSteering;

        // Empêche le poisson de rester bloqué contre le bord opposé.
        direction +=
            ComputeBoundarySteering(
                fishPosition);

        if (direction.sqrMagnitude <
            MinimumDirectionMagnitude)
        {
            direction =
                frame.awayFromBank;
        }

        lastFishPosition = fishPosition;
        lastEscapeDirection =
            direction.normalized;

        return lastEscapeDirection;
    }

    /// <summary>
    /// Repère de nage : la berge est le côté de la zone d'eau le plus proche du pêcheur.
    /// "Loin de la berge" est donc l'axe opposé, et "latéral" l'axe perpendiculaire.
    /// Sans zone d'eau connue, on retombe sur "s'éloigner du pêcheur" sans évitement de bords.
    /// </summary>
    private WaterFrame BuildWaterFrame(
    Vector3 fishPosition,
    Vector3 anglerPosition)
    {
        WaterFrame frame = new WaterFrame();

        if (waterBounds == null)
        {
            Vector3 awayFromAngler =
                fishPosition - anglerPosition;

            awayFromAngler.y = 0f;

            if (awayFromAngler.sqrMagnitude < MinimumDirectionMagnitude)
            {
                awayFromAngler = transform.forward;
                awayFromAngler.y = 0f;
            }

            frame.awayFromBank =
                awayFromAngler.normalized;

            frame.lateralAxis =
                Vector3.Cross(
                    Vector3.up,
                    frame.awayFromBank);

            return frame;
        }

        Bounds bounds = waterBounds.bounds;

        float distanceLeft =
            Mathf.Abs(
                anglerPosition.x -
                bounds.min.x);

        float distanceRight =
            Mathf.Abs(
                bounds.max.x -
                anglerPosition.x);

        float distanceBack =
            Mathf.Abs(
                anglerPosition.z -
                bounds.min.z);

        float distanceFront =
            Mathf.Abs(
                bounds.max.z -
                anglerPosition.z);

        float smallestDistance =
            Mathf.Min(
                distanceLeft,
                distanceRight,
                distanceBack,
                distanceFront);

        if (Mathf.Approximately(
            smallestDistance,
            distanceLeft))
        {
            // Berge à gauche -> fuite vers la droite.
            frame.awayFromBank = Vector3.right;
            frame.lateralAxis = Vector3.forward;
            frame.lateralOffset =
                fishPosition.z -
                bounds.center.z;
            frame.halfLateralExtent =
                bounds.extents.z;
        }
        else if (Mathf.Approximately(
            smallestDistance,
            distanceRight))
        {
            // Berge à droite -> fuite vers la gauche.
            frame.awayFromBank = Vector3.left;
            frame.lateralAxis = Vector3.forward;
            frame.lateralOffset =
                fishPosition.z -
                bounds.center.z;
            frame.halfLateralExtent =
                bounds.extents.z;
        }
        else if (Mathf.Approximately(
            smallestDistance,
            distanceBack))
        {
            // Berge derrière -> fuite vers l'avant.
            frame.awayFromBank = Vector3.forward;
            frame.lateralAxis = Vector3.right;
            frame.lateralOffset =
                fishPosition.x -
                bounds.center.x;
            frame.halfLateralExtent =
                bounds.extents.x;
        }
        else
        {
            // Berge devant -> fuite vers l'arrière.
            frame.awayFromBank = Vector3.back;
            frame.lateralAxis = Vector3.right;
            frame.lateralOffset =
                fishPosition.x -
                bounds.center.x;
            frame.halfLateralExtent =
                bounds.extents.x;
        }

        return frame;
    }

    private Vector3 ComputeBoundarySteering(
    Vector3 fishPosition)
    {
        if (waterBounds == null)
        {
            return Vector3.zero;
        }

        Bounds bounds = waterBounds.bounds;

        float zoneX =
            Mathf.Max(
                MinimumExtent,
                bounds.extents.x *
                boundaryAvoidanceZone);

        float zoneZ =
            Mathf.Max(
                MinimumExtent,
                bounds.extents.z *
                boundaryAvoidanceZone);

        float left =
            1f -
            Mathf.InverseLerp(
                bounds.min.x,
                bounds.min.x + zoneX,
                fishPosition.x);

        float right =
            Mathf.InverseLerp(
                bounds.max.x - zoneX,
                bounds.max.x,
                fishPosition.x);

        float back =
            1f -
            Mathf.InverseLerp(
                bounds.min.z,
                bounds.min.z + zoneZ,
                fishPosition.z);

        float front =
            Mathf.InverseLerp(
                bounds.max.z - zoneZ,
                bounds.max.z,
                fishPosition.z);

        Vector3 steering =
            Vector3.right * left +
            Vector3.left * right +
            Vector3.forward * back +
            Vector3.back * front;

        if (steering.sqrMagnitude <
            MinimumDirectionMagnitude)
        {
            return Vector3.zero;
        }

        return steering.normalized *
               boundaryAvoidanceStrength;
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
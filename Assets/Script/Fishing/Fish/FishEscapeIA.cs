using System;
using UnityEngine;

/// <summary>Machine à états unique du poisson ; la session reste propriétaire des forces et de l'endurance.</summary>
public sealed class FishEscapeAI : MonoBehaviour
{
    private const float MinimumDirectionMagnitude = 0.001f;
    private const float MinimumExtent = 0.01f;
    private const float MinimumStateDuration = 0.05f;
    private const float ObservationInterval = 0.05f;
    private const float MaximumReactionDelay = 2f;
    private const int ObservationCapacity = 64;
    private const float MaximumHeadingCorrectionDegrees = 30f;
    private const float DodgeHeadingCorrectionDegrees = 12f;
    private const float GizmoDirectionLength = 2f;

    [Header("Local Random")]
    [Tooltip("Zéro utilise l'identifiant du poisson ; aucun changement du générateur global Unity.")]
    [SerializeField] private int behaviorSeed;

    [Header("Side Walls")]
    [SerializeField, Range(0.05f, 1f)] private float sideAvoidZone = 0.35f;
    [SerializeField, Range(0f, 1f)] private float sideAvoidStrength = 0.6f;

    [Header("Global Water Boundaries")]
    [SerializeField, Range(0f, 3f)] private float boundaryAvoidanceStrength = 1.5f;
    [SerializeField, Range(0.05f, 0.5f)] private float boundaryAvoidanceZone = 0.2f;

    private struct WaterFrame
    {
        public Vector3 awayFromBank;
        public Vector3 lateralAxis;
        public float lateralOffset;
        public float halfLateralExtent;
    }

    private struct Observation
    {
        public float time;
        public FishCombatContext context;
    }

    private readonly Observation[] observations = new Observation[ObservationCapacity];
    private int observationHead;
    private int observationCount;
    private float observationTimer;
    private float combatTime;
    private float reactionDelay;
    private float sustainedGuidanceTime;
    private float guidanceSign;
    private float pullPressure;
    private FishCombatContext perceivedContext;
    private FishCombatContext latestContext;
    private bool hasContext;
    private bool isCombatActive;
    private bool exhaustionLatched;
    private FishDefinition fishDefinition;
    private BoxCollider waterBounds;
    private FishSpellcastingAI spellcastingService;
    private System.Random localRandom;
    private FishCombatState lastOffensiveState;
    private float stateDuration;
    private float stateElapsed;
    private float timeUntilNextBurst;
    private float currentLateralEscapeStrength;
    private Vector3 lockedActionDirection;
    private Vector3 lastFishPosition;
    private Vector3 lastEscapeDirection;
    private float normalizedEndurance = 1f;

    /// <summary>Seule action principale courante, distincte de l'état de session.</summary>
    public FishCombatState CurrentState { get; private set; } = FishCombatState.Struggle;

    /// <summary>Progression des états temporisés. L'incantation reste à zéro jusqu'à sa fin réelle, sans durée dupliquée.</summary>
    public float NormalizedStateProgress => !isCombatActive || CurrentState == FishCombatState.Exhausted
        ? 0f
        : CurrentState == FishCombatState.CastSpell
            ? (spellcastingService != null && spellcastingService.IsCasting ? 0f : 1f)
            : Mathf.Clamp01(stateElapsed / Mathf.Max(MinimumStateDuration, stateDuration));

    /// <summary>Notification unique par changement d'état ; arrêt de session n'émet pas une fausse action.</summary>
    public event Action<FishCombatState> StateChanged;

    /// <summary>Vrai uniquement pendant la ruée, pas pendant son annonce ni le rebond de berge.</summary>
    public bool IsBursting => isCombatActive && CurrentState == FishCombatState.Burst;

    /// <summary>Seuil atteint de façon irréversible pour cette session, même pendant un rebond/break en cours.</summary>
    public bool IsExhaustionLatched => exhaustionLatched;

    /// <summary>Dernière cause de break engagée ; remise à zéro à Configure.</summary>
    public FishBreakCause CurrentBreakCause { get; private set; }

    /// <summary>Compatibilité : la fatigue est déjà incluse dans EffortMultiplier, ne pas la multiplier à nouveau.</summary>
    public float EnduranceEscapeMultiplier => 1f;

    /// <summary>Compatibilité : la berge est désormais ShoreRebound, jamais une ruée autonome.</summary>
    public bool IsShoreBurst => false;

    /// <summary>Effort de l'état, modulé une seule fois par la fatigue ; nul après StopCombat.</summary>
    public float EffortMultiplier
    {
        get
        {
            if (!isCombatActive || fishDefinition == null)
            {
                return 0f;
            }

            if (CurrentState == FishCombatState.Exhausted)
            {
                return Unit(fishDefinition.exhaustedForceMultiplier);
            }

            float effort;
            switch (CurrentState)
            {
                case FishCombatState.LateralDodge: effort = AtLeast(fishDefinition.lateralDodgeEffortMultiplier, 0f); break;
                case FishCombatState.BurstWindup: effort = Unit(fishDefinition.burstWindupEffortMultiplier); break;
                case FishCombatState.Burst: effort = AtLeast(fishDefinition.burstForceMultiplier, 1f); break;
                case FishCombatState.CastSpell: effort = Unit(fishDefinition.castingEffortMultiplier); break;
                case FishCombatState.Recovery: effort = Unit(fishDefinition.recoveryEffortMultiplier); break;
                case FishCombatState.ShoreRebound: effort = 0f; break;
                case FishCombatState.Break: effort = Unit(fishDefinition.fishBreakEffortMultiplier); break;
                default: effort = 1f; break;
            }

            float enduranceFactor = Mathf.InverseLerp(Unit(fishDefinition.captureEnduranceRatio), 1f, normalizedEndurance);
            return effort * Mathf.Lerp(Unit(fishDefinition.tiredEffortMultiplier), 1f, enduranceFactor);
        }
    }

    /// <summary>Réinitialise décisions, mémoire, aléatoire local et latch en conservant le profil et le volume existants.</summary>
    public void Configure(FishDefinition definition, BoxCollider configuredWaterBounds)
    {
        StopCombat();
        fishDefinition = definition;
        waterBounds = configuredWaterBounds;
        localRandom = new System.Random(behaviorSeed != 0 ? behaviorSeed : GetInstanceID());
        combatTime = 0f;
        observationHead = 0;
        observationCount = 0;
        observationTimer = ObservationInterval;
        hasContext = false;
        exhaustionLatched = false;
        normalizedEndurance = 1f;
        sustainedGuidanceTime = 0f;
        guidanceSign = 0f;
        pullPressure = 0f;
        latestContext = default;
        perceivedContext = default;
        lockedActionDirection = Vector3.zero;
        lastFishPosition = transform.position;
        lastEscapeDirection = Vector3.zero;
        CurrentBreakCause = default;
        lastOffensiveState = FishCombatState.Struggle;
        isCombatActive = definition != null;
        if (!isCombatActive)
        {
            return;
        }

        reactionDelay = Mathf.Clamp(Roll(definition.minReactionDelay, definition.maxReactionDelay, 0f), 0f, MaximumReactionDelay);
        timeUntilNextBurst = RollBurstInterval();
        currentLateralEscapeStrength = Roll(-1f, 1f, -1f) * Unit(definition.struggleLateralStrength);
        EnterStruggle();
    }

    /// <summary>Branche le service configuré par la session ; l'IA seule décide quand TryStartCast est appelé.</summary>
    public void SetSpellcastingService(FishSpellcastingAI service)
    {
        if (spellcastingService == service)
        {
            return;
        }

        spellcastingService?.CancelCasting();
        spellcastingService = service;
        if (isCombatActive && CurrentState == FishCombatState.CastSpell)
        {
            EnterRecovery();
        }
    }

    /// <summary>Termine toute activité, annule l'incantation et neutralise les sorties sans réarmer d'attaque.</summary>
    public void StopCombat()
    {
        isCombatActive = false;
        spellcastingService?.CancelCasting();
        observationHead = 0;
        observationCount = 0;
        hasContext = false;
        sustainedGuidanceTime = 0f;
        guidanceSign = 0f;
        stateElapsed = 0f;
        stateDuration = 0f;
        timeUntilNextBurst = 0f;
        lockedActionDirection = Vector3.zero;
        lastEscapeDirection = Vector3.zero;
    }

    /// <summary>Avance une fois par pas de session, après AdvanceCooldown et le traitement des impacts/berge.</summary>
    public void Tick(float deltaTime, in FishCombatContext context)
    {
        if (!isCombatActive || fishDefinition == null || !IsFinite(deltaTime) || deltaTime <= 0f)
        {
            return;
        }

        latestContext = context;
        normalizedEndurance = context.NormalizedEndurance;
        exhaustionLatched |= normalizedEndurance <= Unit(fishDefinition.captureEnduranceRatio);
        ObserveDelayedActions(deltaTime, in context);
        timeUntilNextBurst = Mathf.Max(0f, timeUntilNextBurst - deltaTime);

        // Le latch n'interrompt jamais la séquence de berge/break déjà engagée.
        if (exhaustionLatched && CurrentState != FishCombatState.ShoreRebound &&
            CurrentState != FishCombatState.Break && CurrentState != FishCombatState.Exhausted)
        {
            EnterState(FishCombatState.Exhausted, 0f);
            return;
        }

        if (CurrentState == FishCombatState.Exhausted)
        {
            return;
        }

        stateElapsed += deltaTime;
        if (CurrentState == FishCombatState.CastSpell)
        {
            if (spellcastingService == null || !spellcastingService.IsCasting)
            {
                EnterRecovery();
            }
            return;
        }

        if (CurrentState == FishCombatState.Struggle)
        {
            UpdateStruggleSteering(deltaTime);
        }

        if (stateElapsed < stateDuration)
        {
            return;
        }

        switch (CurrentState)
        {
            case FishCombatState.Struggle: ChooseNextAction(); break;
            case FishCombatState.BurstWindup:
                EnterState(FishCombatState.Burst, Roll(fishDefinition.minBurstDuration, fishDefinition.maxBurstDuration, MinimumStateDuration));
                break;
            case FishCombatState.ShoreRebound:
                EnterState(FishCombatState.Break, AtLeast(fishDefinition.fishBreakDuration, MinimumStateDuration));
                break;
            case FishCombatState.Break:
                if (exhaustionLatched) EnterState(FishCombatState.Exhausted, 0f);
                else EnterRecovery();
                break;
            case FishCombatState.Recovery: EnterStruggle(); break;
            default: EnterRecovery(); break;
        }
    }

    /// <summary>Compatibilité pour l'ancien appel ; les nouvelles sessions doivent fournir l'instantané complet.</summary>
    public void Tick(float deltaTime, float configuredNormalizedEndurance)
    {
        FishCombatContext context = new FishCombatContext(
            hasContext ? latestContext.FishPosition : transform.position,
            hasContext ? latestContext.FishVelocity : Vector3.zero,
            hasContext ? latestContext.PlayerPosition : transform.position,
            Vector3.zero, 0f, 0f, 0f, configuredNormalizedEndurance, 0f);
        Tick(deltaTime, in context);
    }

    /// <summary>Interrompt prioritairement l'action, sans coût ni immunité ; Shore engage rebond puis break automatiquement.</summary>
    public bool TryEnterBreak(FishBreakCause cause)
    {
        if (!isCombatActive || fishDefinition == null || exhaustionLatched ||
            CurrentState == FishCombatState.ShoreRebound || CurrentState == FishCombatState.Break ||
            CurrentState == FishCombatState.Exhausted ||
            (cause != FishBreakCause.Elemental && cause != FishBreakCause.Shore && cause != FishBreakCause.Environment))
        {
            return false;
        }

        CurrentBreakCause = cause;
        spellcastingService?.CancelCasting();
        if (cause == FishBreakCause.Shore)
        {
            EnterState(FishCombatState.ShoreRebound, AtLeast(fishDefinition.shoreReboundDuration, MinimumStateDuration));
        }
        else
        {
            EnterState(FishCombatState.Break, AtLeast(fishDefinition.fishBreakDuration, MinimumStateDuration));
        }
        return true;
    }

    /// <summary>Compatibilité explicite : interruption vers Recovery ; les dégâts normaux ne doivent plus l'appeler.</summary>
    public bool TryInterruptBurst()
    {
        if (!IsBursting) return false;
        EnterRecovery();
        return true;
    }

    /// <summary>Compatibilité inerte : les bords de WaterBounds ne déclenchent plus de ruée ni de coût.</summary>
    public bool TryTriggerShoreBurst(Vector3 fishPosition, float configuredNormalizedEndurance) => false;

    /// <summary>Compatibilité inerte : la session débite seule le nouveau coût de berge.</summary>
    public bool ConsumeShoreBurstCost() => false;

    /// <summary>Direction horizontale : cap annoncé verrouillé en préparation/ruée, dérobade engagée et steering de confinement.</summary>
    public Vector3 GetEscapeDirection(Vector3 fishPosition, Vector3 anglerPosition)
    {
        if (!isCombatActive || fishDefinition == null)
        {
            return Vector3.zero;
        }

        Vector3 direction;
        if (CurrentState == FishCombatState.BurstWindup)
        {
            direction = lockedActionDirection;
        }
        else if (CurrentState == FishCombatState.Burst || CurrentState == FishCombatState.LateralDodge)
        {
            float correction = CurrentState == FishCombatState.Burst
                ? Mathf.Clamp(AtLeast(fishDefinition.burstHeadingCorrectionDegrees, 0f), 0f, MaximumHeadingCorrectionDegrees)
                : DodgeHeadingCorrectionDegrees;
            Vector3 safeDirection = HorizontalDirection(lockedActionDirection + ComputeBoundarySteering(fishPosition), lockedActionDirection);
            // Toujours relatif au cap engagé : aucune dérive cumulative de l'annonce.
            direction = Vector3.RotateTowards(lockedActionDirection, safeDirection, correction * Mathf.Deg2Rad, 0f);
        }
        else
        {
            direction = ComputeStruggleDirection(fishPosition, anglerPosition);
        }

        lastFishPosition = fishPosition;
        lastEscapeDirection = HorizontalDirection(direction, Vector3.forward);
        return lastEscapeDirection;
    }

    private void ObserveDelayedActions(float deltaTime, in FishCombatContext context)
    {
        if (!hasContext)
        {
            perceivedContext = new FishCombatContext(context.FishPosition, Vector3.zero,
                context.PlayerPosition, Vector3.zero, 0f, 0f, 0f, context.NormalizedEndurance, 0f);
            hasContext = true;
        }

        combatTime += deltaTime;
        observationTimer += deltaTime;
        if (observationTimer >= ObservationInterval)
        {
            observationTimer %= ObservationInterval;
            if (observationCount == ObservationCapacity)
            {
                observationHead = (observationHead + 1) % ObservationCapacity;
                observationCount--;
            }
            int index = (observationHead + observationCount) % ObservationCapacity;
            observations[index] = new Observation { time = combatTime, context = context };
            observationCount++;
        }

        float perceptionCutoff = combatTime - reactionDelay;
        while (observationCount > 0 && observations[observationHead].time <= perceptionCutoff)
        {
            perceivedContext = observations[observationHead].context;
            observationHead = (observationHead + 1) % ObservationCapacity;
            observationCount--;
        }

        Vector3 tangent = GetLineTangent(in perceivedContext);
        float guidance = Mathf.Clamp(perceivedContext.LateralInput +
            Vector3.Dot(perceivedContext.PlayerVelocity, tangent) * AtLeast(fishDefinition.playerVelocityGuidanceWeight, 0f), -1f, 1f);
        float newSign = Mathf.Abs(guidance) > Mathf.Max(MinimumDirectionMagnitude, Unit(fishDefinition.guidanceInputThreshold))
            ? Mathf.Sign(guidance) : 0f;
        sustainedGuidanceTime = newSign != 0f && newSign == guidanceSign ? sustainedGuidanceTime + deltaTime : 0f;
        guidanceSign = newSign;
        float pressureTarget = Mathf.Clamp01(perceivedContext.PullInput - perceivedContext.ReleaseInput);
        pullPressure = Mathf.MoveTowards(pullPressure, pressureTarget, AtLeast(fishDefinition.struggleSteeringResponse, 0f) * deltaTime);
    }

    private void UpdateStruggleSteering(float deltaTime)
    {
        if (sustainedGuidanceTime < AtLeast(fishDefinition.sustainedGuidanceDuration, MinimumStateDuration) || guidanceSign == 0f)
        {
            return;
        }

        WaterFrame frame = BuildWaterFrame(latestContext.FishPosition, latestContext.PlayerPosition);
        Vector3 counterGuidance = -GetLineTangent(in perceivedContext) * guidanceSign;
        float targetStrength = Vector3.Dot(counterGuidance, frame.lateralAxis) * Unit(fishDefinition.struggleLateralStrength);
        currentLateralEscapeStrength = Mathf.MoveTowards(currentLateralEscapeStrength,
            targetStrength, AtLeast(fishDefinition.struggleSteeringResponse, 0f) * deltaTime);
    }

    private void ChooseNextAction()
    {
        Vector3 dodgeDirection = ChooseDodgeDirection();
        bool hasDodgeSpace = GetAvailableSpace(latestContext.FishPosition, dodgeDirection) >= AtLeast(fishDefinition.minimumDodgeSpace, 0f);
        float guidancePressure = sustainedGuidanceTime >= AtLeast(fishDefinition.sustainedGuidanceDuration, MinimumStateDuration) ? 1f : 0f;
        Vector3 awayFromPlayer = HorizontalDirection(perceivedContext.FishPosition - perceivedContext.PlayerPosition, Vector3.forward);
        float approachingSpeed = Mathf.Max(0f, -Vector3.Dot(perceivedContext.FishVelocity, awayFromPlayer));
        float approachPressure = Mathf.Clamp01(approachingSpeed / Mathf.Max(MinimumExtent, AtLeast(fishDefinition.maxSpeed, 0f)));
        float pressure = Mathf.Clamp01(pullPressure + approachPressure);
        bool canBurst = timeUntilNextBurst <= 0f && normalizedEndurance >= Unit(fishDefinition.burstMinEndurance);
        bool canCast = spellcastingService != null && spellcastingService.CanStartCast(normalizedEndurance);
        float burstWeight = canBurst && lastOffensiveState != FishCombatState.BurstWindup
            ? AtLeast(fishDefinition.burstDecisionWeight, 0f) * (1f + pressure * AtLeast(fishDefinition.pullPressureDecisionWeight, 0f) + perceivedContext.NormalizedTension) : 0f;
        float dodgeWeight = hasDodgeSpace && lastOffensiveState != FishCombatState.LateralDodge
            ? AtLeast(fishDefinition.dodgeDecisionWeight, 0f) * (1f + guidancePressure * AtLeast(fishDefinition.sustainedGuidanceDecisionWeight, 0f)) : 0f;
        float spellWeight = canCast && lastOffensiveState != FishCombatState.CastSpell
            ? AtLeast(fishDefinition.spellDecisionWeight, 0f) * (1f + perceivedContext.ReleaseInput) : 0f;
        float totalWeight = burstWeight + dodgeWeight + spellWeight;
        if (totalWeight <= MinimumDirectionMagnitude || !IsFinite(totalWeight))
        {
            // Une lutte active remplace toujours un sort indisponible ; elle réarme la variété du prochain choix.
            lastOffensiveState = FishCombatState.Struggle;
            EnterStruggle();
            return;
        }

        float choice = (float)localRandom.NextDouble() * totalWeight;
        if (choice < burstWeight)
        {
            lockedActionDirection = ComputeStruggleDirection(latestContext.FishPosition, latestContext.PlayerPosition);
            lastOffensiveState = FishCombatState.BurstWindup;
            EnterState(FishCombatState.BurstWindup, Roll(fishDefinition.minBurstWindupDuration, fishDefinition.maxBurstWindupDuration, MinimumStateDuration));
        }
        else if (choice < burstWeight + dodgeWeight)
        {
            lockedActionDirection = dodgeDirection;
            lastOffensiveState = FishCombatState.LateralDodge;
            EnterState(FishCombatState.LateralDodge, Roll(fishDefinition.minLateralDodgeDuration, fishDefinition.maxLateralDodgeDuration, MinimumStateDuration));
        }
        else if (spellcastingService != null && spellcastingService.TryStartCast(normalizedEndurance))
        {
            lastOffensiveState = FishCombatState.CastSpell;
            EnterState(FishCombatState.CastSpell, 0f);
        }
        else
        {
            lastOffensiveState = FishCombatState.Struggle;
            EnterStruggle();
        }
    }

    private Vector3 ChooseDodgeDirection()
    {
        WaterFrame frame = BuildWaterFrame(latestContext.FishPosition, latestContext.PlayerPosition);
        Vector3 tangent = GetLineTangent(in perceivedContext);
        float side = guidanceSign != 0f && sustainedGuidanceTime >= AtLeast(fishDefinition.sustainedGuidanceDuration, MinimumStateDuration)
            ? -guidanceSign : (localRandom.NextDouble() < 0.5 ? -1f : 1f);
        Vector3 forward = frame.awayFromBank * Unit(fishDefinition.dodgeForwardStrength);
        Vector3 desired = HorizontalDirection(tangent * side + forward, frame.lateralAxis);
        Vector3 alternative = HorizontalDirection(-tangent * side + forward, -frame.lateralAxis);
        float desiredSpace = GetAvailableSpace(latestContext.FishPosition, desired);
        float alternativeSpace = GetAvailableSpace(latestContext.FishPosition, alternative);
        return desiredSpace < AtLeast(fishDefinition.minimumDodgeSpace, 0f) && alternativeSpace > desiredSpace ? alternative : desired;
    }

    private float GetAvailableSpace(Vector3 position, Vector3 direction)
    {
        if (waterBounds == null) return float.MaxValue;
        Bounds bounds = waterBounds.bounds;
        float distance = float.MaxValue;
        if (Mathf.Abs(direction.x) > MinimumDirectionMagnitude)
            distance = Mathf.Min(distance, ((direction.x > 0f ? bounds.max.x : bounds.min.x) - position.x) / direction.x);
        if (Mathf.Abs(direction.z) > MinimumDirectionMagnitude)
            distance = Mathf.Min(distance, ((direction.z > 0f ? bounds.max.z : bounds.min.z) - position.z) / direction.z);
        return Mathf.Max(0f, distance);
    }

    private void EnterStruggle()
    {
        currentLateralEscapeStrength = Roll(-1f, 1f, -1f) * Unit(fishDefinition.struggleLateralStrength);
        EnterState(FishCombatState.Struggle, Roll(fishDefinition.minStruggleDuration, fishDefinition.maxStruggleDuration, MinimumStateDuration));
    }

    private void EnterRecovery() => EnterState(FishCombatState.Recovery,
        Roll(fishDefinition.minRecoveryDuration, fishDefinition.maxRecoveryDuration, MinimumStateDuration));

    private void EnterState(FishCombatState newState, float duration)
    {
        FishCombatState previousState = CurrentState;
        if ((previousState == FishCombatState.Burst || previousState == FishCombatState.BurstWindup) && newState != FishCombatState.Burst)
        {
            timeUntilNextBurst = RollBurstInterval();
        }
        if (previousState == FishCombatState.CastSpell && newState != FishCombatState.CastSpell)
        {
            spellcastingService?.CancelCasting();
        }
        CurrentState = newState;
        stateElapsed = 0f;
        stateDuration = duration;
        if (newState == FishCombatState.Exhausted)
        {
            spellcastingService?.CancelCasting();
        }
        if (previousState != newState) StateChanged?.Invoke(newState);
    }

    private Vector3 ComputeStruggleDirection(Vector3 fishPosition, Vector3 anglerPosition)
    {
        WaterFrame frame = BuildWaterFrame(fishPosition, anglerPosition);
        float lateralSteering = Mathf.Clamp(currentLateralEscapeStrength + ComputeSideWallSteering(frame), -1f, 1f);
        Vector3 lineResponse = hasContext
            ? HorizontalDirection(perceivedContext.FishPosition - perceivedContext.PlayerPosition, frame.awayFromBank) * pullPressure
            : Vector3.zero;
        return HorizontalDirection(frame.awayFromBank + lineResponse + frame.lateralAxis * lateralSteering +
            ComputeBoundarySteering(fishPosition), frame.awayFromBank);
    }

    private static Vector3 GetLineTangent(in FishCombatContext context) =>
        Vector3.Cross(Vector3.up, HorizontalDirection(context.FishPosition - context.PlayerPosition, Vector3.forward));

    private WaterFrame BuildWaterFrame(Vector3 fishPosition, Vector3 anglerPosition)
    {
        WaterFrame frame = new WaterFrame();
        if (waterBounds == null)
        {
            frame.awayFromBank = HorizontalDirection(fishPosition - anglerPosition, transform.forward);
            frame.lateralAxis = Vector3.Cross(Vector3.up, frame.awayFromBank);
            return frame;
        }

        Bounds bounds = waterBounds.bounds;
        float distanceLeft = Mathf.Abs(anglerPosition.x - bounds.min.x);
        float distanceRight = Mathf.Abs(bounds.max.x - anglerPosition.x);
        float distanceBack = Mathf.Abs(anglerPosition.z - bounds.min.z);
        float distanceFront = Mathf.Abs(bounds.max.z - anglerPosition.z);
        float smallestDistance = Mathf.Min(Mathf.Min(distanceLeft, distanceRight), Mathf.Min(distanceBack, distanceFront));
        if (Mathf.Approximately(smallestDistance, distanceLeft) || Mathf.Approximately(smallestDistance, distanceRight))
        {
            frame.awayFromBank = Mathf.Approximately(smallestDistance, distanceLeft) ? Vector3.right : Vector3.left;
            frame.lateralAxis = Vector3.forward;
            frame.lateralOffset = fishPosition.z - bounds.center.z;
            frame.halfLateralExtent = bounds.extents.z;
        }
        else
        {
            frame.awayFromBank = Mathf.Approximately(smallestDistance, distanceBack) ? Vector3.forward : Vector3.back;
            frame.lateralAxis = Vector3.right;
            frame.lateralOffset = fishPosition.x - bounds.center.x;
            frame.halfLateralExtent = bounds.extents.x;
        }
        return frame;
    }

    private Vector3 ComputeBoundarySteering(Vector3 fishPosition)
    {
        if (waterBounds == null) return Vector3.zero;
        Bounds bounds = waterBounds.bounds;
        float zoneX = Mathf.Max(MinimumExtent, bounds.extents.x * boundaryAvoidanceZone);
        float zoneZ = Mathf.Max(MinimumExtent, bounds.extents.z * boundaryAvoidanceZone);
        float left = 1f - Mathf.InverseLerp(bounds.min.x, bounds.min.x + zoneX, fishPosition.x);
        float right = Mathf.InverseLerp(bounds.max.x - zoneX, bounds.max.x, fishPosition.x);
        float back = 1f - Mathf.InverseLerp(bounds.min.z, bounds.min.z + zoneZ, fishPosition.z);
        float front = Mathf.InverseLerp(bounds.max.z - zoneZ, bounds.max.z, fishPosition.z);
        Vector3 steering = Vector3.right * left + Vector3.left * right + Vector3.forward * back + Vector3.back * front;
        return steering.sqrMagnitude < MinimumDirectionMagnitude ? Vector3.zero : steering.normalized * boundaryAvoidanceStrength;
    }

    private float ComputeSideWallSteering(WaterFrame frame)
    {
        if (frame.halfLateralExtent <= MinimumExtent) return 0f;
        float offset = frame.lateralOffset / frame.halfLateralExtent;
        float proximity = Mathf.InverseLerp(1f - sideAvoidZone, 1f, Mathf.Abs(offset));
        return -Mathf.Sign(offset) * Mathf.SmoothStep(0f, 1f, proximity) * sideAvoidStrength;
    }

    private float RollBurstInterval() => Roll(fishDefinition.minTimeBetweenBursts, fishDefinition.maxTimeBetweenBursts, 0f);

    private float Roll(float minimum, float maximum, float lowerBound)
    {
        float low = AtLeast(minimum, lowerBound);
        float high = AtLeast(maximum, low);
        return Mathf.Lerp(low, high, (float)localRandom.NextDouble());
    }

    private static Vector3 HorizontalDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (!IsFinite(direction.x) || !IsFinite(direction.z) || direction.sqrMagnitude < MinimumDirectionMagnitude)
        {
            direction = fallback;
            direction.y = 0f;
        }
        return direction.sqrMagnitude < MinimumDirectionMagnitude ? Vector3.forward : direction.normalized;
    }

    private void OnDisable() => StopCombat();

    private void OnValidate()
    {
        sideAvoidZone = Mathf.Clamp(AtLeast(sideAvoidZone, MinimumStateDuration), MinimumStateDuration, 1f);
        sideAvoidStrength = Unit(sideAvoidStrength);
        boundaryAvoidanceStrength = Mathf.Clamp(AtLeast(boundaryAvoidanceStrength, 0f), 0f, 3f);
        boundaryAvoidanceZone = Mathf.Clamp(AtLeast(boundaryAvoidanceZone, MinimumStateDuration), MinimumStateDuration, 0.5f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(lastFishPosition, lastEscapeDirection * GizmoDirectionLength);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static float Unit(float value) => Mathf.Clamp01(AtLeast(value, 0f));
    private static float AtLeast(float value, float minimum) => IsFinite(value) ? Mathf.Max(minimum, value) : minimum;
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Autorité unique de simulation, ressources, breaks et conditions terminales du combat.</summary>
public class FishingSessionController : MonoBehaviour, ISpendableEndurance
{
    private const float Epsilon = 0.001f;
    private const float MinimumStruggleDrainRatio = 0.35f;
    private const float BurstStruggleDrainMultiplier = 1.25f;
    private const float MinimumTensionLoad = 0.15f;
    private const int InfluenceCapacity = 128;
    private const int MaximumPendingImpacts = 32;
    private const float MinimumBurstGuidanceAuthority = 0.5f;
    private const float MinimumInfluenceWindow = 0.05f;
    private const float MaximumInfluenceWindow = 2f;

    [Header("Required References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform rodTip;
    [SerializeField] private FishDefinition fishDefinition;
    [SerializeField] private FishingInputReader fishingInputReader;
    [SerializeField] private FishEscapeAI fishEscapeAI;
    [SerializeField] private FishSpellcastingAI fishSpellcastingAI;
    [SerializeField] private FishMovementController fishMovementController;
    [SerializeField] private BoxCollider waterBounds;
    [SerializeField] private BoxCollider catchZone;
    [SerializeField] private FishingLinePresenter fishingLinePresenter;
    [SerializeField] private ATBTimingController atbTimingController;

    [Header("Spatial Control")]
    [SerializeField, Min(0f)] private float reelForce = 30f;
    [SerializeField, Range(0f, 1f)] private float maxFishResistanceWhilePulling = 0.6f;
    [SerializeField, Range(0f, 1f)] private float openingResistanceRatio = 0.3f;
    [SerializeField, Range(0f, 1f)] private float fishBreakResistanceRatio = 0.1f;
    [SerializeField, Range(0f, 1f)] private float fishLateralMovementRatio = 0.35f;
    [SerializeField, Min(0.01f)] private float guidanceResponseTime = 0.18f;
    [SerializeField, Min(0f)] private float maximumLateralSpeed = 4.5f;
    [SerializeField, Min(0f)] private float maximumGuidanceAcceleration = 30f;
    [SerializeField, Min(0f)] private float playerMovementGuidanceWeight = 1f;
    [SerializeField, Range(0.5f, 1f)] private float burstGuidanceAuthority = 0.65f;
    [SerializeField, Min(0f)] private float targetApproachSpeed = 3.5f;
    [SerializeField, Min(1f)] private float fishBreakPullMultiplier = 1.3f;
    [SerializeField, Range(0f, 1f)] private float shorePullAuthority = 0.15f;

    [Header("Tension - Line Rupture")]
    [SerializeField, Min(0f)] private float pullTensionRate = 0.55f;
    [SerializeField, Min(0f)] private float releaseTensionRecovery = 0.9f;
    [SerializeField, Min(0f)] private float idleTensionRecovery = 0.3f;
    [SerializeField, Range(0.5f, 1f)] private float breakThreshold = 1f;
    [SerializeField, Min(0.1f)] private float breakDuration = 2.5f;
    [SerializeField, Min(0f)] private float breakTimerDecayMultiplier = 2f;
    [SerializeField, Min(0f)] private float lateralTensionRate = 0.12f;
    [SerializeField, Min(0f)] private float burstTensionRate = 0.4f;

    [Header("Line Distance")]
    [SerializeField, Min(0f)] private float lineDistanceTolerance = 1f;
    [SerializeField, Min(0f)] private float distanceTensionRate = 0.08f;
    [SerializeField, Min(0f)] private float maximumStretchForTension = 4f;
    [SerializeField, Min(0f)] private float distanceSlackRecoveryRate = 0.06f;

    [Header("Endurance")]
    [SerializeField, Min(0f)] private float enduranceDrainPerSecond = 3f;
    [SerializeField, Range(0f, 1f)] private float fishBreakAutonomousDrainRatio = 0.02f;
    [SerializeField, Min(1f)] private float fishBreakPullDrainMultiplier = 2.5f;

    [Header("Fish Break - Not Line Rupture")]
    [SerializeField, Min(1f)] private float elementalBreakCapacity = 100f;
    [SerializeField, Min(0f)] private float maximumElementalPowerPerImpact = 50f;
    [SerializeField, Min(0f)] private float fishBreakImmunityDuration = 2f;
    [SerializeField, Range(0f, 1f)] private float shoreEnduranceCostRatio = 0.12f;
    [SerializeField, Min(0f)] private float shoreReboundSpeed = 5.5f;
    [SerializeField, Min(0f)] private float shoreCooldownDuration = 4f;
    [SerializeField, Min(0f)] private float shoreRearmMargin = 2f;

    [Header("Directed Environment Impacts")]
    [SerializeField, Range(0.05f, 2f)] private float playerInfluenceWindow = 0.75f;
    [SerializeField, Min(0f)] private float minimumDirectedVelocityContribution = 0.15f;

    private struct InfluenceSample { public float time; public Vector3 velocityChange; }
    private struct EnvironmentImpact
    {
        public FishingBreakObstacle obstacle;
        public Vector3 normal;
        public float speed;
    }
    private struct ObstacleLock { public float until; public bool separated; }

    private readonly InfluenceSample[] influenceHistory = new InfluenceSample[InfluenceCapacity];
    private readonly List<EnvironmentImpact> pendingImpacts = new List<EnvironmentImpact>(MaximumPendingImpacts);
    private readonly Dictionary<FishingBreakObstacle, ObstacleLock> obstacleLocks = new Dictionary<FishingBreakObstacle, ObstacleLock>();
    private readonly List<FishingBreakObstacle> obstacleKeys = new List<FishingBreakObstacle>();
    private readonly ATBContinuousBoostTracker pullBoostTracker = new ATBContinuousBoostTracker();
    private readonly ATBContinuousBoostTracker releaseBoostTracker = new ATBContinuousBoostTracker();
    private readonly ATBContinuousBoostTracker lateralBoostTracker = new ATBContinuousBoostTracker();
    private FishingPlayerController playerController;
    private FishIdentificationState fishIdentity;
    private FishEnvironmentCollisionRelay collisionRelay;
    private int influenceHead;
    private int influenceCount;
    private float combatTime;
    private float currentEndurance;
    private float normalizedTension;
    private float whirlwindTimeRemaining;
    private float whirlwindPullAcceleration;
    private float breakTimer;
    private float referenceLineDistance;
    private float elementalBreakGauge;
    private float pendingElementalPower;
    private float fishBreakImmuneUntil;
    private float shoreCooldownUntil;
    private bool shoreArmed;
    private bool shoreCaptureRequiresExit;
    private bool exhaustionLatched;
    private bool isConfigured;
    private bool hasLoggedConfigurationWarning;
    private Vector3 previousFishPosition;
    private Vector3 lastLineDirection = Vector3.back;
    private FishCombatState observedFishState;

    public FishingState State { get; private set; } = FishingState.Active;
    public float CurrentEndurance => currentEndurance;
    public float NormalizedTension => normalizedTension;
    public float NormalizedEndurance => fishDefinition == null ? 0f : Mathf.Clamp01(currentEndurance / Mathf.Max(Epsilon, fishDefinition.maxEndurance));
    public float BreakProgress => Mathf.Clamp01(breakTimer / Mathf.Max(Epsilon, breakDuration));
    public float NormalizedElementalBreak => Mathf.Clamp01(elementalBreakGauge / Mathf.Max(Epsilon, elementalBreakCapacity));
    public float NormalizedFishBreakRemaining => State == FishingState.Active && CurrentFishState == FishCombatState.Break ? 1f - fishEscapeAI.NormalizedStateProgress : 0f;
    public FishCombatState CurrentFishState => fishEscapeAI != null ? fishEscapeAI.CurrentState : FishCombatState.Struggle;
    public bool CanCatchFish => isConfigured && State == FishingState.Active && NormalizedEndurance <= fishDefinition.captureEnduranceRatio && !IsFishBreakSequence;
    public FishBreakCause LastFishBreakCause { get; private set; }
    private bool IsFishBreakSequence => CurrentFishState == FishCombatState.Break || CurrentFishState == FishCombatState.ShoreRebound;
    private bool CanStartBreak => isConfigured && State == FishingState.Active && !IsFishBreakSequence && combatTime >= fishBreakImmuneUntil;

    public event Action<FishingState> StateChanged;
    public event Action BurstInterrupted;
    public event Action<FishBreakCause> FishBreakStarted;

    private void Awake()
    {
        if (atbTimingController == null) atbTimingController = FindFirstObjectByType<ATBTimingController>();
    }

    private void Start() { ResetSession(); }

    private void FixedUpdate()
    {
        if (!isConfigured || State != FishingState.Active) return;
        float deltaTime = Time.fixedDeltaTime;
        combatTime += deltaTime;
        float pull = Mathf.Clamp01(fishingInputReader.PullInput);
        float release = Mathf.Clamp01(fishingInputReader.ReleaseInput);
        float lateral = Mathf.Clamp(fishingInputReader.LateralInput, -1f, 1f);
        Vector3 playerVelocity = playerController != null ? playerController.HorizontalVelocity : Vector3.zero;
        Vector3 fishPosition = fishMovementController.Position;
        float pullMultiplier = pullBoostTracker.Evaluate(pull > Epsilon, atbTimingController);
        float releaseMultiplier = releaseBoostTracker.Evaluate(release > Epsilon, atbTimingController);
        float lateralMultiplier = lateralBoostTracker.Evaluate(Mathf.Abs(lateral) > Epsilon, atbTimingController);

        UpdateObstacleSeparation();
        ProcessPendingImpacts();
        ProcessShoreContact(fishPosition);
        if (State != FishingState.Active) return;
        fishSpellcastingAI?.AdvanceCooldown(deltaTime);
        var context = new FishCombatContext(fishPosition, fishMovementController.CurrentVelocity,
            playerTransform.position, playerVelocity, pull, release, lateral, NormalizedEndurance, normalizedTension);
        fishEscapeAI.Tick(deltaTime, in context);

        Vector3 directionToPlayer = Horizontal(playerTransform.position - fishPosition);
        if (directionToPlayer.sqrMagnitude > Epsilon) lastLineDirection = directionToPlayer.normalized;
        directionToPlayer = lastLineDirection;
        Vector3 escapeDirection = fishEscapeAI.GetEscapeDirection(fishPosition, playerTransform.position);
        float effectiveReelForce = reelForce * pullMultiplier * (1f - Mathf.Clamp01(fishDefinition.resistanceToPull));
        ApplyForces(pull, lateral, pullMultiplier, lateralMultiplier, playerVelocity, directionToPlayer,
            escapeDirection, effectiveReelForce, deltaTime, out float resistance, out float exertedPull);
        UpdateEndurance(exertedPull, pullMultiplier, resistance, effectiveReelForce, deltaTime);
        UpdateTension(pull, release, lateral, releaseMultiplier, resistance, effectiveReelForce, fishPosition, deltaTime);
        fishMovementController.Simulate(deltaTime);
        previousFishPosition = fishPosition;
        UpdateBreakTimer(deltaTime);
    }

    private void Update()
    {
        if (!isConfigured || State != FishingState.Active) return;
        OrientPlayerTowardsFish();
        if (fishingLinePresenter != null) fishingLinePresenter.SetLineTension(normalizedTension);
    }

    /// <summary>Réinitialise toutes les ressources, impulsions, mémoires et protections de la session.</summary>
    public void ResetSession()
    {
        isConfigured = ValidateConfiguration();
        if (!isConfigured) return;
        if (fishEscapeAI != null) fishEscapeAI.StateChanged -= HandleFishStateChanged;
        playerController = playerTransform.GetComponent<FishingPlayerController>();
        fishIdentity = fishMovementController.GetComponent<FishIdentificationState>();
        collisionRelay = fishMovementController.GetComponent<FishEnvironmentCollisionRelay>();
        playerController?.ResetMovement();
        fishIdentity?.ResetIdentification();
        currentEndurance = fishDefinition.maxEndurance;
        normalizedTension = breakTimer = combatTime = 0f;
        elementalBreakGauge = pendingElementalPower = 0f;
        fishBreakImmuneUntil = shoreCooldownUntil = 0f;
        whirlwindTimeRemaining = whirlwindPullAcceleration = 0f;
        exhaustionLatched = shoreCaptureRequiresExit = false;
        shoreArmed = true;
        LastFishBreakCause = default;
        pendingImpacts.Clear();
        obstacleLocks.Clear();
        obstacleKeys.Clear();
        influenceHead = influenceCount = 0;
        pullBoostTracker.Reset(); releaseBoostTracker.Reset(); lateralBoostTracker.Reset();
        fishMovementController.Configure(fishDefinition, waterBounds);
        previousFishPosition = fishMovementController.Position;
        Vector3 initialLine = Horizontal(playerTransform.position - previousFishPosition);
        lastLineDirection = initialLine.sqrMagnitude > Epsilon ? initialLine.normalized : Vector3.back;
        referenceLineDistance = GetHorizontalLineDistance(previousFishPosition);
        fishEscapeAI.Configure(fishDefinition, waterBounds);
        fishSpellcastingAI?.Configure(playerTransform, this);
        fishEscapeAI.SetSpellcastingService(fishSpellcastingAI);
        observedFishState = fishEscapeAI.CurrentState;
        fishEscapeAI.StateChanged += HandleFishStateChanged;
        collisionRelay?.Configure(this);
        if (fishingLinePresenter != null)
        {
            fishingLinePresenter.Configure(rodTip, fishMovementController.transform);
            fishingLinePresenter.SetLineTension(0f);
        }
        SetState(FishingState.Active);
    }

    /// <summary>Termine le combat lorsque le joueur est épuisé.</summary>
    public void EndFromPlayerExhaustion() { EndSession(FishingState.Escaped); }

    /// <summary>Ajoute une surtension valide pendant le combat.</summary>
    public void ApplyTensionSpike(float normalizedAmount)
    {
        if (isConfigured && State == FishingState.Active && ValidAmount(normalizedAmount))
            normalizedTension = Mathf.Clamp01(normalizedTension + normalizedAmount);
    }

    /// <summary>Inflige des dégâts ordinaires, sans interrompre automatiquement une ruée.</summary>
    public void ApplyFishEnduranceDamage(float amount)
    {
        if (!isConfigured || State != FishingState.Active || !ValidAmount(amount)) return;
        currentEndurance = Mathf.Max(0f, currentEndurance - amount);
        LatchExhaustion();
    }

    /// <summary>Débite une dépense de sort abordable. Seul un échec de démarrage peut la rembourser.</summary>
    public bool TrySpendFishEndurance(float amount)
    {
        if (!isConfigured || State != FishingState.Active || !ValidAmount(amount) || amount > currentEndurance) return false;
        currentEndurance -= amount;
        return true;
    }

    /// <summary>Implémente le contrat de dépense de sorts.</summary>
    public bool TrySpendEndurance(float amount) { return TrySpendFishEndurance(amount); }

    /// <summary>Rembourse un démarrage échoué ; un épuisement déjà validé reste irréversible.</summary>
    public void RestoreEndurance(float amount)
    {
        if (!isConfigured || State != FishingState.Active || !ValidAmount(amount)) return;
        float maximum = exhaustionLatched || fishEscapeAI.IsExhaustionLatched
            ? fishDefinition.maxEndurance * fishDefinition.captureEnduranceRatio : fishDefinition.maxEndurance;
        currentEndurance = Mathf.Min(maximum, currentEndurance + amount);
    }

    /// <summary>Programme l'attraction physique du tourbillon sans répéter son impact élémentaire.</summary>
    public void ApplyWhirlwind(float duration, float pullAcceleration)
    {
        if (!isConfigured || State != FishingState.Active || !ValidAmount(duration) || !ValidAmount(pullAcceleration)) return;
        whirlwindTimeRemaining = Mathf.Max(whirlwindTimeRemaining, duration);
        whirlwindPullAcceleration = Mathf.Max(whirlwindPullAcceleration, pullAcceleration);
    }

    /// <summary>Met en attente un impact de faiblesse ; la puissance ATB a déjà été appliquée par le lanceur.</summary>
    public void ApplyElementalBreak(FishElementWeakness element, float amount)
    {
        if (!CanStartBreak || !ValidAmount(amount) || element == FishElementWeakness.None ||
            fishIdentity == null || fishIdentity.Identity == null || fishIdentity.Identity.elementalWeakness != element) return;
        pendingElementalPower = Mathf.Min(elementalBreakCapacity,
            pendingElementalPower + Mathf.Min(amount, maximumElementalPowerPerImpact));
    }

    /// <summary>Engage un seul break prioritaire ; les coûts spécifiques restent dans la session.</summary>
    public bool TryStartFishBreak(FishBreakCause cause, Vector3 knockbackDirection)
    {
        if (!CanStartBreak || !Finite(knockbackDirection)) return false;
        bool interruptedBurst = fishEscapeAI.IsBursting;
        if (!fishEscapeAI.TryEnterBreak(cause)) return false;
        LastFishBreakCause = cause;
        fishSpellcastingAI?.CancelCasting();
        if (cause == FishBreakCause.Shore)
        {
            ApplyFishEnduranceDamage(fishDefinition.maxEndurance * shoreEnduranceCostRatio);
            Vector3 desiredVelocity = Horizontal(knockbackDirection).normalized * shoreReboundSpeed;
            fishMovementController.QueueVelocityChange(desiredVelocity - fishMovementController.CurrentVelocity);
            shoreArmed = false;
            shoreCaptureRequiresExit = true;
            shoreCooldownUntil = combatTime + shoreCooldownDuration;
        }
        FishBreakStarted?.Invoke(cause);
        if (interruptedBurst) BurstInterrupted?.Invoke();
        return true;
    }

    /// <summary>Reçoit une collision pré-solveur et la déduplique avant traitement au prochain pas.</summary>
    public void NotifyEnvironmentImpact(FishingBreakObstacle obstacle, Vector3 contactNormal, float incomingNormalSpeed)
    {
        if (!CanStartBreak || obstacle == null || !ValidAmount(incomingNormalSpeed) || !Finite(contactNormal) ||
            incomingNormalSpeed < obstacle.MinimumImpactSpeed || pendingImpacts.Count >= MaximumPendingImpacts) return;
        Vector3 normal = Horizontal(contactNormal);
        if (normal.sqrMagnitude < Epsilon) return;
        for (int index = 0; index < pendingImpacts.Count; index++)
            if (pendingImpacts[index].obstacle == obstacle) return;
        pendingImpacts.Add(new EnvironmentImpact { obstacle = obstacle, normal = normal.normalized, speed = incomingNormalSpeed });
    }

    private void ProcessPendingImpacts()
    {
        if (CanStartBreak && pendingElementalPower > 0f)
        {
            elementalBreakGauge = Mathf.Min(elementalBreakCapacity, elementalBreakGauge + pendingElementalPower);
            if (elementalBreakGauge >= elementalBreakCapacity && TryStartFishBreak(FishBreakCause.Elemental, Vector3.zero))
                elementalBreakGauge = 0f;
        }
        pendingElementalPower = 0f;
        for (int index = 0; index < pendingImpacts.Count; index++)
        {
            EnvironmentImpact impact = pendingImpacts[index];
            FishingBreakObstacle obstacle = impact.obstacle;
            if (!CanStartBreak || obstacle == null || impact.speed < obstacle.MinimumImpactSpeed) continue;
            if (obstacleLocks.TryGetValue(obstacle, out ObstacleLock obstacleLock) &&
                (!obstacleLock.separated || combatTime < obstacleLock.until)) continue;
            float directedContribution = 0f;
            for (int sampleIndex = 0; sampleIndex < influenceCount; sampleIndex++)
            {
                InfluenceSample sample = influenceHistory[(influenceHead - 1 - sampleIndex + InfluenceCapacity) % InfluenceCapacity];
                if (combatTime - sample.time > playerInfluenceWindow) break;
                directedContribution += Vector3.Dot(sample.velocityChange, -impact.normal);
            }
            if (directedContribution < minimumDirectedVelocityContribution ||
                !TryStartFishBreak(FishBreakCause.Environment, impact.normal)) continue;
            ApplyFishEnduranceDamage(fishDefinition.maxEndurance * obstacle.EnduranceCostRatio);
            fishMovementController.QueueVelocityChange(impact.normal * obstacle.KnockbackSpeed);
            if (!obstacleLocks.ContainsKey(obstacle)) obstacleKeys.Add(obstacle);
            obstacleLocks[obstacle] = new ObstacleLock { until = combatTime + obstacle.Cooldown, separated = false };
        }
        pendingImpacts.Clear();
    }

    private void UpdateObstacleSeparation()
    {
        for (int index = obstacleKeys.Count - 1; index >= 0; index--)
        {
            FishingBreakObstacle obstacle = obstacleKeys[index];
            if (obstacle == null)
            {
                obstacleLocks.Remove(obstacle);
                obstacleKeys.RemoveAt(index);
                continue;
            }
            ObstacleLock obstacleLock = obstacleLocks[obstacle];
            if (!obstacleLock.separated && collisionRelay != null && !collisionRelay.IsTouching(obstacle))
            {
                obstacleLock.separated = true;
                obstacleLocks[obstacle] = obstacleLock;
            }
        }
    }

    private void ProcessShoreContact(Vector3 position)
    {
        Bounds zone = catchZone.bounds;
        bool inside = InsideHorizontal(zone, position, 0f);
        bool crossing = !InsideHorizontal(zone, previousFishPosition, 0f) &&
            CrossesHorizontal(zone, previousFishPosition, position);
        if (!inside) shoreCaptureRequiresExit = false;
        if (!InsideHorizontal(zone, position, shoreRearmMargin) && combatTime >= shoreCooldownUntil && CanStartBreak)
            shoreArmed = true;
        if (!inside && !crossing) return;
        if (CanCatchFish && !shoreCaptureRequiresExit)
        {
            EndSession(FishingState.Caught);
            return;
        }
        if (!shoreArmed || !CanStartBreak || NormalizedEndurance <= fishDefinition.captureEnduranceRatio) return;
        Vector3 inward = Horizontal(waterBounds.bounds.center - zone.center);
        if (inward.sqrMagnitude < Epsilon) inward = Horizontal(waterBounds.bounds.center - position);
        if (inward.sqrMagnitude < Epsilon) inward = Vector3.forward;
        TryStartFishBreak(FishBreakCause.Shore, inward.normalized);
    }

    private void ApplyForces(float pull, float lateral, float pullMultiplier, float lateralMultiplier,
        Vector3 playerVelocity, Vector3 directionToPlayer, Vector3 escapeDirection,
        float effectiveReelForce, float deltaTime, out float resistance, out float exertedPull)
    {
        Vector3 velocity = fishMovementController.CurrentVelocity;
        Vector3 tangent = Vector3.Cross(Vector3.up, -directionToPlayer);
        float pressure = fishEscapeAI.IsBursting ? 1f : Mathf.Lerp(fishDefinition.idleForceMultiplier, 1f, pull);
        Vector3 fishForce = Horizontal(escapeDirection) * fishDefinition.escapeForce * pressure * fishEscapeAI.EffortMultiplier;
        float radialFishForce = Vector3.Dot(fishForce, -directionToPlayer);
        Vector3 fishTangent = (fishForce + directionToPlayer * radialFishForce) * fishLateralMovementRatio;
        float opposition = Mathf.Max(0f, radialFishForce);
        float cap = CurrentFishState == FishCombatState.Break ? fishBreakResistanceRatio :
            CurrentFishState == FishCombatState.Recovery || CurrentFishState == FishCombatState.CastSpell ||
            CurrentFishState == FishCombatState.BurstWindup ? openingResistanceRatio : maxFishResistanceWhilePulling;
        float statePull = CurrentFishState == FishCombatState.ShoreRebound ? shorePullAuthority :
            CurrentFishState == FishCombatState.Break ? fishBreakPullMultiplier : 1f;
        float desiredApproachSpeed = targetApproachSpeed * pull * pullMultiplier * statePull;
        float radialAcceleration = (desiredApproachSpeed - Vector3.Dot(velocity, directionToPlayer)) / guidanceResponseTime;
        radialAcceleration += Mathf.Max(0f, fishDefinition.drag) * desiredApproachSpeed;
        float maximumReel = effectiveReelForce * pull * statePull;
        // Résout R - min(opposition, cap * R) = accélération nette demandée.
        // Compenser le plafond maximal plutôt que le plafond réellement appliqué ferait dépasser la vitesse cible.
        float requestedNetAcceleration = Mathf.Max(0f, radialAcceleration + Mathf.Min(0f, radialFishForce));
        float resistanceCompensation = fishEscapeAI.IsBursting ? opposition :
            Mathf.Min(opposition, requestedNetAcceleration * cap / Mathf.Max(Epsilon, 1f - cap));
        float playerReel = pull > Epsilon ? Mathf.Clamp(requestedNetAcceleration + resistanceCompensation, 0f, maximumReel) : 0f;
        resistance = fishEscapeAI.IsBursting || pull <= Epsilon ? opposition : Mathf.Min(opposition, playerReel * cap);
        // Un seul terme radial : la mesure de résistance ne sera jamais réajoutée comme force.
        float appliedRadialFish = radialFishForce > 0f ? resistance : radialFishForce;
        Vector3 autonomousAcceleration = -directionToPlayer * appliedRadialFish + fishTangent;

        float movementRequest = Vector3.Dot(playerVelocity, tangent) * playerMovementGuidanceWeight;
        float targetLateralSpeed = Mathf.Clamp(lateral * maximumLateralSpeed * lateralMultiplier + movementRequest,
            -maximumLateralSpeed, maximumLateralSpeed);
        float lateralAcceleration = 0f;
        if (Mathf.Abs(targetLateralSpeed) > Epsilon)
        {
            lateralAcceleration = (targetLateralSpeed - Vector3.Dot(velocity, tangent)) / guidanceResponseTime;
            lateralAcceleration += Mathf.Max(0f, fishDefinition.drag) * targetLateralSpeed;
            float authority = fishEscapeAI.IsBursting ? burstGuidanceAuthority : 1f;
            lateralAcceleration = Mathf.Clamp(lateralAcceleration, -maximumGuidanceAcceleration, maximumGuidanceAcceleration) * authority;
        }
        Vector3 playerAcceleration = directionToPlayer * playerReel + tangent * lateralAcceleration;
        if (whirlwindTimeRemaining > 0f)
        {
            playerAcceleration += directionToPlayer * whirlwindPullAcceleration;
            whirlwindTimeRemaining = Mathf.Max(0f, whirlwindTimeRemaining - deltaTime);
        }
        Vector3 totalAcceleration = autonomousAcceleration + playerAcceleration;
        fishMovementController.SetAcceleration(totalAcceleration);
        float damping = 1f + Mathf.Max(0f, fishDefinition.drag) * deltaTime;
        Vector3 withPlayer = Vector3.ClampMagnitude((velocity + totalAcceleration * deltaTime) / damping, fishDefinition.maxSpeed);
        Vector3 withoutPlayer = Vector3.ClampMagnitude((velocity + autonomousAcceleration * deltaTime) / damping, fishDefinition.maxSpeed);
        influenceHistory[influenceHead] = new InfluenceSample { time = combatTime, velocityChange = withPlayer - withoutPlayer };
        influenceHead = (influenceHead + 1) % InfluenceCapacity;
        influenceCount = Mathf.Min(InfluenceCapacity, influenceCount + 1);
        exertedPull = playerReel > Epsilon ? pull : 0f;
    }

    private void UpdateEndurance(float exertedPull, float pullMultiplier, float resistance, float effectiveReelForce, float deltaTime)
    {
        LatchExhaustion();
        bool isBreak = CurrentFishState == FishCombatState.Break;
        float drain = 0f;
        if (exertedPull > Epsilon)
        {
            float ratio = Mathf.Lerp(MinimumStruggleDrainRatio, 1f, Mathf.Clamp01(resistance / Mathf.Max(Epsilon, effectiveReelForce)));
            drain = exertedPull * ratio * enduranceDrainPerSecond * fishDefinition.enduranceDrainMultiplier * pullMultiplier;
            if (fishEscapeAI.IsBursting) drain *= BurstStruggleDrainMultiplier;
            if (isBreak) drain *= fishBreakPullDrainMultiplier;
        }
        else if (!IsFishBreakSequence && !exhaustionLatched && !fishEscapeAI.IsExhaustionLatched)
            currentEndurance += fishDefinition.enduranceRecoveryPerSecond * deltaTime;
        if (isBreak) drain += fishDefinition.maxEndurance * fishBreakAutonomousDrainRatio;
        currentEndurance = Mathf.Clamp(currentEndurance - drain * deltaTime, 0f, fishDefinition.maxEndurance);
        LatchExhaustion();
    }

    private void UpdateTension(float pull, float release, float lateral, float releaseMultiplier,
        float resistance, float effectiveReelForce, Vector3 fishPosition, float deltaTime)
    {
        float load = pull > Epsilon ? Mathf.Clamp01(resistance / Mathf.Max(Epsilon, effectiveReelForce)) : 0f;
        float gain = pull * Mathf.Lerp(MinimumTensionLoad, 1f, load) * pullTensionRate + Mathf.Abs(lateral) * lateralTensionRate;
        if (fishEscapeAI.IsBursting) gain += pull * burstTensionRate;
        float distance = GetHorizontalLineDistance(fishPosition);
        float stretch = Mathf.Clamp(distance - referenceLineDistance - lineDistanceTolerance, 0f, maximumStretchForTension);
        float slack = Mathf.Max(0f, referenceLineDistance - distance - lineDistanceTolerance);
        gain = (gain + stretch * distanceTensionRate) * fishDefinition.tensionGainMultiplier;
        float recovery = release * releaseTensionRecovery * releaseMultiplier + slack * distanceSlackRecoveryRate;
        if (pull <= Epsilon && !fishEscapeAI.IsBursting && Mathf.Abs(lateral) <= Epsilon) recovery += idleTensionRecovery;
        normalizedTension = Mathf.Clamp01(normalizedTension + (gain - recovery) * deltaTime);
    }

    private void HandleFishStateChanged(FishCombatState newState)
    {
        if (observedFishState == FishCombatState.Break && newState != FishCombatState.Break)
            fishBreakImmuneUntil = combatTime + fishBreakImmunityDuration;
        observedFishState = newState;
    }

    private void LatchExhaustion() { exhaustionLatched |= NormalizedEndurance <= fishDefinition.captureEnduranceRatio; }
    private float GetHorizontalLineDistance(Vector3 position) { return Horizontal(playerTransform.position - position).magnitude; }

    private void UpdateBreakTimer(float deltaTime)
    {
        if (normalizedTension >= breakThreshold)
        {
            breakTimer += deltaTime;
            if (breakTimer >= breakDuration) EndSession(FishingState.Escaped);
        }
        else breakTimer = Mathf.Max(0f, breakTimer - deltaTime * breakTimerDecayMultiplier);
    }

    private static bool InsideHorizontal(Bounds bounds, Vector3 point, float margin)
    {
        return point.x >= bounds.min.x - margin && point.x <= bounds.max.x + margin &&
            point.z >= bounds.min.z - margin && point.z <= bounds.max.z + margin;
    }

    private static bool CrossesHorizontal(Bounds bounds, Vector3 start, Vector3 end)
    {
        float enter = 0f;
        float exit = 1f;
        return ClipAxis(start.x, end.x - start.x, bounds.min.x, bounds.max.x, ref enter, ref exit) &&
            ClipAxis(start.z, end.z - start.z, bounds.min.z, bounds.max.z, ref enter, ref exit);
    }

    private static bool ClipAxis(float origin, float direction, float minimum, float maximum, ref float enter, ref float exit)
    {
        if (Mathf.Abs(direction) < Epsilon) return origin >= minimum && origin <= maximum;
        float first = (minimum - origin) / direction;
        float last = (maximum - origin) / direction;
        if (first > last) { float swap = first; first = last; last = swap; }
        enter = Mathf.Max(enter, first); exit = Mathf.Min(exit, last);
        return enter <= exit;
    }

    private void OrientPlayerTowardsFish()
    {
        Vector3 direction = Horizontal(fishMovementController.Position - playerTransform.position);
        if (direction.sqrMagnitude > Epsilon) playerTransform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private bool ValidateConfiguration()
    {
        bool valid = playerTransform != null && fishDefinition != null && fishingInputReader != null &&
            fishEscapeAI != null && fishMovementController != null && waterBounds != null && catchZone != null;
        if (!valid && !hasLoggedConfigurationWarning)
        {
            Debug.LogWarning($"{nameof(FishingSessionController)} on '{name}' has missing combat references.", this);
            hasLoggedConfigurationWarning = true;
        }
        return valid;
    }

    private void EndSession(FishingState finalState)
    {
        if (State != FishingState.Active) return;
        fishMovementController?.Stop();
        fishEscapeAI?.StopCombat();
        fishSpellcastingAI?.CancelCasting();
        collisionRelay?.ResetContacts();
        pendingImpacts.Clear(); pendingElementalPower = 0f;
        whirlwindTimeRemaining = whirlwindPullAcceleration = 0f;
        influenceHead = influenceCount = 0;
        pullBoostTracker.Reset(); releaseBoostTracker.Reset(); lateralBoostTracker.Reset();
        SetState(finalState);
        if (fishingLinePresenter != null) fishingLinePresenter.SetLineTension(normalizedTension);
    }

    private void OnDisable()
    {
        if (isConfigured && State == FishingState.Active) EndSession(FishingState.Escaped);
        if (fishEscapeAI != null) fishEscapeAI.StateChanged -= HandleFishStateChanged;
    }

    private void SetState(FishingState newState)
    {
        if (State == newState) return;
        State = newState;
        StateChanged?.Invoke(State);
    }

    private static Vector3 Horizontal(Vector3 vector) { vector.y = 0f; return vector; }
    private static bool ValidAmount(float value) { return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value); }
    private static bool Finite(Vector3 value) { return ValidAmount(Mathf.Abs(value.x)) && ValidAmount(Mathf.Abs(value.y)) && ValidAmount(Mathf.Abs(value.z)); }

    private static float NonNegativeFinite(float value) { return ValidAmount(value) ? value : 0f; }

    private void OnValidate()
    {
        reelForce = NonNegativeFinite(reelForce);
        maxFishResistanceWhilePulling = Mathf.Clamp01(NonNegativeFinite(maxFishResistanceWhilePulling));
        openingResistanceRatio = Mathf.Clamp01(NonNegativeFinite(openingResistanceRatio));
        fishBreakResistanceRatio = Mathf.Clamp01(NonNegativeFinite(fishBreakResistanceRatio));
        fishLateralMovementRatio = Mathf.Clamp01(NonNegativeFinite(fishLateralMovementRatio));
        guidanceResponseTime = Mathf.Max(Epsilon, NonNegativeFinite(guidanceResponseTime));
        maximumLateralSpeed = NonNegativeFinite(maximumLateralSpeed);
        maximumGuidanceAcceleration = NonNegativeFinite(maximumGuidanceAcceleration);
        playerMovementGuidanceWeight = NonNegativeFinite(playerMovementGuidanceWeight);
        burstGuidanceAuthority = Mathf.Clamp(NonNegativeFinite(burstGuidanceAuthority), MinimumBurstGuidanceAuthority, 1f);
        targetApproachSpeed = NonNegativeFinite(targetApproachSpeed);
        fishBreakPullMultiplier = Mathf.Max(1f, NonNegativeFinite(fishBreakPullMultiplier));
        shorePullAuthority = Mathf.Clamp01(NonNegativeFinite(shorePullAuthority));
        enduranceDrainPerSecond = NonNegativeFinite(enduranceDrainPerSecond);
        fishBreakAutonomousDrainRatio = Mathf.Clamp01(NonNegativeFinite(fishBreakAutonomousDrainRatio));
        fishBreakPullDrainMultiplier = Mathf.Max(1f, NonNegativeFinite(fishBreakPullDrainMultiplier));
        elementalBreakCapacity = Mathf.Max(1f, NonNegativeFinite(elementalBreakCapacity));
        maximumElementalPowerPerImpact = NonNegativeFinite(maximumElementalPowerPerImpact);
        shoreEnduranceCostRatio = Mathf.Clamp01(NonNegativeFinite(shoreEnduranceCostRatio));
        shoreReboundSpeed = NonNegativeFinite(shoreReboundSpeed);
        shoreRearmMargin = NonNegativeFinite(shoreRearmMargin);
        playerInfluenceWindow = Mathf.Clamp(NonNegativeFinite(playerInfluenceWindow), MinimumInfluenceWindow, MaximumInfluenceWindow);
        minimumDirectedVelocityContribution = NonNegativeFinite(minimumDirectedVelocityContribution);
        breakDuration = Mathf.Max(Epsilon, NonNegativeFinite(breakDuration));
        shoreCooldownDuration = NonNegativeFinite(shoreCooldownDuration);
        fishBreakImmunityDuration = NonNegativeFinite(fishBreakImmunityDuration);
    }
}

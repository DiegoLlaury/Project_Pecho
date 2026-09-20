using System;
using UnityEngine;

public class FishingSessionController : MonoBehaviour, ISpendableEndurance
{
    private const float Epsilon = 0.001f;
    private const float MinimumStruggleDrainRatio = 0.35f;
    private const float BurstStruggleDrainMultiplier = 1.25f;
    private const float ShoreBurstEnduranceCostRatio = 0.25f;
    private const float DefaultLineDistanceTolerance = 1f;

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

    [Header("Player")]
    [Tooltip("Accélération maximale de traction du joueur.")]
    [SerializeField, Min(0f)] private float reelForce = 12f;

    [Tooltip("Force latérale du joueur.")]
    [SerializeField, Min(0f)] private float lateralForce = 6f;

    [Tooltip(
        "Résistance maximale que le poisson peut réellement opposer pendant que le joueur tire. " +
        "0.9 signifie que le joueur reste toujours légèrement plus fort."
    )]
    [SerializeField, Range(0.5f, 1f)]
    private float maxFishResistanceWhilePulling = 0.9f;

    [Tooltip(
        "Part de la force du poisson utilisée pour son déplacement latéral. " +
        "Le reste sert au duel joueur/poisson."
    )]
    [SerializeField, Range(0f, 1f)]
    private float fishLateralMovementRatio = 0.35f;

    [Header("Tension")]
    [SerializeField, Min(0f)]
    private float pullTensionRate = 0.55f;

    [SerializeField, Min(0f)]
    private float releaseTensionRecovery = 0.9f;

    [SerializeField, Min(0f)]
    private float idleTensionRecovery = 0.3f;

    [SerializeField, Range(0.5f, 1f)]
    private float breakThreshold = 1f;

    [SerializeField, Min(0.1f)]
    private float breakDuration = 2.5f;

    [SerializeField, Min(0f)]
    private float breakTimerDecayMultiplier = 2f;

    [SerializeField, Min(0f)]
    private float lateralTensionRate = 0.12f;

    [Tooltip("Surtension appliquée lorsqu'une traction est maintenue pendant une ruée.")]
    [SerializeField, Min(0f)]
    private float burstTensionRate = 0.4f;

    [Header("Line Distance")]
    [Tooltip("Tolérance en mètres avant que l'éloignement du joueur tende la ligne.")]
    [SerializeField, Min(0f)] private float lineDistanceTolerance = DefaultLineDistanceTolerance;

    [Tooltip("Tension ajoutée par seconde et par mètre lorsque la ligne est trop étirée.")]
    [SerializeField, Min(0f)] private float distanceTensionRate = 0.08f;

    [Tooltip("Allongement maximal pris en compte dans le calcul de tension.")]
    [SerializeField, Min(0f)] private float maximumStretchForTension = 4f;

    [Tooltip("Détente appliquée lorsque le joueur réduit la distance avec le poisson.")]
    [SerializeField, Min(0f)] private float distanceSlackRecoveryRate = 0.06f;

    [Header("Endurance")]
    [SerializeField, Min(0f)]
    private float enduranceDrainPerSecond = 12f;

    private float currentEndurance;
    private float normalizedTension;
    private float whirlwindTimeRemaining;
    private float whirlwindPullAcceleration;

    private float breakTimer;
    private float referenceLineDistance;

    private bool isConfigured;
    private bool hasLoggedConfigurationWarning;
    private readonly ATBContinuousBoostTracker pullBoostTracker = new ATBContinuousBoostTracker();
    private readonly ATBContinuousBoostTracker releaseBoostTracker = new ATBContinuousBoostTracker();
    private readonly ATBContinuousBoostTracker lateralBoostTracker = new ATBContinuousBoostTracker();

    public FishingState State { get; private set; } = FishingState.Active;

    public float CurrentEndurance => currentEndurance;

    public float NormalizedTension => normalizedTension;

    public float NormalizedEndurance =>
        fishDefinition == null
            ? 0f
            : Mathf.Clamp01(
                currentEndurance / Mathf.Max(Epsilon, fishDefinition.maxEndurance));

    public float BreakProgress =>
        Mathf.Clamp01(breakTimer / breakDuration);

    public event Action<FishingState> StateChanged;
    public event Action BurstInterrupted;

    private void Awake()
    {
        if (atbTimingController == null)
        {
            atbTimingController = FindFirstObjectByType<ATBTimingController>();
        }
    }

    private void Start()
    {
        ResetSession();
    }

    private void FixedUpdate()
    {
        if (!isConfigured || State != FishingState.Active)
        {
            return;
        }

        float deltaTime = Time.fixedDeltaTime;

        float pull = Mathf.Clamp01(fishingInputReader.PullInput);
        float release = Mathf.Clamp01(fishingInputReader.ReleaseInput);
        float lateral = Mathf.Clamp(
            fishingInputReader.LateralInput,
            -1f,
            1f);

        float pullActionMultiplier = pullBoostTracker.Evaluate(
            pull > Epsilon,
            atbTimingController);
        float releaseActionMultiplier = releaseBoostTracker.Evaluate(
            release > Epsilon,
            atbTimingController);
        float lateralActionMultiplier = lateralBoostTracker.Evaluate(
            Mathf.Abs(lateral) > Epsilon,
            atbTimingController);

        fishEscapeAI.Tick(
            deltaTime,
            NormalizedEndurance);
        fishSpellcastingAI.Tick(
            deltaTime,
            NormalizedEndurance);

        Vector3 fishPosition = fishMovementController.Position;

        fishEscapeAI.TryTriggerShoreBurst(fishPosition, NormalizedEndurance);

        if (fishEscapeAI.ConsumeShoreBurstCost())
        {
            currentEndurance = Mathf.Max(
                0f,
                currentEndurance -
                fishDefinition.maxEndurance *
                ShoreBurstEnduranceCostRatio);
        }


        Vector3 directionToPlayer =
            playerTransform.position - fishPosition;

        directionToPlayer.y = 0f;

        if (directionToPlayer.sqrMagnitude < Epsilon)
        {
            return;
        }

        directionToPlayer.Normalize();

        Vector3 awayFromPlayer = -directionToPlayer;

        Vector3 escapeDirection =
            fishEscapeAI.GetEscapeDirection(
                fishPosition,
                playerTransform.position);

        float fishForce = ComputeFishForce(pull);

        float effectiveReelForce =
            reelForce *
            pullActionMultiplier *
            (1f - Mathf.Clamp01(fishDefinition.resistanceToPull));

        float fishResistanceForce =
            ComputeFishResistanceForce(
                fishForce,
                escapeDirection,
                awayFromPlayer,
                pull,
                effectiveReelForce);

        ApplyForces(
            pull,
            lateral,
            lateralActionMultiplier,
            directionToPlayer,
            escapeDirection,
            effectiveReelForce,
            fishForce,
            fishResistanceForce);

        UpdateEndurance(
            pull,
            pullActionMultiplier,
            fishResistanceForce,
            effectiveReelForce,
            deltaTime);

        UpdateTension(
            pull,
            release,
            lateral,
            releaseActionMultiplier,
            fishResistanceForce,
            effectiveReelForce,
            fishPosition,
            deltaTime);

        TryCatchFish();

        if (State != FishingState.Active)
        {
            return;
        }

        UpdateBreakTimer(deltaTime);
    }

    private void Update()
    {
        if (!isConfigured || State != FishingState.Active)
        {
            return;
        }

        OrientPlayerTowardsFish();
        fishingLinePresenter.SetLineTension(normalizedTension);
    }

    public void ResetSession()
    {
        isConfigured = ValidateConfiguration();

        if (!isConfigured)
        {
            enabled = false;
            return;
        }

        currentEndurance = fishDefinition.maxEndurance;
        normalizedTension = 0f;
        breakTimer = 0f;
        pullBoostTracker.Reset();
        releaseBoostTracker.Reset();
        lateralBoostTracker.Reset();

        fishMovementController.Stop();

        fishMovementController.Configure(
            fishDefinition,
            waterBounds);

        referenceLineDistance = GetHorizontalLineDistance(
            fishMovementController.Position);

        fishEscapeAI.Configure(
            fishDefinition,
            waterBounds);
        fishSpellcastingAI.Configure(playerTransform, this);

        fishingLinePresenter.Configure(
            rodTip,
            fishMovementController.transform);

        fishingLinePresenter.SetLineTension(0f);

        SetState(FishingState.Active);
    }

    /// <summary>Ajoute immédiatement une surtension normalisée à la ligne.</summary>
    public void ApplyTensionSpike(float normalizedAmount)
    {
        if (State != FishingState.Active)
        {
            return;
        }

        normalizedTension = Mathf.Clamp01(normalizedTension + Mathf.Max(0f, normalizedAmount));
    }

    /// <summary>Retire de l'endurance au poisson et interrompt sa ruée si elle est active.</summary>
    public void ApplyFishEnduranceDamage(float amount)
    {
        float clampedAmount = Mathf.Max(0f, amount);
        if (clampedAmount <= 0f || State != FishingState.Active)
        {
            return;
        }

        currentEndurance = Mathf.Clamp(
            currentEndurance - clampedAmount,
            0f,
            fishDefinition != null ? fishDefinition.maxEndurance : 0f);

        if (fishEscapeAI != null && fishEscapeAI.TryInterruptBurst())
        {
            BurstInterrupted?.Invoke();
        }
    }

    /// <summary>Débite l'endurance du poisson si la session est active et si le solde couvre le coût.</summary>
    public bool TrySpendFishEndurance(float amount)
    {
        float clampedAmount = Mathf.Max(0f, amount);
        if (State != FishingState.Active || clampedAmount > currentEndurance)
        {
            return false;
        }

        currentEndurance -= clampedAmount;
        return true;
    }

    /// <summary>Débite l'endurance via le contrat de ressource utilisé par l'IA.</summary>
    public bool TrySpendEndurance(float amount)
    {
        return TrySpendFishEndurance(amount);
    }

    /// <summary>Restaure l'endurance du poisson sans dépasser sa capacité maximale.</summary>
    public void RestoreEndurance(float amount)
    {
        float maximumEndurance = fishDefinition != null ? fishDefinition.maxEndurance : 0f;
        currentEndurance = Mathf.Min(
            maximumEndurance,
            currentEndurance + Mathf.Max(0f, amount));
    }

    /// <summary>Applique une attraction temporaire du poisson vers le joueur.</summary>
    public void ApplyWhirlwind(float duration, float pullAcceleration)
    {
        whirlwindTimeRemaining = Mathf.Max(whirlwindTimeRemaining, Mathf.Max(0f, duration));
        whirlwindPullAcceleration = Mathf.Max(0f, pullAcceleration);
    }


    private bool ValidateConfiguration()
    {
        bool valid =
            playerTransform != null &&
            rodTip != null &&
            fishDefinition != null &&
            fishingInputReader != null &&
            fishEscapeAI != null &&
            fishSpellcastingAI != null &&
            fishMovementController != null &&
            waterBounds != null &&
            catchZone != null &&
            fishingLinePresenter != null;

        if (!valid && !hasLoggedConfigurationWarning)
        {
            Debug.LogWarning(
                $"{nameof(FishingSessionController)} on '{name}' " +
                "has missing references.",
                this);

            hasLoggedConfigurationWarning = true;
        }

        return valid;
    }

    private float ComputeFishForce(float pull)
    {
        if (currentEndurance <= Epsilon)
        {
            return 0f;
        }

        float enduranceFactor = NormalizedEndurance;
        float fatigueForceMultiplier = Mathf.Lerp(
            fishDefinition.exhaustedForceMultiplier,
            1f,
            enduranceFactor);

        float pressureFactor =
            fishEscapeAI.IsBursting
                ? 1f
                : Mathf.Lerp(
                    fishDefinition.idleForceMultiplier,
                    1f,
                    pull);

        float force =
            fishDefinition.escapeForce *
            fatigueForceMultiplier *
            pressureFactor *
            fishEscapeAI.EffortMultiplier *
            fishEscapeAI.EnduranceEscapeMultiplier;

        return Mathf.Max(0f, force);
    }

    private float ComputeFishResistanceForce(
        float fishForce,
        Vector3 escapeDirection,
        Vector3 awayFromPlayer,
        float pull,
        float effectiveReelForce)
    {
        /*
         * Seule la composante du déplacement du poisson
         * qui s'oppose réellement à la traction fatigue le joueur.
         */
        float escapeAlongLine =
            Mathf.Max(
                0f,
                Vector3.Dot(
                    escapeDirection,
                    awayFromPlayer));

        float resistanceForce =
            fishForce * escapeAlongLine;

        /*
         * Le joueur doit toujours rester légèrement plus fort
         * pendant la traction.
         */
        if (pull > Epsilon)
        {
            resistanceForce = Mathf.Min(
                resistanceForce,
                effectiveReelForce *
                maxFishResistanceWhilePulling);
        }

        return resistanceForce;
    }

    private void ApplyForces(
    float pull,
    float lateral,
    float lateralActionMultiplier,
    Vector3 directionToPlayer,
    Vector3 escapeDirection,
    float effectiveReelForce,
    float fishForce,
    float fishResistanceForce)
    {
        Vector3 awayFromPlayer = -directionToPlayer;

        float playerReelAcceleration =
            pull * effectiveReelForce;

        float netReelAcceleration =
            playerReelAcceleration -
            fishResistanceForce;

        Vector3 playerReelForce =
            directionToPlayer *
            netReelAcceleration;

        Vector3 fishEscapeForce =
            escapeDirection *
            fishForce;

        Vector3 fishRetreatForce =
            Vector3.Project(
                fishEscapeForce,
                awayFromPlayer);

        Vector3 fishLateralEscapeForce =
            fishEscapeForce -
            fishRetreatForce;

        fishEscapeForce =
            fishRetreatForce +
            fishLateralEscapeForce *
            fishLateralMovementRatio;

        Vector3 playerRight =
            Vector3.Cross(
                Vector3.up,
                -directionToPlayer);

        float playerLateralAcceleration =
            lateral *
            lateralForce *
            lateralActionMultiplier *
            (1f - fishDefinition.lateralResistance);

        Vector3 playerLateralForce =
            playerRight *
            playerLateralAcceleration;

        Vector3 totalAcceleration =
            playerReelForce +
            fishEscapeForce +
            playerLateralForce;

        if (whirlwindTimeRemaining > 0f)
        {
            totalAcceleration += directionToPlayer * whirlwindPullAcceleration;
            whirlwindTimeRemaining = Mathf.Max(0f, whirlwindTimeRemaining - Time.fixedDeltaTime);
        }

        fishMovementController.SetAcceleration(
            totalAcceleration);
    }


    private void UpdateEndurance(
    float pull,
    float pullActionMultiplier,
    float fishResistanceForce,
    float effectiveReelForce,
    float deltaTime)
    {
        if (pull > Epsilon)
        {
            float resistance01 = Mathf.Clamp01(
                fishResistanceForce /
                Mathf.Max(Epsilon, effectiveReelForce));

            float struggleDrainRatio = Mathf.Lerp(
                MinimumStruggleDrainRatio,
                1f,
                resistance01);

            float burstDrainMultiplier =
                fishEscapeAI.IsBursting
                    ? BurstStruggleDrainMultiplier
                    : 1f;

            currentEndurance -=
                pull *
                struggleDrainRatio *
                burstDrainMultiplier *
                enduranceDrainPerSecond *
                fishDefinition.enduranceDrainMultiplier *
                pullActionMultiplier *
                deltaTime;
        }
        else
        {
            currentEndurance +=
                fishDefinition.enduranceRecoveryPerSecond *
                deltaTime;
        }

        currentEndurance = Mathf.Clamp(
            currentEndurance,
            0f,
            fishDefinition.maxEndurance);
    }


    private void UpdateTension(
        float pull,
        float release,
        float lateral,
        float releaseActionMultiplier,
        float fishResistanceForce,
        float effectiveReelForce,
        Vector3 fishPosition,
        float deltaTime)
    {
        bool isBursting =
            fishEscapeAI.IsBursting;

        float load01 =
            pull > Epsilon
                ? Mathf.Clamp01(
                    fishResistanceForce /
                    Mathf.Max(Epsilon, effectiveReelForce))
                : 0f;

        /*
         * Même avec un poisson faible, tirer produit un peu de tension.
         * Plus le poisson résiste, plus la tension grimpe vite.
         */
        float gain =
            pull *
            Mathf.Lerp(
                0.15f,
                1f,
                load01) *
            pullTensionRate;

        gain +=
            Mathf.Abs(lateral) *
            lateralTensionRate;

        if (isBursting)
        {
            gain += pull * burstTensionRate;
        }

        float currentLineDistance =
            GetHorizontalLineDistance(fishPosition);

        float stretchedDistance = Mathf.Clamp(
            currentLineDistance -
            referenceLineDistance -
            lineDistanceTolerance,
            0f,
            maximumStretchForTension);

        float slackDistance = Mathf.Max(
            0f,
            referenceLineDistance -
            currentLineDistance -
            lineDistanceTolerance);

        gain += stretchedDistance * distanceTensionRate;

        gain *=
            fishDefinition.tensionGainMultiplier;

        float recovery =
            release *
            releaseTensionRecovery *
            releaseActionMultiplier +
            slackDistance *
            distanceSlackRecoveryRate;

        if (pull <= Epsilon &&
            !isBursting &&
            Mathf.Abs(lateral) <= Epsilon)
        {
            recovery +=
                idleTensionRecovery;
        }

        normalizedTension =
            Mathf.Clamp01(
                normalizedTension +
                (gain - recovery) *
                deltaTime);
    }

    private float GetHorizontalLineDistance(Vector3 fishPosition)
    {
        Vector3 playerPosition = playerTransform.position;
        playerPosition.y = 0f;
        fishPosition.y = 0f;

        return Vector3.Distance(playerPosition, fishPosition);
    }

    private void UpdateBreakTimer(float deltaTime)
    {
        if (normalizedTension >= breakThreshold)
        {
            breakTimer += deltaTime;

            if (breakTimer >= breakDuration)
            {
                EndSession(
                    FishingState.Escaped);
            }

            return;
        }

        breakTimer = Mathf.Max(
            0f,
            breakTimer -
            deltaTime *
            breakTimerDecayMultiplier);
    }

    private void TryCatchFish()
    {
        if (currentEndurance > 0f)
        {
            return;
        }

        if (!IsFishInsideCatchZone())
        {
            return;
        }

        EndSession(FishingState.Caught);
    }

    private bool IsFishInsideCatchZone()
    {
        Bounds zone = catchZone.bounds;
        Vector3 position =
            fishMovementController.Position;

        return position.x >= zone.min.x &&
               position.x <= zone.max.x &&
               position.z >= zone.min.z &&
               position.z <= zone.max.z;
    }

    private void OrientPlayerTowardsFish()
    {
        Vector3 directionToFish =
            fishMovementController.transform.position -
            playerTransform.position;

        directionToFish.y = 0f;

        if (directionToFish.sqrMagnitude < Epsilon)
        {
            return;
        }

        playerTransform.rotation =
            Quaternion.LookRotation(
                directionToFish.normalized,
                Vector3.up);
    }

    private void EndSession(
        FishingState finalState)
    {
        if (State != FishingState.Active)
        {
            return;
        }

        fishMovementController.Stop();

        SetState(finalState);

        fishingLinePresenter.SetLineTension(
            normalizedTension);
    }

    private void SetState(FishingState newState)
    {
        if (State == newState)
        {
            return;
        }

        State = newState;
        StateChanged?.Invoke(State);
    }
}
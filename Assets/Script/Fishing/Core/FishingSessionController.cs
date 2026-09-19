using System;
using UnityEngine;

public sealed class FishingSessionController : MonoBehaviour
{
    private const float Epsilon = 0.001f;
    private const float ResistanceRecoveryThresholdRatio = 0.35f;

    [Header("Required References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform rodTip;
    [SerializeField] private FishDefinition fishDefinition;
    [SerializeField] private FishingInputReader fishingInputReader;
    [SerializeField] private FishEscapeAI fishEscapeAI;
    [SerializeField] private FishMovementController fishMovementController;
    [SerializeField] private BoxCollider waterBounds;
    [SerializeField] private BoxCollider catchZone;
    [SerializeField] private FishingLinePresenter fishingLinePresenter;

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
    private float burstTensionRate = 0.3f;

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

    [Header("Endurance")]
    [SerializeField, Min(0f)]
    private float enduranceDrainPerSecond = 12f;

    private float currentEndurance;
    private float normalizedTension;
    private float breakTimer;

    private bool isConfigured;
    private bool hasLoggedConfigurationWarning;

    public FishingState State { get; private set; } = FishingState.Active;

    public float NormalizedTension => normalizedTension;

    public float NormalizedEndurance =>
        fishDefinition == null
            ? 0f
            : Mathf.Clamp01(
                currentEndurance / Mathf.Max(Epsilon, fishDefinition.maxEndurance));

    public float BreakProgress =>
        Mathf.Clamp01(breakTimer / breakDuration);

    public event Action<FishingState> StateChanged;

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

        fishEscapeAI.Tick(
            deltaTime,
            NormalizedEndurance);

        Vector3 fishPosition = fishMovementController.Position;

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
            directionToPlayer,
            escapeDirection,
            effectiveReelForce,
            fishForce,
            fishResistanceForce);

        UpdateEndurance(
            pull,
            fishResistanceForce,
            effectiveReelForce,
            deltaTime);

        UpdateTension(
            pull,
            release,
            lateral,
            fishResistanceForce,
            effectiveReelForce,
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

        fishMovementController.Stop();

        fishMovementController.Configure(
            fishDefinition,
            waterBounds);

        fishEscapeAI.Configure(
            fishDefinition,
            waterBounds);

        fishingLinePresenter.Configure(
            rodTip,
            fishMovementController.transform);

        fishingLinePresenter.SetLineTension(0f);

        SetState(FishingState.Active);
    }

    private bool ValidateConfiguration()
    {
        bool valid =
            playerTransform != null &&
            rodTip != null &&
            fishDefinition != null &&
            fishingInputReader != null &&
            fishEscapeAI != null &&
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

        float pressureFactor =
            fishEscapeAI.IsBursting
                ? 1f
                : Mathf.Lerp(
                    fishDefinition.idleForceMultiplier,
                    1f,
                    pull);

        float force =
            fishDefinition.escapeForce *
            enduranceFactor *
            pressureFactor *
            fishEscapeAI.EffortMultiplier;

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

        float escapeOpposition =
            Mathf.Max(
                0f,
                Vector3.Dot(
                    escapeDirection,
                    awayFromPlayer));

        float directEscapeForce =
            fishForce *
            escapeOpposition;

        Vector3 fishEscapeForce =
            escapeDirection *
            directEscapeForce;

        Vector3 playerRight =
            Vector3.Cross(
                Vector3.up,
                -directionToPlayer);

        float playerLateralAcceleration =
            lateral *
            lateralForce *
            (1f - fishDefinition.lateralResistance);

        Vector3 playerLateralForce =
            playerRight *
            playerLateralAcceleration;

        Vector3 totalAcceleration =
            playerReelForce +
            fishEscapeForce +
            playerLateralForce;

        fishMovementController.SetAcceleration(
            totalAcceleration);
    }


    private void UpdateEndurance(
    float pull,
    float fishResistanceForce,
    float effectiveReelForce,
    float deltaTime)
    {
        float recoveryThreshold =
            fishDefinition.maxEndurance *
            ResistanceRecoveryThresholdRatio;

        bool canResist =
            currentEndurance >= recoveryThreshold;

        if (pull > Epsilon && canResist)
        {
            float resistance01 =
                Mathf.Clamp01(
                    fishResistanceForce /
                    Mathf.Max(Epsilon, effectiveReelForce));

            currentEndurance -=
                pull *
                resistance01 *
                enduranceDrainPerSecond *
                fishDefinition.enduranceDrainMultiplier *
                deltaTime;
        }
        else if (pull <= Epsilon)
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
        float fishResistanceForce,
        float effectiveReelForce,
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

        if (isBursting)
        {
            gain +=
                (1f - release) *
                burstTensionRate;
        }

        gain +=
            Mathf.Abs(lateral) *
            lateralTensionRate;

        gain *=
            fishDefinition.tensionGainMultiplier;

        float recovery =
            release *
            releaseTensionRecovery;

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
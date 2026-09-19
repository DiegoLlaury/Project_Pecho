using System;
using UnityEngine;

public sealed class FishingSessionController : MonoBehaviour
{
    private const float MinimumDirectionMagnitude = 0.001f;
    private const float MaximumTension = 1f;
    private const float MaximumNormalizedValue = 1f;
    private const float MinimumNormalizedValue = 0f;

    [Header("Required References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform rodTip;
    [SerializeField] private Transform hookedFish;
    [SerializeField] private FishDefinition fishDefinition;
    [SerializeField] private FishingInputReader fishingInputReader;
    [SerializeField] private FishEscapeAI fishEscapeAI;
    [SerializeField] private FishMovementController fishMovementController;
    [SerializeField] private BoxCollider waterBounds;
    [SerializeField] private BoxCollider catchZone;
    [SerializeField] private FishingLinePresenter fishingLinePresenter;

    [Header("Player Influence")]
    [SerializeField] private float pullAcceleration = 12f;
    [SerializeField] private float lateralAcceleration = 8f;
    [SerializeField] private float releaseTensionReductionPerSecond = 0.7f;

    [Header("Tension")]
    [SerializeField] private float relaxedLineLength = 20f;
    [SerializeField] private float lineStretchForMaximumTension = 8f;
    [SerializeField] private float stretchTensionMultiplier = 1f;
    [SerializeField] private float opposingForceTensionMultiplier = 0.04f;
    [SerializeField] private float tensionGainPerSecond = 0.25f;
    [SerializeField] private float tensionRecoveryPerSecond = 0.5f;
    [SerializeField, Range(0f, 1f)] private float breakThreshold = 0.9f;
    [SerializeField] private float breakDuration = 2.5f;
    [SerializeField] private float pullTensionGainPerSecond = 0.16f;
    [SerializeField] private float fishRunTensionGainPerSecond = 0.12f;
    [SerializeField] private float slackTensionRecoveryPerSecond = 0.2f;

    [Header("Endurance")]
    [SerializeField] private float enduranceDrainPerSecond = 12f;
    [SerializeField, Range(0f, 1f)] private float oppositionThreshold = 0.35f;

    private float currentEndurance;
    private float normalizedTension;
    private float breakTimer;
    private bool isConfigured;
    private bool hasLoggedConfigurationWarning;

    /// <summary>
    /// État actuel de la session de pêche.
    /// </summary>
    public FishingState State { get; private set; } = FishingState.Active;

    /// <summary>
    /// Tension de la ligne, normalisée entre 0 et 1.
    /// </summary>
    public float NormalizedTension => normalizedTension;

    /// <summary>
    /// Endurance restante du poisson, normalisée entre 0 et 1.
    /// </summary>
    public float NormalizedEndurance =>
        fishDefinition == null
            ? MinimumNormalizedValue
            : Mathf.Clamp01(currentEndurance / fishDefinition.maxEndurance);

    /// <summary>
    /// Événement déclenché à chaque transition d'état de la session.
    /// </summary>
    public event Action<FishingState> StateChanged;

    private void Start()
    {
        ResetSession();
    }

    private void Update()
    {
        if (!isConfigured || State != FishingState.Active)
        {
            return;
        }

        OrientPlayerTowardsFish();
        UpdateFishingForces();
        UpdateTensionAndBreakTimer();
        TryCatchFish();
        UpdateLinePresentation();
    }

    /// <summary>
    /// Réinitialise l'endurance, la tension et l'état de la session.
    /// </summary>
    public void ResetSession()
    {
        isConfigured = ValidateConfiguration();

        if (!isConfigured)
        {
            enabled = false;
            return;
        }

        currentEndurance = fishDefinition.maxEndurance;
        normalizedTension = MinimumNormalizedValue;
        breakTimer = 0f;

        fishMovementController.Configure(fishDefinition, waterBounds);
        fishingLinePresenter.Configure(rodTip, hookedFish);
        fishingLinePresenter.SetLineTension(normalizedTension);

        SetState(FishingState.Active);
    }

    private bool ValidateConfiguration()
    {
        bool hasValidConfiguration =
            playerTransform != null &&
            rodTip != null &&
            hookedFish != null &&
            fishDefinition != null &&
            fishingInputReader != null &&
            fishEscapeAI != null &&
            fishMovementController != null &&
            waterBounds != null &&
            catchZone != null &&
            fishingLinePresenter != null;

        if (!hasValidConfiguration && !hasLoggedConfigurationWarning)
        {
            Debug.LogWarning(
                $"{nameof(FishingSessionController)} on '{name}' is disabled " +
                "because one or more required references are missing.",
                this);

            hasLoggedConfigurationWarning = true;
        }

        return hasValidConfiguration;
    }

    private void OrientPlayerTowardsFish()
    {
        Vector3 directionToFish = hookedFish.position - playerTransform.position;
        directionToFish.y = 0f;

        if (directionToFish.sqrMagnitude < MinimumDirectionMagnitude)
        {
            return;
        }

        playerTransform.rotation = Quaternion.LookRotation(
            directionToFish.normalized,
            Vector3.up);
    }

    private void UpdateFishingForces()
    {
        Vector2 fightInput = fishingInputReader.FightInput;

        Vector3 directionToPlayer =
            playerTransform.position - hookedFish.position;

        directionToPlayer.y = 0f;

        if (directionToPlayer.sqrMagnitude < MinimumDirectionMagnitude)
        {
            fishMovementController.SetSteering(Vector3.zero);
            return;
        }

        directionToPlayer.Normalize();

        Vector3 escapeDirection =
            fishEscapeAI.GetEscapeDirection(playerTransform.position);

        Vector3 lateralDirection = Vector3.Cross(
            Vector3.up,
            directionToPlayer).normalized;

        float pullInput = Mathf.Max(0f, -fightInput.y);

        float pullResistanceMultiplier = 1f -
            fishDefinition.resistanceToPull;

        float lateralResistanceMultiplier = 1f -
            fishDefinition.lateralResistance;

        Vector3 escapeInfluence =
            escapeDirection * fishDefinition.escapeAcceleration;

        Vector3 pullInfluence =
            directionToPlayer *
            pullInput *
            pullAcceleration *
            pullResistanceMultiplier;

        Vector3 lateralInfluence =
            lateralDirection *
            fightInput.x *
            lateralAcceleration *
            lateralResistanceMultiplier;

        Vector3 combinedSteering =
            escapeInfluence +
            pullInfluence +
            lateralInfluence;

        fishMovementController.SetSteering(combinedSteering);

        UpdateEndurance(
            pullInfluence + lateralInfluence,
            escapeDirection);
    }


    private void UpdateEndurance(
        Vector3 playerInfluence,
        Vector3 escapeDirection)
    {
        if (playerInfluence.sqrMagnitude < MinimumDirectionMagnitude)
        {
            return;
        }

        float opposition = Vector3.Dot(
            playerInfluence.normalized,
            -escapeDirection.normalized);

        if (opposition < oppositionThreshold)
        {
            return;
        }

        float intensity = Mathf.Clamp01(
            playerInfluence.magnitude / Mathf.Max(1f, pullAcceleration));

        currentEndurance -=
            intensity *
            enduranceDrainPerSecond *
            fishDefinition.enduranceDrainMultiplier *
            Time.deltaTime;

        currentEndurance = Mathf.Max(0f, currentEndurance);
    }

    private void UpdateTensionAndBreakTimer()
    {
        Vector2 fightInput = fishingInputReader.FightInput;

        float pullInput = Mathf.Max(0f, -fightInput.y);
        float releaseInput = Mathf.Max(0f, fightInput.y);

        float lineLength = Vector3.Distance(
            rodTip.position,
            hookedFish.position);

        float lineStretch = Mathf.Max(
            0f,
            lineLength - relaxedLineLength);

        float normalizedLineStretch = Mathf.Clamp01(
            lineStretch / lineStretchForMaximumTension);

        Vector3 directionToPlayer =
            playerTransform.position - hookedFish.position;

        directionToPlayer.y = 0f;

        float fishEscapeSpeed = 0f;

        if (directionToPlayer.sqrMagnitude > MinimumDirectionMagnitude)
        {
            directionToPlayer.Normalize();

            Vector3 directionAwayFromPlayer = -directionToPlayer;

            fishEscapeSpeed = Mathf.Clamp01(
                Vector3.Dot(
                    fishMovementController.CurrentVelocity,
                    directionAwayFromPlayer) /
                Mathf.Max(MinimumDirectionMagnitude, fishDefinition.maxSpeed));
        }

        float playerPullTensionGain =
            pullInput * pullTensionGainPerSecond;

        float escapingFishTensionGain =
            normalizedLineStretch *
            fishEscapeSpeed *
            fishRunTensionGainPerSecond;

        float tensionRecovery =
            releaseInput *
            releaseTensionReductionPerSecond;

        if (pullInput <= 0f)
        {
            tensionRecovery +=
                (1f - normalizedLineStretch) *
                slackTensionRecoveryPerSecond;
        }

        normalizedTension +=
            (playerPullTensionGain +
            escapingFishTensionGain -
            tensionRecovery) *
            Time.deltaTime;

        normalizedTension = Mathf.Clamp01(normalizedTension);

        if (normalizedTension >= breakThreshold)
        {
            breakTimer += Time.deltaTime;

            if (breakTimer >= breakDuration)
            {
                EndSession(FishingState.Escaped);
            }

            return;
        }

        breakTimer = 0f;
    }


    private void TryCatchFish()
    {
        if (currentEndurance > 0f || !IsFishInsideCatchZone())
        {
            return;
        }

        EndSession(FishingState.Caught);
    }

    private bool IsFishInsideCatchZone()
    {
        Vector3 closestPoint = catchZone.ClosestPoint(hookedFish.position);

        return (closestPoint - hookedFish.position).sqrMagnitude <
               MinimumDirectionMagnitude;
    }

    private void UpdateLinePresentation()
    {
        fishingLinePresenter.SetLineTension(normalizedTension);
    }

    private void EndSession(FishingState finalState)
    {
        if (State != FishingState.Active)
        {
            return;
        }

        fishMovementController.Stop();
        normalizedTension = Mathf.Clamp01(normalizedTension);
        SetState(finalState);
        UpdateLinePresentation();
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

    private void OnValidate()
    {
        pullAcceleration = Mathf.Max(0f, pullAcceleration);
        lateralAcceleration = Mathf.Max(0f, lateralAcceleration);
        releaseTensionReductionPerSecond =
            Mathf.Max(0f, releaseTensionReductionPerSecond);

        
        stretchTensionMultiplier = Mathf.Max(0f, stretchTensionMultiplier);
        opposingForceTensionMultiplier =
            Mathf.Max(0f, opposingForceTensionMultiplier);

        tensionRecoveryPerSecond = Mathf.Max(0f, tensionRecoveryPerSecond);

        relaxedLineLength = Mathf.Max(0f, relaxedLineLength);
        lineStretchForMaximumTension = Mathf.Max(
            MinimumDirectionMagnitude,
            lineStretchForMaximumTension);

        tensionGainPerSecond = Mathf.Max(0f, tensionGainPerSecond);

        breakThreshold = Mathf.Clamp01(breakThreshold);
        breakDuration = Mathf.Max(0f, breakDuration);

        enduranceDrainPerSecond = Mathf.Max(0f, enduranceDrainPerSecond);
        oppositionThreshold = Mathf.Clamp01(oppositionThreshold);
    }
}

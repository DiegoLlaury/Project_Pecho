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
    [SerializeField] private float maximumLineLength = 25f;
    [SerializeField] private float stretchTensionMultiplier = 1f;
    [SerializeField] private float opposingForceTensionMultiplier = 0.04f;
    [SerializeField] private float tensionRecoveryPerSecond = 0.15f;
    [SerializeField] private float tensionSmoothingSpeed = 8f;
    [SerializeField] private float breakThreshold = 0.98f;
    [SerializeField] private float breakDuration = 2.5f;

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
        Vector3 escapeDirection =
            fishEscapeAI.GetEscapeDirection(playerTransform.position);

        Vector3 playerInfluence = CalculatePlayerInfluence();
        Vector3 fishSteering =
            escapeDirection * fishDefinition.escapeAcceleration +
            playerInfluence;

        fishMovementController.SetSteering(fishSteering);

        UpdateEndurance(playerInfluence, escapeDirection);
    }

    private Vector3 CalculatePlayerInfluence()
    {
        Vector2 fightInput = fishingInputReader.FightInput;

        Vector3 directionToPlayer =
            playerTransform.position - hookedFish.position;

        directionToPlayer.y = 0f;

        if (directionToPlayer.sqrMagnitude < MinimumDirectionMagnitude)
        {
            return Vector3.zero;
        }

        directionToPlayer.Normalize();

        Vector3 lateralDirection = Vector3.Cross(
            Vector3.up,
            directionToPlayer).normalized;

        float pullInput = Mathf.Max(0f, -fightInput.y);
        float releaseInput = Mathf.Max(0f, fightInput.y);

        float pullResistanceMultiplier = Mathf.Clamp01(
            1f - fishDefinition.resistanceToPull);

        float lateralResistanceMultiplier = Mathf.Clamp01(
            1f - fishDefinition.lateralResistance);

        Vector3 pullForce =
            directionToPlayer *
            pullInput *
            pullAcceleration *
            pullResistanceMultiplier;

        Vector3 lateralForce =
            lateralDirection *
            fightInput.x *
            lateralAcceleration *
            lateralResistanceMultiplier;

        if (releaseInput > 0f)
        {
            normalizedTension = Mathf.Max(
                MinimumNormalizedValue,
                normalizedTension -
                releaseInput *
                releaseTensionReductionPerSecond *
                Time.deltaTime);
        }

        return pullForce + lateralForce;
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
        float lineLength = Vector3.Distance(
            rodTip.position,
            hookedFish.position);

        float normalizedStretch = Mathf.Clamp01(
            lineLength / Mathf.Max(MinimumDirectionMagnitude, maximumLineLength));

        Vector3 escapeDirection =
            fishEscapeAI.GetEscapeDirection(playerTransform.position);

        Vector3 directionToPlayer =
            playerTransform.position - hookedFish.position;

        directionToPlayer.y = 0f;

        float escapeAwayFromPlayer = 0f;

        if (directionToPlayer.sqrMagnitude > MinimumDirectionMagnitude)
        {
            directionToPlayer.Normalize();
            escapeAwayFromPlayer = Mathf.Max(
                0f,
                Vector3.Dot(escapeDirection, -directionToPlayer));
        }

        float desiredTension =
            normalizedStretch * stretchTensionMultiplier +
            escapeAwayFromPlayer *
            fishDefinition.escapeAcceleration *
            opposingForceTensionMultiplier *
            fishDefinition.tensionGainMultiplier;

        desiredTension = Mathf.Clamp01(desiredTension);

        normalizedTension = Mathf.MoveTowards(
            normalizedTension,
            desiredTension,
            tensionSmoothingSpeed * Time.deltaTime);

        if (!fishingInputReader.HasFightInput)
        {
            normalizedTension = Mathf.MoveTowards(
                normalizedTension,
                MinimumNormalizedValue,
                tensionRecoveryPerSecond * Time.deltaTime);
        }

        normalizedTension = Mathf.Clamp(
            normalizedTension,
            MinimumNormalizedValue,
            MaximumTension);

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

        maximumLineLength = Mathf.Max(MinimumDirectionMagnitude, maximumLineLength);
        stretchTensionMultiplier = Mathf.Max(0f, stretchTensionMultiplier);
        opposingForceTensionMultiplier =
            Mathf.Max(0f, opposingForceTensionMultiplier);

        tensionRecoveryPerSecond = Mathf.Max(0f, tensionRecoveryPerSecond);
        tensionSmoothingSpeed = Mathf.Max(0f, tensionSmoothingSpeed);

        breakThreshold = Mathf.Clamp01(breakThreshold);
        breakDuration = Mathf.Max(0f, breakDuration);

        enduranceDrainPerSecond = Mathf.Max(0f, enduranceDrainPerSecond);
        oppositionThreshold = Mathf.Clamp01(oppositionThreshold);
    }
}

using System;
using UnityEngine;

public sealed class FishingSessionController : MonoBehaviour
{
    private const float Epsilon = 0.001f;

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
    [Tooltip("Force de traction maximale du joueur. Doit dépasser l'escapeForce des poissons.")]
    [SerializeField, Min(0f)] private float reelForce = 12f;
    [SerializeField, Min(0f)] private float lateralForce = 6f;

    [Header("Tension")]
    [Tooltip("Tension gagnée par seconde en tirant contre un poisson qui force autant que le joueur.")]
    [SerializeField, Min(0f)] private float pullTensionRate = 0.6f;
    [Tooltip("Tension gagnée par seconde pendant une ruée, sauf si le joueur relâche.")]
    [SerializeField, Min(0f)] private float burstTensionRate = 0.3f;
    [SerializeField, Min(0f)] private float releaseTensionRecovery = 0.7f;
    [SerializeField, Min(0f)] private float idleTensionRecovery = 0.25f;
    [SerializeField, Range(0.5f, 1f)] private float breakThreshold = 1f;
    [SerializeField, Min(0.1f)] private float breakDuration = 2.5f;
    [Tooltip("Vitesse de décroissance du timer de rupture quand la tension repasse sous le seuil.")]
    [SerializeField, Min(0f)] private float breakTimerDecayMultiplier = 2f;

    [Header("Endurance")]
    [SerializeField, Min(0f)] private float enduranceDrainPerSecond = 12f;

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
            ? 0f
            : Mathf.Clamp01(currentEndurance / fishDefinition.maxEndurance);

    /// <summary>
    /// Progression du timer de rupture, de 0 à 1 (1 = le fil casse). Utile pour l'UI.
    /// </summary>
    public float BreakProgress => Mathf.Clamp01(breakTimer / breakDuration);

    /// <summary>
    /// Événement déclenché à chaque transition d'état de la session.
    /// </summary>
    public event Action<FishingState> StateChanged;

    private void Start()
    {
        ResetSession();
    }

    /// <summary>
    /// Simulation : forces, endurance, tension, capture et rupture. Tout en pas fixe.
    /// </summary>
    private void FixedUpdate()
    {
        if (!isConfigured || State != FishingState.Active)
        {
            return;
        }

        float deltaTime = Time.fixedDeltaTime;

        float pull = fishingInputReader.PullInput;
        float release = fishingInputReader.ReleaseInput;
        float lateral = fishingInputReader.LateralInput;

        fishEscapeAI.Tick(deltaTime, NormalizedEndurance);

        float fishForce = ComputeFishForce(pull);

        ApplyForces(pull, lateral, fishForce);
        UpdateEndurance(pull, fishForce, deltaTime);
        UpdateTension(pull, release, fishForce, deltaTime);

        TryCatchFish();

        if (State != FishingState.Active)
        {
            return;
        }

        UpdateBreakTimer(deltaTime);
    }

    /// <summary>
    /// Présentation : orientation du joueur et ligne, à la fréquence de rendu.
    /// </summary>
    private void Update()
    {
        if (!isConfigured || State != FishingState.Active)
        {
            return;
        }

        OrientPlayerTowardsFish();
        UpdateLinePresentation();
    }

    /// <summary>
    /// Réinitialise l'endurance, la tension et l'état de la session.
    /// </summary>
    public void ResetSession()
    {
        isConfigured = ValidateConfiguration();
        enabled = isConfigured;

        if (!isConfigured)
        {
            return;
        }

        currentEndurance = fishDefinition.maxEndurance;
        normalizedTension = 0f;
        breakTimer = 0f;

        fishMovementController.Configure(fishDefinition, waterBounds);
        fishEscapeAI.Configure(fishDefinition, waterBounds);
        fishingLinePresenter.Configure(rodTip, fishMovementController.transform);
        fishingLinePresenter.SetLineTension(normalizedTension);

        SetState(FishingState.Active);
    }

    private bool ValidateConfiguration()
    {
        bool hasValidConfiguration =
            playerTransform != null &&
            rodTip != null &&
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

    /// <summary>
    /// Force actuelle du poisson = escapeForce × fatigue × pression × ruée.
    /// - fatigue   : plus l'endurance baisse, plus le poisson faiblit.
    /// - pression  : il ne se bat à fond que si le joueur tire (ou pendant une ruée).
    /// - ruée      : multiplicateur temporaire décidé par FishEscapeAI.
    /// </summary>
    private float ComputeFishForce(float pull)
    {
        float strength = Mathf.Lerp(
            fishDefinition.exhaustedForceMultiplier,
            1f,
            NormalizedEndurance);

        float pressure = fishEscapeAI.IsBursting
            ? 1f
            : Mathf.Lerp(fishDefinition.idleForceMultiplier, 1f, pull);

        return fishDefinition.escapeForce *
               strength *
               pressure *
               fishEscapeAI.EffortMultiplier;
    }

    /// <summary>
    /// Additionne les forces (fuite du poisson + traction + latéral) et les envoie au mouvement.
    /// </summary>
    private void ApplyForces(float pull, float lateral, float fishForce)
    {
        Vector3 fishPosition = fishMovementController.Position;

        Vector3 directionToPlayer = playerTransform.position - fishPosition;
        directionToPlayer.y = 0f;

        if (directionToPlayer.sqrMagnitude < Epsilon)
        {
            fishMovementController.SetAcceleration(Vector3.zero);
            return;
        }

        directionToPlayer.Normalize();

        Vector3 escapeDirection = fishEscapeAI.GetEscapeDirection(
            fishPosition,
            playerTransform.position);

        // Droite du joueur lorsqu'il fait face au poisson.
        Vector3 playerRight = Vector3.Cross(Vector3.up, -directionToPlayer);

        float pullForce =
            pull *
            reelForce *
            (1f - fishDefinition.resistanceToPull);

        float sideForce =
            lateral *
            lateralForce *
            (1f - fishDefinition.lateralResistance);

        Vector3 totalAcceleration =
            escapeDirection * fishForce +
            directionToPlayer * pullForce +
            playerRight * sideForce;

        fishMovementController.SetAcceleration(totalAcceleration);
    }

    /// <summary>
    /// Tirer contre un poisson qui force le fatigue ; se reposer lui laisse récupérer.
    /// </summary>
    private void UpdateEndurance(float pull, float fishForce, float deltaTime)
    {
        if (pull > Epsilon)
        {
            float fishEffort =
                fishForce / Mathf.Max(Epsilon, fishDefinition.escapeForce);

            currentEndurance -=
                pull *
                fishEffort *
                enduranceDrainPerSecond *
                fishDefinition.enduranceDrainMultiplier *
                deltaTime;
        }
        else
        {
            currentEndurance +=
                fishDefinition.enduranceRecoveryPerSecond * deltaTime;
        }

        currentEndurance = Mathf.Clamp(
            currentEndurance,
            0f,
            fishDefinition.maxEndurance);
    }

    /// <summary>
    /// La tension vient de la charge sur la ligne : (force du poisson / force du joueur) quand on tire,
    /// plus un bonus pendant une ruée. Relâcher ou se reposer la fait redescendre.
    /// </summary>
    private void UpdateTension(
        float pull,
        float release,
        float fishForce,
        float deltaTime)
    {
        bool isBursting = fishEscapeAI.IsBursting;

        float loadRatio = Mathf.Clamp01(
            fishForce / Mathf.Max(Epsilon, reelForce));

        float gain = pull * loadRatio * pullTensionRate;

        if (isBursting)
        {
            gain += (1f - release) * burstTensionRate;
        }

        gain *= fishDefinition.tensionGainMultiplier;

        float recovery = release * releaseTensionRecovery;

        if (pull <= Epsilon && !isBursting)
        {
            recovery += idleTensionRecovery;
        }

        normalizedTension = Mathf.Clamp01(
            normalizedTension + (gain - recovery) * deltaTime);
    }

    private void UpdateBreakTimer(float deltaTime)
    {
        if (normalizedTension >= breakThreshold)
        {
            breakTimer += deltaTime;

            if (breakTimer >= breakDuration)
            {
                EndSession(FishingState.Escaped);
            }

            return;
        }

        breakTimer = Mathf.Max(
            0f,
            breakTimer - deltaTime * breakTimerDecayMultiplier);
    }

    private void TryCatchFish()
    {
        if (currentEndurance > 0f || !IsFishInsideCatchZone())
        {
            return;
        }

        EndSession(FishingState.Caught);
    }

    /// <summary>
    /// Test en XZ uniquement : la hauteur de nage du poisson ne doit pas empêcher la capture.
    /// </summary>
    private bool IsFishInsideCatchZone()
    {
        Bounds zone = catchZone.bounds;
        Vector3 position = fishMovementController.Position;

        return position.x >= zone.min.x && position.x <= zone.max.x &&
               position.z >= zone.min.z && position.z <= zone.max.z;
    }

    private void OrientPlayerTowardsFish()
    {
        Vector3 directionToFish =
            fishMovementController.transform.position - playerTransform.position;

        directionToFish.y = 0f;

        if (directionToFish.sqrMagnitude < Epsilon)
        {
            return;
        }

        playerTransform.rotation = Quaternion.LookRotation(
            directionToFish.normalized,
            Vector3.up);
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
}
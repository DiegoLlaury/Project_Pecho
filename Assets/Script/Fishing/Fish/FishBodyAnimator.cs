using UnityEngine;

/// <summary>
/// Compose l'ondulation, le banking et les poses d'action sur le mesh enfant uniquement.
/// Le corps physique du poisson n'est jamais déplacé par la présentation.
/// </summary>
public sealed class FishBodyAnimator : MonoBehaviour
{
    private const float MinimumDeltaTime = 0.0001f;
    private const float MinimumDirectionMagnitude = 0.001f;
    private const float FullCycleRadians = Mathf.PI * 2f;
    private const float DefaultPoseSmoothing = 12f;
    private const float DefaultWindupContraction = 0.16f;
    private const float DefaultBurstOffset = 0.12f;
    private const float DefaultDodgeRoll = 22f;
    private const float DefaultCastPitch = -8f;
    private const float DefaultRecoveryPitch = 6f;
    private const float DefaultBreakRoll = 28f;
    private const float ActionPulseDuration = 0.3f;
    private const float WindupSwimMultiplier = 0.25f;
    private const float BurstSwimMultiplier = 1.6f;
    private const float CastingSwimMultiplier = 0.2f;
    private const float RecoverySwimMultiplier = 0.5f;
    private const float BreakSwimMultiplier = 0.1f;
    private const float ExhaustedSwimMultiplier = 0.3f;
    private const float ContractionWidthRatio = 0.5f;
    private const float WindupPitchRatio = -0.5f;
    private const float BurstPitchRatio = 0.5f;
    private const float BreakPitchRatio = 0.4f;
    private const float BreakRestRollRatio = 0.25f;
    private const float ExhaustedRollRatio = 0.2f;

    [Header("References")]
    [SerializeField] private FishMovementController movementController;
    [Tooltip("Mesh enfant sans Rigidbody. Son pivot doit être au centre du corps.")]
    [SerializeField] private Transform visualRoot;

    [Header("Optional Combat References")]
    [SerializeField] private FishingSessionController fishingSessionController;
    [SerializeField] private FishEscapeAI fishEscapeAI;
    [Tooltip("Avec l'IA, permet d'orienter le mesh vers le cap annoncé pendant la préparation uniquement.")]
    [SerializeField] private Transform playerTransform;

    [Header("Swim Wave")]
    [SerializeField, Min(0f)] private float waveAngleDegrees = 10f;
    [SerializeField, Min(0f)] private float waveFrequencyAtReferenceSpeed = 3.5f;
    [SerializeField, Min(0.01f)] private float referenceSpeed = 3f;

    [Header("Banking")]
    [SerializeField, Min(0f)] private float maxBankDegrees = 25f;
    [SerializeField, Min(0f)] private float bankDegreesPerYawRate = 0.15f;
    [SerializeField, Min(0f)] private float bankSmoothing = 8f;

    [Header("Visual Action Offsets")]
    [SerializeField, Min(0f)] private float poseSmoothing = DefaultPoseSmoothing;
    [SerializeField, Range(0f, 1f)] private float windupContraction = DefaultWindupContraction;
    [SerializeField, Min(0f)] private float burstForwardOffset = DefaultBurstOffset;
    [SerializeField, Min(0f)] private float dodgeRollDegrees = DefaultDodgeRoll;
    [SerializeField] private float castingPitchDegrees = DefaultCastPitch;
    [SerializeField] private float recoveryPitchDegrees = DefaultRecoveryPitch;
    [SerializeField, Min(0f)] private float breakRollDegrees = DefaultBreakRoll;

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Vector3 initialLocalScale;
    private Vector3 currentPositionOffset;
    private Vector3 currentScaleMultiplier = Vector3.one;
    private Vector3 currentActionAngles;
    private FishCombatState previousState;
    private float stateElapsed;
    private float dodgeSide = 1f;
    private float previousYaw;
    private float wavePhase;
    private float currentBank;
    private bool isReady;
    private bool hasCombatState;

    private void Awake()
    {
        if (movementController == null)
        {
            movementController = GetComponent<FishMovementController>();
        }

        isReady = movementController != null && visualRoot != null &&
                  visualRoot != movementController.transform &&
                  visualRoot.IsChildOf(movementController.transform) &&
                  visualRoot.GetComponentInChildren<Rigidbody>(true) == null;
        if (!isReady)
        {
            Debug.LogWarning(
                $"{nameof(FishBodyAnimator)} sur '{name}' nécessite un moteur et un mesh enfant sans Rigidbody.",
                this);
            enabled = false;
            return;
        }

        if (fishingSessionController == null)
        {
            fishingSessionController = FindFirstObjectByType<FishingSessionController>();
        }
        if (fishEscapeAI == null)
        {
            fishEscapeAI = movementController.GetComponent<FishEscapeAI>();
        }

        initialLocalPosition = visualRoot.localPosition;
        initialLocalRotation = visualRoot.localRotation;
        initialLocalScale = visualRoot.localScale;
        previousYaw = movementController.transform.eulerAngles.y;
    }

    private void LateUpdate()
    {
        float deltaTime = Time.deltaTime;
        if (!isReady || visualRoot == null || movementController == null || deltaTime < MinimumDeltaTime)
        {
            return;
        }

        float yaw = movementController.transform.eulerAngles.y;
        float yawRate = Mathf.DeltaAngle(previousYaw, yaw) / deltaTime;
        previousYaw = yaw;
        float speedRatio = Mathf.Clamp01(
            movementController.CurrentVelocity.magnitude / Mathf.Max(MinimumDeltaTime, referenceSpeed));

        bool hasActiveCombat = fishingSessionController != null
            ? fishingSessionController.State == FishingState.Active
            : fishEscapeAI != null;
        FishCombatState state = fishingSessionController != null
            ? fishingSessionController.CurrentFishState
            : fishEscapeAI != null ? fishEscapeAI.CurrentState : FishCombatState.Struggle;
        if (!hasActiveCombat)
        {
            hasCombatState = false;
            state = FishCombatState.Struggle;
        }
        else if (!hasCombatState || state != previousState)
        {
            previousState = state;
            stateElapsed = 0f;
            hasCombatState = true;
            Vector3 lateralVelocity = movementController.transform.InverseTransformDirection(
                movementController.CurrentVelocity);
            float lateralCue = Mathf.Abs(lateralVelocity.x) > MinimumDirectionMagnitude
                ? lateralVelocity.x : yawRate;
            if (Mathf.Abs(lateralCue) > MinimumDirectionMagnitude)
            {
                dodgeSide = Mathf.Sign(lateralCue);
            }
        }
        else
        {
            stateElapsed += deltaTime;
        }

        GetActionPose(state, out Vector3 positionOffset, out Vector3 scaleMultiplier,
            out Vector3 actionAngles, out float swimMultiplier);
        float poseBlend = poseSmoothing > 0f ? 1f - Mathf.Exp(-poseSmoothing * deltaTime) : 1f;
        currentPositionOffset = Vector3.Lerp(currentPositionOffset, positionOffset, poseBlend);
        currentScaleMultiplier = Vector3.Lerp(currentScaleMultiplier, scaleMultiplier, poseBlend);
        currentActionAngles = Vector3.Lerp(currentActionAngles, actionAngles, poseBlend);

        wavePhase = Mathf.Repeat(wavePhase + FullCycleRadians * waveFrequencyAtReferenceSpeed *
            speedRatio * swimMultiplier * deltaTime, FullCycleRadians);
        float wave = Mathf.Sin(wavePhase) * waveAngleDegrees * speedRatio * Mathf.Min(1f, swimMultiplier);
        // La préparation annonce le cap exact, pas une oscillation de cap arbitraire.
        if (state == FishCombatState.BurstWindup)
        {
            wave = 0f;
        }

        float targetBank = Mathf.Clamp(-yawRate * bankDegreesPerYawRate, -maxBankDegrees, maxBankDegrees);
        currentBank = Mathf.Lerp(currentBank, targetBank, 1f - Mathf.Exp(-bankSmoothing * deltaTime));
        float announcedYaw = state == FishCombatState.BurstWindup ? GetAnnouncedLocalYaw() : 0f;

        // Toujours recomposer depuis la pose initiale : aucun déplacement/scale cumulatif.
        visualRoot.localPosition = initialLocalPosition + currentPositionOffset;
        visualRoot.localScale = Vector3.Scale(initialLocalScale, currentScaleMultiplier);
        visualRoot.localRotation = Quaternion.Euler(currentActionAngles.x,
            announcedYaw + wave, currentBank + currentActionAngles.z) * initialLocalRotation;
    }

    private void GetActionPose(FishCombatState state, out Vector3 positionOffset,
        out Vector3 scaleMultiplier, out Vector3 actionAngles, out float swimMultiplier)
    {
        positionOffset = Vector3.zero;
        scaleMultiplier = Vector3.one;
        actionAngles = Vector3.zero;
        swimMultiplier = 1f;
        float pulse = Mathf.Exp(-stateElapsed / ActionPulseDuration);
        switch (state)
        {
            case FishCombatState.BurstWindup:
                scaleMultiplier = new Vector3(1f + windupContraction * ContractionWidthRatio,
                    1f + windupContraction * ContractionWidthRatio, 1f - windupContraction);
                actionAngles.x = recoveryPitchDegrees * WindupPitchRatio;
                swimMultiplier = WindupSwimMultiplier;
                break;
            case FishCombatState.Burst:
                positionOffset.z = burstForwardOffset * pulse;
                actionAngles.x = castingPitchDegrees * BurstPitchRatio * pulse;
                swimMultiplier = BurstSwimMultiplier;
                break;
            case FishCombatState.LateralDodge:
                actionAngles.z = -dodgeSide * dodgeRollDegrees;
                break;
            case FishCombatState.CastSpell:
                actionAngles.x = castingPitchDegrees;
                swimMultiplier = CastingSwimMultiplier;
                break;
            case FishCombatState.Recovery:
                actionAngles.x = recoveryPitchDegrees;
                swimMultiplier = RecoverySwimMultiplier;
                break;
            case FishCombatState.ShoreRebound:
                positionOffset.z = -burstForwardOffset * pulse;
                actionAngles.x = breakRollDegrees * BreakPitchRatio * pulse;
                actionAngles.z = -breakRollDegrees * pulse;
                swimMultiplier = BreakSwimMultiplier;
                break;
            case FishCombatState.Break:
                actionAngles.x = breakRollDegrees * BreakPitchRatio * pulse;
                actionAngles.z = breakRollDegrees * Mathf.Max(BreakRestRollRatio, pulse);
                swimMultiplier = BreakSwimMultiplier;
                break;
            case FishCombatState.Exhausted:
                actionAngles.z = breakRollDegrees * ExhaustedRollRatio;
                swimMultiplier = ExhaustedSwimMultiplier;
                break;
        }
    }

    private float GetAnnouncedLocalYaw()
    {
        if (fishEscapeAI == null || playerTransform == null || visualRoot.parent == null)
        {
            return 0f;
        }

        Vector3 direction = fishEscapeAI.GetEscapeDirection(movementController.Position, playerTransform.position);
        direction.y = 0f;
        if (direction.sqrMagnitude < MinimumDirectionMagnitude * MinimumDirectionMagnitude)
        {
            return 0f;
        }

        Vector3 localDirection = visualRoot.parent.InverseTransformDirection(direction);
        return Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
    }

    private void OnDisable()
    {
        if (!isReady || visualRoot == null)
        {
            return;
        }

        visualRoot.localPosition = initialLocalPosition;
        visualRoot.localRotation = initialLocalRotation;
        visualRoot.localScale = initialLocalScale;
        currentPositionOffset = Vector3.zero;
        currentScaleMultiplier = Vector3.one;
        currentActionAngles = Vector3.zero;
        currentBank = 0f;
        hasCombatState = false;
        previousYaw = movementController != null ? movementController.transform.eulerAngles.y : previousYaw;
    }
}

using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Contrôleur temporaire de combat de pêche fondé sur la longueur du fil plutôt que sur les limites de la carte.
/// </summary>
public sealed class FishingLineCombatController : MonoBehaviour
{
    private const float Epsilon = 0.001f;
    private const float MinimumSpeedMultiplier = 0.15f;
    private const float TensionSoftCapFactor = 0.85f;

    private enum FishActivity
    {
        Swimming,
        Resting
    }

    [Header("Required References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform rodTip;
    [SerializeField] private Rigidbody fishRigidbody;
    [SerializeField] private BoxCollider waterBounds;
    [SerializeField] private FishingLinePresenter fishingLinePresenter;

    [Header("Line Limit")]
    [Tooltip("Longueur maximale du fil sur le plan horizontal. Elle définit l'aire jouable du poisson.")]
    [SerializeField, Min(1f)] private float maximumLineLength = 28f;
    [Tooltip("Part de la longueur du fil à partir de laquelle le poisson commence à ralentir.")]
    [SerializeField, Range(0.5f, 0.99f)] private float lineSlowdownStartRatio = 0.8f;
    [SerializeField, Min(0f)] private float lineReturnForce = 5f;
    [SerializeField, Min(0f)] private float lineLimitTensionRate = 0.22f;

    [Header("Player Control")]
    [SerializeField, Min(0f)] private float reelAcceleration = 5.5f;
    [Tooltip("Vitesse minimale vers le joueur appliquée tant que le joueur tire.")]
    [SerializeField, Min(0.1f)] private float reelInSpeed = 7f;
    [SerializeField, Min(0f)] private float lateralAcceleration = 8f;
    [SerializeField, Min(0f)] private float lateralTensionRate = 0.18f;

    [Header("Player View Corridor")]
    [Tooltip("Distance minimale devant le joueur à laquelle le poisson est maintenu.")]
    [SerializeField, Min(0f)] private float minimumFrontDistance = 5f;
    [Tooltip("Distance maximale devant le joueur à laquelle le poisson reste visible et atteignable.")]
    [SerializeField, Min(0.1f)] private float maximumFrontDistance = 18f;
    [Tooltip("Écart latéral maximal, exprimé comme une part de la distance devant le joueur.")]
    [SerializeField, Range(0.1f, 1f)] private float maximumFrontSideRatio = 0.35f;
    [Tooltip("Vitesse vers l'avant conservée quand le poisson atteint la limite proche du cône.")]
    [SerializeField, Min(0f)] private float minimumEscapeSpeed = 0.8f;

    [Header("Fish Movement")]
    [SerializeField, Min(0f)] private float swimAcceleration = 1.2f;
    [SerializeField, Min(0f)] private float restResistanceAcceleration = 0.9f;
    [SerializeField, Min(0.1f)] private float maximumFishSpeed = 3f;
    [SerializeField, Min(0f)] private float movementDrag = 2f;
    [SerializeField, Range(0f, 1f)] private float waterEdgeAvoidanceStrength = 0.7f;
    [SerializeField, Range(0.05f, 0.5f)] private float waterEdgeAvoidanceZone = 0.2f;

    [Header("Fish Activity")]
    [SerializeField, Min(0f)] private float minimumSwimDuration = 2.5f;
    [SerializeField, Min(0f)] private float maximumSwimDuration = 5.5f;
    [SerializeField, Min(0f)] private float minimumRestDuration = 1f;
    [SerializeField, Min(0f)] private float maximumRestDuration = 2.4f;
    [SerializeField, Min(0.1f)] private float minimumDirectionChangeInterval = 0.75f;
    [SerializeField, Min(0.1f)] private float maximumDirectionChangeInterval = 1.6f;
    [SerializeField] private int behaviorSeed;

    [Header("Tension")]
    [SerializeField, Min(0f)] private float pullTensionRate = 0.55f;
    [SerializeField, Min(0f)] private float releaseTensionRecovery = 0.8f;
    [SerializeField, Min(0f)] private float idleTensionRecovery = 0.18f;

    private System.Random random;
    private FishActivity fishActivity;
    private float activityTimeRemaining;
    private float directionChangeTimeRemaining;
    private float normalizedTension;
    private Vector3 roamDirection;
    private float swimmingHeight;

    /// <summary>
    /// Tension actuelle du fil, normalisée entre 0 et 1.
    /// </summary>
    public float NormalizedTension => normalizedTension;

    private void Awake()
    {
        ResolveReferences();
        DisableLegacyControllers();
    }

    private void Start()
    {
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        int seed = behaviorSeed == 0 ? GetInstanceID() : behaviorSeed;
        random = new System.Random(seed);
        swimmingHeight = fishRigidbody.position.y;
        fishRigidbody.useGravity = false;
        fishRigidbody.linearDamping = 0f;
        BeginSwimming();
        fishingLinePresenter.Configure(rodTip, fishRigidbody.transform);
        fishingLinePresenter.SetLineTension(normalizedTension);
    }

    private void FixedUpdate()
    {
        float deltaTime = Time.fixedDeltaTime;
        UpdateFishActivity(deltaTime);

        float pull = ReadButtonInput(Keyboard.current?.upArrowKey.isPressed == true,
            Keyboard.current?.downArrowKey.isPressed == true);
        float release = ReadButtonInput(Keyboard.current?.downArrowKey.isPressed == true,
            Keyboard.current?.upArrowKey.isPressed == true);
        float lateral = ReadAxis(
            Keyboard.current?.leftArrowKey.isPressed == true,
            Keyboard.current?.rightArrowKey.isPressed == true);

        Vector3 fishPosition = fishRigidbody.position;
        Vector3 toRod = rodTip.position - fishPosition;
        Vector3 horizontalToRod = Vector3.ProjectOnPlane(toRod, Vector3.up);
        float lineLength = horizontalToRod.magnitude;
        float linePressure = CalculateLinePressure(lineLength);

        Vector3 directionToPlayer = Vector3.ProjectOnPlane(playerTransform.position - fishPosition, Vector3.up);
        if (directionToPlayer.sqrMagnitude < Epsilon)
        {
            directionToPlayer = horizontalToRod;
        }

        directionToPlayer = directionToPlayer.sqrMagnitude < Epsilon
            ? Vector3.forward
            : directionToPlayer.normalized;

        Vector3 playerRight = Vector3.Cross(Vector3.up, -directionToPlayer);
        Vector3 fishForce = CalculateFishForce(fishPosition, directionToPlayer, pull);
        Vector3 playerForce = directionToPlayer * (pull * reelAcceleration) +
                              playerRight * (lateral * lateralAcceleration);
        Vector3 lineForce = CalculateLineReturnForce(horizontalToRod, lineLength);

        Vector3 velocity = fishRigidbody.linearVelocity;
        velocity.y = 0f;
        velocity += (fishForce + playerForce + lineForce) * deltaTime;
        velocity /= 1f + movementDrag * deltaTime;

        float fishSpeedLimit = Mathf.Lerp(
            maximumFishSpeed,
            maximumFishSpeed * MinimumSpeedMultiplier,
            linePressure);
        float speedLimit = Mathf.Lerp(
            fishSpeedLimit,
            Mathf.Max(fishSpeedLimit, reelInSpeed),
            pull);
        velocity = Vector3.ClampMagnitude(velocity, speedLimit);
        ApplyReelInSpeed(ref velocity, directionToPlayer, pull);
        ConstrainFishToFrontCorridor(ref velocity);
        fishRigidbody.linearVelocity = velocity;
        MaintainSwimmingHeight();
        RotateTowardsVelocity(velocity, deltaTime);

        UpdateTension(pull, release, lateral, linePressure, deltaTime);
    }

    private void LateUpdate()
    {
        if (fishingLinePresenter != null)
        {
            fishingLinePresenter.SetLineTension(normalizedTension);
        }
    }

    private void ResolveReferences()
    {
        playerTransform ??= GameObject.Find("Player")?.transform;
        rodTip ??= GameObject.Find("RodTip")?.transform;
        fishRigidbody ??= GameObject.Find("HookedFish")?.GetComponent<Rigidbody>();
        waterBounds ??= GameObject.Find("WaterBounds")?.GetComponent<BoxCollider>();
        fishingLinePresenter ??= FindFirstObjectByType<FishingLinePresenter>();
    }

    private void DisableLegacyControllers()
    {
        FishingSessionController legacySession = GetComponent<FishingSessionController>();
        if (legacySession != null)
        {
            legacySession.enabled = false;
        }

        FishMovementController legacyMovement = fishRigidbody != null
            ? fishRigidbody.GetComponent<FishMovementController>()
            : null;
        if (legacyMovement != null)
        {
            legacyMovement.enabled = false;
        }

        FishEscapeAI legacyEscapeAI = fishRigidbody != null
            ? fishRigidbody.GetComponent<FishEscapeAI>()
            : null;
        if (legacyEscapeAI != null)
        {
            legacyEscapeAI.enabled = false;
        }

        FishingInputReader legacyInput = FindFirstObjectByType<FishingInputReader>();
        if (legacyInput != null)
        {
            legacyInput.enabled = false;
        }
    }

    private bool HasRequiredReferences()
    {
        return playerTransform != null &&
               rodTip != null &&
               fishRigidbody != null &&
               fishingLinePresenter != null;
    }

    private void UpdateFishActivity(float deltaTime)
    {
        activityTimeRemaining -= deltaTime;
        directionChangeTimeRemaining -= deltaTime;

        if (fishActivity == FishActivity.Swimming && directionChangeTimeRemaining <= 0f)
        {
            SelectRoamDirection();
        }

        if (activityTimeRemaining > 0f)
        {
            return;
        }

        if (fishActivity == FishActivity.Swimming)
        {
            BeginResting();
            return;
        }

        BeginSwimming();
    }

    private void BeginSwimming()
    {
        fishActivity = FishActivity.Swimming;
        activityTimeRemaining = RollRange(minimumSwimDuration, maximumSwimDuration);
        SelectRoamDirection();
    }

    private void BeginResting()
    {
        fishActivity = FishActivity.Resting;
        activityTimeRemaining = RollRange(minimumRestDuration, maximumRestDuration);
    }

    private void SelectRoamDirection()
    {
        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
        roamDirection = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        directionChangeTimeRemaining = RollRange(
            minimumDirectionChangeInterval,
            maximumDirectionChangeInterval);
    }

    private Vector3 CalculateFishForce(
        Vector3 fishPosition,
        Vector3 directionToPlayer,
        float pull)
    {
        if (fishActivity == FishActivity.Resting)
        {
            return -directionToPlayer * (pull * restResistanceAcceleration);
        }

        Vector3 awayFromPlayer = -directionToPlayer;
        Vector3 direction = (awayFromPlayer * 0.55f + roamDirection * 0.85f +
                             CalculateWaterEdgeAvoidance(fishPosition)).normalized;
        return direction * swimAcceleration;
    }

    private Vector3 CalculateWaterEdgeAvoidance(Vector3 fishPosition)
    {
        if (waterBounds == null)
        {
            return Vector3.zero;
        }

        Bounds bounds = waterBounds.bounds;
        Vector3 offset = fishPosition - bounds.center;
        float normalizedX = offset.x / Mathf.Max(Epsilon, bounds.extents.x);
        float normalizedZ = offset.z / Mathf.Max(Epsilon, bounds.extents.z);
        float threshold = 1f - waterEdgeAvoidanceZone;

        float pushX = Mathf.InverseLerp(threshold, 1f, Mathf.Abs(normalizedX));
        float pushZ = Mathf.InverseLerp(threshold, 1f, Mathf.Abs(normalizedZ));

        return new Vector3(
            -Mathf.Sign(normalizedX) * pushX,
            0f,
            -Mathf.Sign(normalizedZ) * pushZ) * waterEdgeAvoidanceStrength;
    }

    private Vector3 CalculateLineReturnForce(Vector3 horizontalToRod, float lineLength)
    {
        float overflow = Mathf.Max(0f, lineLength - maximumLineLength);
        if (overflow <= 0f || horizontalToRod.sqrMagnitude < Epsilon)
        {
            return Vector3.zero;
        }

        return horizontalToRod.normalized * (overflow * lineReturnForce);
    }

    private float CalculateLinePressure(float lineLength)
    {
        float slowdownDistance = maximumLineLength * lineSlowdownStartRatio;
        return Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(slowdownDistance, maximumLineLength, lineLength));
    }

    private void UpdateTension(
        float pull,
        float release,
        float lateral,
        float linePressure,
        float deltaTime)
    {
        float gain = pull * pullTensionRate;
        gain += Mathf.Abs(lateral) * lateralTensionRate;
        gain += linePressure * lineLimitTensionRate *
                (1f - normalizedTension * TensionSoftCapFactor);

        float recovery = release * releaseTensionRecovery;
        if (pull <= Epsilon && Mathf.Abs(lateral) <= Epsilon)
        {
            recovery += idleTensionRecovery;
        }

        normalizedTension = Mathf.Clamp01(
            normalizedTension + (gain - recovery) * deltaTime);
    }

    private void ApplyReelInSpeed(
        ref Vector3 velocity,
        Vector3 directionToPlayer,
        float pull)
    {
        if (pull <= Epsilon)
        {
            return;
        }

        float currentSpeedTowardsPlayer = Vector3.Dot(velocity, directionToPlayer);
        float targetSpeedTowardsPlayer = reelInSpeed * pull;

        if (currentSpeedTowardsPlayer < targetSpeedTowardsPlayer)
        {
            velocity += directionToPlayer * (targetSpeedTowardsPlayer - currentSpeedTowardsPlayer);
        }
    }

    private void ConstrainFishToFrontCorridor(ref Vector3 velocity)
    {
        Vector3 playerPosition = playerTransform.position;
        playerPosition.y = swimmingHeight;

        Vector3 forward = Vector3.ProjectOnPlane(playerTransform.forward, Vector3.up);
        if (forward.sqrMagnitude < Epsilon)
        {
            return;
        }

        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 fishOffset = fishRigidbody.position - playerPosition;
        float frontDistance = Vector3.Dot(fishOffset, forward);
        float sideDistance = Vector3.Dot(fishOffset, right);
        float lineLimitedMaximumDistance = Mathf.Min(maximumFrontDistance, maximumLineLength);
        float clampedFrontDistance = Mathf.Clamp(
            frontDistance,
            minimumFrontDistance,
            lineLimitedMaximumDistance);
        float maximumSideDistance = Mathf.Max(
            minimumFrontDistance * maximumFrontSideRatio,
            clampedFrontDistance * maximumFrontSideRatio);
        float clampedSideDistance = Mathf.Clamp(
            sideDistance,
            -maximumSideDistance,
            maximumSideDistance);

        if (Mathf.Approximately(frontDistance, clampedFrontDistance) &&
            Mathf.Approximately(sideDistance, clampedSideDistance))
        {
            return;
        }

        fishRigidbody.position = playerPosition +
                                 forward * clampedFrontDistance +
                                 right * clampedSideDistance;

        float forwardVelocity = Vector3.Dot(velocity, forward);
        float sideVelocity = Vector3.Dot(velocity, right);
        bool hitNearLimit = frontDistance < minimumFrontDistance;
        bool hitFarLimit = frontDistance > lineLimitedMaximumDistance;

        if (hitNearLimit)
        {
            forwardVelocity = Mathf.Max(forwardVelocity, minimumEscapeSpeed);
        }
        else if (hitFarLimit)
        {
            forwardVelocity = Mathf.Min(forwardVelocity, 0f);
        }

        if (sideDistance != clampedSideDistance &&
            Mathf.Sign(sideDistance - clampedSideDistance) == Mathf.Sign(sideVelocity))
        {
            sideVelocity = 0f;
        }

        velocity = forward * forwardVelocity + right * sideVelocity;
    }

    private void MaintainSwimmingHeight()
    {
        Vector3 position = fishRigidbody.position;
        position.y = swimmingHeight;
        fishRigidbody.position = position;
    }

    private void RotateTowardsVelocity(Vector3 velocity, float deltaTime)
    {
        if (velocity.sqrMagnitude < Epsilon)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        fishRigidbody.MoveRotation(Quaternion.RotateTowards(
            fishRigidbody.rotation,
            targetRotation,
            180f * deltaTime));
    }

    private float RollRange(float minimum, float maximum)
    {
        return Mathf.Lerp(minimum, Mathf.Max(minimum, maximum), (float)random.NextDouble());
    }

    private static float ReadButtonInput(bool positivePressed, bool negativePressed)
    {
        return positivePressed && !negativePressed ? 1f : 0f;
    }

    private static float ReadAxis(bool negativePressed, bool positivePressed)
    {
        if (negativePressed == positivePressed)
        {
            return 0f;
        }

        return positivePressed ? 1f : -1f;
    }
}

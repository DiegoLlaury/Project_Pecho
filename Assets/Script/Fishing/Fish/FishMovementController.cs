using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class FishMovementController : MonoBehaviour
{
    private const float MinimumVelocityMagnitude = 0.001f;
    private const float BoundsPadding = 0.01f;

    [Header("References")]
    [SerializeField] private Rigidbody fishRigidbody;

    [Header("Swimming")]
    [SerializeField] private float swimmingHeight;
    [SerializeField] private bool rotateTowardsVelocity = true;
    [SerializeField] private float steeringForMaximumSpeed = 4f;

    private FishDefinition fishDefinition;
    private BoxCollider waterBounds;
    private Vector3 combinedSteering;
    private bool isConfigured;
    private bool hasLoggedConfigurationWarning;

    /// <summary>
    /// Retourne la vitesse horizontale courante du poisson.
    /// </summary>
    public Vector3 CurrentVelocity
    {
        get
        {
            Vector3 velocity = fishRigidbody != null
                ? fishRigidbody.linearVelocity
                : Vector3.zero;

            velocity.y = 0f;
            return velocity;
        }
    }

    private void Reset()
    {
        fishRigidbody = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (fishRigidbody == null)
        {
            fishRigidbody = GetComponent<Rigidbody>();
        }
    }

    /// <summary>
    /// Configure le poisson avec son profil de comportement et son volume de nage.
    /// </summary>
    public void Configure(FishDefinition definition, BoxCollider configuredWaterBounds)
    {
        fishDefinition = definition;
        waterBounds = configuredWaterBounds;

        if (fishRigidbody == null)
        {
            fishRigidbody = GetComponent<Rigidbody>();
        }

        isConfigured = ValidateConfiguration();

        if (isConfigured)
        {
            swimmingHeight = transform.position.y;
            fishRigidbody.useGravity = false;
        }
    }

    /// <summary>
    /// Définit l'accélération combinée issue de l'IA et des forces de pêche.
    /// </summary>
    public void SetSteering(Vector3 steering)
    {
        combinedSteering = Vector3.ProjectOnPlane(steering, Vector3.up);
    }

    /// <summary>
    /// Arrête immédiatement le poisson et annule toute intention de mouvement.
    /// </summary>
    public void Stop()
    {
        combinedSteering = Vector3.zero;

        if (fishRigidbody == null)
        {
            return;
        }

        fishRigidbody.linearVelocity = Vector3.zero;
        fishRigidbody.angularVelocity = Vector3.zero;
    }


    private void FixedUpdate()
    {
        if (!isConfigured)
        {

            return;
        }

        ApplySteering();
        ConstrainToWaterBounds();
        RotateTowardsMovement();
    }

    private bool ValidateConfiguration()
    {
        bool hasValidConfiguration =
            fishDefinition != null &&
            fishRigidbody != null &&
            waterBounds != null;

        if (!hasValidConfiguration && !hasLoggedConfigurationWarning)
        {
            Debug.LogWarning(
                $"{nameof(FishMovementController)} on '{name}' is disabled because " +
                "FishDefinition, Rigidbody, or WaterBounds is missing.",
                this);

            hasLoggedConfigurationWarning = true;
        }

        return hasValidConfiguration;
    }

    private void ApplySteering()
    {
        Vector3 horizontalSteering =
            Vector3.ProjectOnPlane(combinedSteering, Vector3.up);

        float steeringMagnitude = horizontalSteering.magnitude;

        if (steeringMagnitude < MinimumVelocityMagnitude)
        {
            fishRigidbody.linearVelocity = Vector3.zero;
            return;
        }

        float speedRatio = Mathf.Clamp01(
            steeringMagnitude / steeringForMaximumSpeed);

        Vector3 targetVelocity =
            horizontalSteering.normalized *
            fishDefinition.maxSpeed *
            speedRatio;

        Vector3 currentVelocity = fishRigidbody.linearVelocity;
        currentVelocity.y = 0f;

        Vector3 adjustedVelocity = Vector3.MoveTowards(
            currentVelocity,
            targetVelocity,
            fishDefinition.turnResponsiveness * Time.fixedDeltaTime);

        fishRigidbody.linearVelocity = new Vector3(
            adjustedVelocity.x,
            0f,
            adjustedVelocity.z);
    }



    private void ConstrainToWaterBounds()
    {
        Bounds worldBounds = waterBounds.bounds;
        Vector3 position = fishRigidbody.position;

        float minimumX = worldBounds.min.x + BoundsPadding;
        float maximumX = worldBounds.max.x - BoundsPadding;
        float minimumZ = worldBounds.min.z + BoundsPadding;
        float maximumZ = worldBounds.max.z - BoundsPadding;

        Vector3 constrainedPosition = new Vector3(
            Mathf.Clamp(position.x, minimumX, maximumX),
            swimmingHeight,
            Mathf.Clamp(position.z, minimumZ, maximumZ));

        bool leftWaterBounds =
            (constrainedPosition - position).sqrMagnitude >
            MinimumVelocityMagnitude;

        if (!leftWaterBounds)
        {
            return;
        }

        fishRigidbody.position = constrainedPosition;

        Vector3 velocity = fishRigidbody.linearVelocity;

        if (position.x != constrainedPosition.x)
        {
            velocity.x = 0f;
        }

        if (position.z != constrainedPosition.z)
        {
            velocity.z = 0f;
        }

        fishRigidbody.linearVelocity = velocity;
    }

    private void RotateTowardsMovement()
    {
        if (!rotateTowardsVelocity)
        {
            return;
        }

        Vector3 horizontalVelocity = CurrentVelocity;

        if (horizontalVelocity.sqrMagnitude < MinimumVelocityMagnitude)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(
            horizontalVelocity.normalized,
            Vector3.up);

        Quaternion smoothedRotation = Quaternion.Slerp(
            fishRigidbody.rotation,
            targetRotation,
            fishDefinition.turnResponsiveness * Time.fixedDeltaTime);

        fishRigidbody.MoveRotation(smoothedRotation);
    }

    private void OnValidate()
    {
        swimmingHeight = Mathf.Max(0f, swimmingHeight);
        steeringForMaximumSpeed = Mathf.Max(MinimumVelocityMagnitude,steeringForMaximumSpeed);
    }
}

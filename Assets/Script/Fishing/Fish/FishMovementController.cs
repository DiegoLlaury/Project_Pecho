using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class FishMovementController : MonoBehaviour
{
    private const float Epsilon = 0.001f;
    private const float BoundsPadding = 0.01f;

    [Header("References")]
    [SerializeField] private Rigidbody fishRigidbody;

    [Header("Rotation")]
    [SerializeField] private bool rotateTowardsVelocity = true;
    [Tooltip("Vitesse de rotation maximale en degrés par seconde, atteinte à pleine vitesse de virage.")]
    [SerializeField, Min(0f)] private float maxTurnSpeedDegrees = 180f;
    [Tooltip("Sous cette vitesse (m/s), le poisson garde son cap.")]
    [SerializeField, Min(0f)] private float minimumSpeedToTurn = 0.15f;
    [Tooltip("Part de maxSpeed à partir de laquelle le poisson tourne à pleine vitesse.")]
    [SerializeField, Range(0.1f, 1f)] private float fullTurnSpeedRatio = 0.4f;

    private FishDefinition fishDefinition;
    private BoxCollider waterBounds;
    private Vector3 acceleration;
    private float swimmingHeight;
    private bool isConfigured;
    private bool hasLoggedConfigurationWarning;

    /// <summary>
    /// Position physique du poisson (source de vérité pour la simulation).
    /// </summary>
    public Vector3 Position =>
        fishRigidbody != null ? fishRigidbody.position : transform.position;

    /// <summary>
    /// Vitesse horizontale courante du poisson.
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
    public void Configure(
    FishDefinition definition,
    BoxCollider configuredWaterBounds)
    {
        fishDefinition = definition;
        waterBounds = configuredWaterBounds;

        if (fishRigidbody == null)
        {
            fishRigidbody = GetComponent<Rigidbody>();
        }

        isConfigured = ValidateConfiguration();

        if (!isConfigured)
        {
            return;
        }

        // Le poisson doit être un Rigidbody dynamique.
        fishRigidbody.isKinematic = false;

        // Les collisions doivent rester actives.
        fishRigidbody.detectCollisions = true;

        // On autorise X et Z.
        // On bloque uniquement la hauteur et les rotations
        // qui pourraient faire basculer le poisson.
        fishRigidbody.constraints =
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;

        fishRigidbody.useGravity = false;

        fishRigidbody.linearDamping = 0f;
        fishRigidbody.angularDamping = 10f;

        fishRigidbody.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;

        fishRigidbody.interpolation =
            RigidbodyInterpolation.Interpolate;

        // --------------------------------------------------
        // ÉTAT DE SIMULATION
        // --------------------------------------------------

        swimmingHeight = fishRigidbody.position.y;

        acceleration = Vector3.zero;

        fishRigidbody.linearVelocity = Vector3.zero;
        fishRigidbody.angularVelocity = Vector3.zero;

        fishRigidbody.WakeUp();

        Debug.Log(
            $"[FISH CONFIG] {name} | " +
            $"Kinematic={fishRigidbody.isKinematic} | " +
            $"Constraints={fishRigidbody.constraints} | " +
            $"Velocity={fishRigidbody.linearVelocity}",
            this);
    }

    /// <summary>
    /// Définit l'accélération horizontale totale (force du poisson + traction du joueur).
    /// </summary>
    public void SetAcceleration(Vector3 newAcceleration)
    {
        acceleration = Vector3.ProjectOnPlane(newAcceleration, Vector3.up);
    }

    /// <summary>
    /// Arrête immédiatement le poisson et annule toute accélération.
    /// </summary>
    public void Stop()
    {
        acceleration = Vector3.zero;

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

        float deltaTime = Time.fixedDeltaTime;

        Integrate(deltaTime);
        ConstrainToWaterBounds();
        RotateTowardsMovement(deltaTime);
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

    /// <summary>
    /// Intègre l'accélération : v += a*dt, puis frottement linéaire, puis plafond maxSpeed.
    /// La vitesse d'équilibre vaut donc (force nette / drag) : la force nette compte vraiment.
    /// </summary>
    private void Integrate(float deltaTime)
    {
        Vector3 velocity = fishRigidbody.linearVelocity;
        velocity.y = 0f;

        velocity += acceleration * deltaTime;
        velocity /= 1f + fishDefinition.drag * deltaTime;
        velocity = Vector3.ClampMagnitude(velocity, fishDefinition.maxSpeed);

        fishRigidbody.linearVelocity = velocity;
    }

    private void ConstrainToWaterBounds()
    {
        Bounds bounds = waterBounds.bounds;
        Vector3 position = fishRigidbody.position;

        float x = Mathf.Clamp(
            position.x,
            bounds.min.x + BoundsPadding,
            bounds.max.x - BoundsPadding);

        float z = Mathf.Clamp(
            position.z,
            bounds.min.z + BoundsPadding,
            bounds.max.z - BoundsPadding);

        bool clampedX = !Mathf.Approximately(position.x, x);
        bool clampedZ = !Mathf.Approximately(position.z, z);
        bool wrongHeight = !Mathf.Approximately(position.y, swimmingHeight);

        if (!clampedX && !clampedZ && !wrongHeight)
        {
            return;
        }

        fishRigidbody.position = new Vector3(x, swimmingHeight, z);

        Vector3 velocity = fishRigidbody.linearVelocity;

        if (clampedX)
        {
            velocity.x = 0f;
        }

        if (clampedZ)
        {
            velocity.z = 0f;
        }

        fishRigidbody.linearVelocity = velocity;
    }

    /// <summary>
    /// Oriente le poisson vers sa vitesse avec un taux de rotation plafonné et proportionnel à sa vitesse :
    /// il décrit un vrai virage au lieu de pivoter sur place, et garde son cap quand il est presque immobile.
    /// turnResponsiveness règle la vivacité de la rotation, maxTurnSpeedDegrees son plafond.
    /// </summary>
    private void RotateTowardsMovement(float deltaTime)
    {
        if (!rotateTowardsVelocity)
        {
            return;
        }

        Vector3 horizontalVelocity = CurrentVelocity;
        float speed = horizontalVelocity.magnitude;

        if (speed < minimumSpeedToTurn)
        {
            return;
        }

        float fullTurnSpeed = Mathf.Max(
            minimumSpeedToTurn + Epsilon,
            fishDefinition.maxSpeed * fullTurnSpeedRatio);

        float speedFactor = Mathf.InverseLerp(minimumSpeedToTurn, fullTurnSpeed, speed);
        float turnLimit = maxTurnSpeedDegrees * speedFactor;

        float currentYaw = fishRigidbody.rotation.eulerAngles.y;
        float targetYaw = Mathf.Atan2(horizontalVelocity.x, horizontalVelocity.z) * Mathf.Rad2Deg;
        float yawError = Mathf.DeltaAngle(currentYaw, targetYaw);

        float turnSpeed = Mathf.Clamp(
            yawError * fishDefinition.turnResponsiveness,
            -turnLimit,
            turnLimit);

        float yawStep = turnSpeed * deltaTime;

        if (Mathf.Abs(yawStep) > Mathf.Abs(yawError))
        {
            yawStep = yawError;
        }

        fishRigidbody.MoveRotation(Quaternion.Euler(0f, currentYaw + yawStep, 0f));
    }

    [ContextMenu("TEST - Move Fish Left")]
    private void TestMoveFishLeft()
    {
        if (fishRigidbody == null)
        {
            Debug.LogError("Fish Rigidbody missing.", this);
            return;
        }

        fishRigidbody.isKinematic = false;

        fishRigidbody.constraints =
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;

        fishRigidbody.linearVelocity =
            Vector3.left * 50f;

        fishRigidbody.WakeUp();

        Debug.Log(
            $"TEST MOVEMENT | velocity={fishRigidbody.linearVelocity}",
            this);
    }
}
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class FishMovementController : MonoBehaviour
{
    private const float Epsilon = 0.001f;
    private const float BoundsPadding = 0.01f;
    private const float AngularDamping = 10f;
    private const float TestMovementSpeed = 50f;
    private const float MidpointRatio = 0.5f;

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
    private Collider[] fishColliders;
    private Vector3 acceleration;
    private Vector3 pendingVelocityChange;
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

    /// <summary>
    /// Vitesse horizontale préparée par le dernier Simulate, avant le solveur de collisions.
    /// Reste stable pendant les callbacks de ce pas physique, puis est remplacée au pas suivant.
    /// </summary>
    public Vector3 PreImpactVelocity { get; private set; }

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
    /// Configure le corps dynamique, annule les commandes précédentes et replace
    /// le poisson dans le volume de nage en tenant compte de ses colliders physiques.
    /// </summary>
    public void Configure(FishDefinition definition, BoxCollider configuredWaterBounds)
    {
        fishDefinition = definition;
        waterBounds = configuredWaterBounds;
        acceleration = Vector3.zero;
        pendingVelocityChange = Vector3.zero;
        PreImpactVelocity = Vector3.zero;

        if (fishRigidbody == null)
        {
            fishRigidbody = GetComponent<Rigidbody>();
        }

        isConfigured = ValidateConfiguration();

        if (!isConfigured)
        {
            Stop();
            return;
        }

        // Garder un corps dynamique : le solveur Unity reste responsable des contacts.
        fishRigidbody.isKinematic = false;
        fishRigidbody.detectCollisions = true;
        fishRigidbody.constraints =
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;
        fishRigidbody.useGravity = false;
        fishRigidbody.linearDamping = 0f;
        fishRigidbody.angularDamping = AngularDamping;
        fishRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        fishRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        fishColliders = fishRigidbody.GetComponentsInChildren<Collider>(true);
        swimmingHeight = fishRigidbody.position.y;
        Stop();
        ConstrainToWaterBounds(true);
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
    /// Cumule une variation de vitesse horizontale en m/s, indépendante de la masse.
    /// Elle est consommée une seule fois au prochain Simulate valide, avant le plafond de vitesse.
    /// </summary>
    public void QueueVelocityChange(Vector3 velocityChange)
    {
        pendingVelocityChange += Vector3.ProjectOnPlane(velocityChange, Vector3.up);
    }

    /// <summary>
    /// Prépare un pas depuis la vitesse issue du solveur précédent. La session doit appeler
    /// cette méthode une seule fois par pas, après SetAcceleration et avant le solveur Unity.
    /// Cette méthode n'avance pas elle-même Physics.
    /// </summary>
    public void Simulate(float dt)
    {
        if (!isConfigured || !isActiveAndEnabled || dt <= 0f ||
            float.IsNaN(dt) || float.IsInfinity(dt))
        {
            return;
        }

        Integrate(dt);
        ConstrainToWaterBounds();
        RotateTowardsMovement(dt);
        PreImpactVelocity = CurrentVelocity;
    }

    /// <summary>
    /// Arrête immédiatement le poisson et annule accélération, impulsions et instantané de collision.
    /// </summary>
    public void Stop()
    {
        acceleration = Vector3.zero;
        pendingVelocityChange = Vector3.zero;
        PreImpactVelocity = Vector3.zero;

        if (fishRigidbody == null || fishRigidbody.isKinematic)
        {
            return;
        }

        fishRigidbody.linearVelocity = Vector3.zero;
        fishRigidbody.angularVelocity = Vector3.zero;
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
    /// Intègre l'accélération et le frottement, puis consomme les impulsions avant le plafond maxSpeed.
    /// </summary>
    private void Integrate(float deltaTime)
    {
        // Conserver la réponse du solveur : ne jamais réinjecter une vitesse mémorisée.
        Vector3 velocity = fishRigidbody.linearVelocity;
        velocity.y = 0f;

        velocity += acceleration * deltaTime;
        velocity /= 1f + Mathf.Max(0f, fishDefinition.drag) * deltaTime;

        // Une impulsion n'est ni multipliée par dt ni amortie dès sa première application.
        velocity += pendingVelocityChange;
        pendingVelocityChange = Vector3.zero;
        velocity = Vector3.ClampMagnitude(velocity, Mathf.Max(0f, fishDefinition.maxSpeed));

        fishRigidbody.linearVelocity = velocity;
    }

    private void ConstrainToWaterBounds(bool initializeSwimmingHeight = false)
    {
        Bounds bounds = waterBounds.bounds;
        Vector3 position = fishRigidbody.position;
        Bounds colliderOffsets = GetColliderOffsets();

        float x = ClampToFittingInterval(
            position.x,
            bounds.min.x + BoundsPadding - colliderOffsets.min.x,
            bounds.max.x - BoundsPadding - colliderOffsets.max.x);
        float z = ClampToFittingInterval(
            position.z,
            bounds.min.z + BoundsPadding - colliderOffsets.min.z,
            bounds.max.z - BoundsPadding - colliderOffsets.max.z);

        if (initializeSwimmingHeight)
        {
            swimmingHeight = ClampToFittingInterval(
                position.y,
                bounds.min.y + BoundsPadding - colliderOffsets.min.y,
                bounds.max.y - BoundsPadding - colliderOffsets.max.y);
        }

        bool clampedX = !Mathf.Approximately(position.x, x);
        bool clampedZ = !Mathf.Approximately(position.z, z);
        bool wrongHeight = !Mathf.Approximately(position.y, swimmingHeight);

        if (!clampedX && !clampedZ && !wrongHeight)
        {
            return;
        }

        fishRigidbody.position = new Vector3(x, swimmingHeight, z);
        Vector3 velocity = fishRigidbody.linearVelocity;

        // Retirer uniquement la vitesse sortante, pas un rebond entrant produit par le solveur.
        if (clampedX && (position.x - x) * velocity.x > 0f)
        {
            velocity.x = 0f;
        }

        if (clampedZ && (position.z - z) * velocity.z > 0f)
        {
            velocity.z = 0f;
        }

        velocity.y = 0f;
        fishRigidbody.linearVelocity = velocity;
    }

    private Bounds GetColliderOffsets()
    {
        Bounds colliderOffsets = new Bounds(Vector3.zero, Vector3.zero);
        bool hasPhysicalCollider = false;

        // Collider.bounds suit le Transform rendu : soustraire ce même repère pour
        // ne pas transformer le retard de l'interpolation en faux décalage du collider.
        Vector3 bodyTransformPosition = fishRigidbody.transform.position;

        foreach (Collider fishCollider in fishColliders)
        {
            if (fishCollider == null || !fishCollider.enabled ||
                !fishCollider.gameObject.activeInHierarchy || fishCollider.isTrigger ||
                fishCollider.attachedRigidbody != fishRigidbody)
            {
                continue;
            }

            Bounds colliderBounds = fishCollider.bounds;
            colliderBounds.center -= bodyTransformPosition;

            if (!hasPhysicalCollider)
            {
                colliderOffsets = colliderBounds;
                hasPhysicalCollider = true;
            }
            else
            {
                colliderOffsets.Encapsulate(colliderBounds);
            }
        }

        return colliderOffsets;
    }

    private static float ClampToFittingInterval(float value, float minimum, float maximum)
    {
        // Un collider plus grand que le volume ne peut pas tenir : le centrer
        // plutôt qu'appeler Mathf.Clamp avec un intervalle inversé.
        return minimum <= maximum
            ? Mathf.Clamp(value, minimum, maximum)
            : (minimum + maximum) * MidpointRatio;
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
            Vector3.left * TestMovementSpeed;

        fishRigidbody.WakeUp();

        Debug.Log(
            $"TEST MOVEMENT | velocity={fishRigidbody.linearVelocity}",
            this);
    }
}
using UnityEngine;

[RequireComponent(typeof(FishMovementController))]
public sealed class TestFishMovement : MonoBehaviour
{
    private const float MinimumDirectionMagnitude = 0.001f;

    [Header("References")]
    [SerializeField] private FishMovementController fishMovementController;
    [SerializeField] private FishDefinition fishDefinition;
    [SerializeField] private BoxCollider waterBounds;

    [Header("Test Movement")]
    [SerializeField] private Vector3 testDirection = Vector3.forward;
    [SerializeField] private float steeringMultiplier = 1f;
    [SerializeField] private bool moveOnStart = true;

    private void Reset()
    {
        fishMovementController = GetComponent<FishMovementController>();
    }

    private void Awake()
    {
        if (fishMovementController == null)
        {
            fishMovementController = GetComponent<FishMovementController>();
        }

        if (fishMovementController == null ||
            fishDefinition == null ||
            waterBounds == null)
        {
            Debug.LogWarning(
                $"{nameof(TestFishMovement)} on '{name}' requires a " +
                "FishMovementController, FishDefinition, and WaterBounds.",
                this);

            enabled = false;
            return;
        }

        fishMovementController.Configure(fishDefinition, waterBounds);
    }

    private void FixedUpdate()
    {
        if (!moveOnStart)
        {
            fishMovementController.SetSteering(Vector3.zero);
            return;
        }

        Vector3 horizontalDirection = Vector3.ProjectOnPlane(
            testDirection,
            Vector3.up);

        if (horizontalDirection.sqrMagnitude < MinimumDirectionMagnitude)
        {
            fishMovementController.SetSteering(Vector3.zero);
            return;
        }

        Vector3 steering =
            horizontalDirection.normalized *
            fishDefinition.escapeAcceleration *
            steeringMultiplier;

        fishMovementController.SetSteering(steering);
    }

    /// <summary>
    /// Démarre ou arrête le déplacement de test.
    /// </summary>
    public void SetMovementEnabled(bool isEnabled)
    {
        moveOnStart = isEnabled;

        if (!moveOnStart)
        {
            fishMovementController.Stop();
        }
    }

    /// <summary>
    /// Change la direction horizontale utilisée par le test.
    /// </summary>
    public void SetTestDirection(Vector3 direction)
    {
        testDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
    }
}

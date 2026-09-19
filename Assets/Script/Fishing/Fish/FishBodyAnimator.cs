using UnityEngine;

/// <summary>
/// Animation purement visuelle du corps : ondulation de nage et inclinaison dans les virages.
/// À placer sur l'objet du poisson (celui qui porte FishMovementController).
/// Le mesh doit être un ENFANT : on ne touche jamais à la rotation du Rigidbody.
/// </summary>
public sealed class FishBodyAnimator : MonoBehaviour
{
    private const float MinimumDeltaTime = 0.0001f;

    [Header("References")]
    [SerializeField] private FishMovementController movementController;
    [Tooltip("Le mesh du poisson (enfant). Son pivot doit être au centre du corps.")]
    [SerializeField] private Transform visualRoot;

    [Header("Swim Wave")]
    [SerializeField, Min(0f)] private float waveAngleDegrees = 10f;
    [SerializeField, Min(0f)] private float waveFrequencyAtReferenceSpeed = 3.5f;
    [SerializeField, Min(0.01f)] private float referenceSpeed = 3f;

    [Header("Banking")]
    [SerializeField, Min(0f)] private float maxBankDegrees = 25f;
    [SerializeField, Min(0f)] private float bankDegreesPerYawRate = 0.15f;
    [SerializeField, Min(0f)] private float bankSmoothing = 8f;

    private Quaternion initialLocalRotation;
    private float previousYaw;
    private float wavePhase;
    private float currentBank;
    private bool isReady;

    private void Awake()
    {
        if (movementController == null)
        {
            movementController = GetComponent<FishMovementController>();
        }

        isReady = movementController != null && visualRoot != null;

        if (!isReady)
        {
            Debug.LogWarning(
                $"{nameof(FishBodyAnimator)} on '{name}' is disabled because " +
                "FishMovementController or Visual Root is missing.",
                this);

            enabled = false;
            return;
        }

        initialLocalRotation = visualRoot.localRotation;
        previousYaw = transform.eulerAngles.y;
    }

    private void LateUpdate()
    {
        float deltaTime = Time.deltaTime;

        if (!isReady || deltaTime < MinimumDeltaTime)
        {
            return;
        }

        float yaw = transform.eulerAngles.y;
        float yawRate = Mathf.DeltaAngle(previousYaw, yaw) / deltaTime;
        previousYaw = yaw;

        float speedRatio = Mathf.Clamp01(
            movementController.CurrentVelocity.magnitude / referenceSpeed);

        wavePhase += Mathf.PI * 2f *
                     waveFrequencyAtReferenceSpeed *
                     speedRatio *
                     deltaTime;

        float wave = Mathf.Sin(wavePhase) * waveAngleDegrees * speedRatio;

        // Virage à droite (yaw qui augmente) => roulis vers la droite => angle Z négatif.
        float targetBank = Mathf.Clamp(
            -yawRate * bankDegreesPerYawRate,
            -maxBankDegrees,
            maxBankDegrees);

        currentBank = Mathf.Lerp(
            currentBank,
            targetBank,
            1f - Mathf.Exp(-bankSmoothing * deltaTime));

        visualRoot.localRotation =
            Quaternion.Euler(0f, wave, currentBank) * initialLocalRotation;
    }
}
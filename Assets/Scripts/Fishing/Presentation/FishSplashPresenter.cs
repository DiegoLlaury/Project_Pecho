using UnityEngine;

/// <summary>
/// Genere les effets d'eau du poisson a la surface du volume de peche.
/// </summary>
public sealed class FishSplashPresenter : MonoBehaviour
{
    private const float MinimumInterval = 0.05f;

    [Header("References")]
    [SerializeField] private FishingSessionController fishingSessionController;
    [SerializeField] private FishMovementController fishMovementController;
    [SerializeField] private Transform waterSurface;

    [Header("Splash Prefabs")]
    [SerializeField] private GameObject regularSplashPrefab;
    [SerializeField] private GameObject interruptedBurstSplashPrefab;

    [Header("Timing")]
    [SerializeField, Min(MinimumInterval)] private float minimumSplashInterval = 1.2f;
    [SerializeField, Min(MinimumInterval)] private float maximumSplashInterval = 2.2f;
    [SerializeField, Min(0f)] private float particleLifetime = 5f;

    [Header("Placement")]
    [SerializeField] private float surfaceOffset = 0.02f;
    [SerializeField, Min(0f)] private float regularSplashScale = 0.65f;
    [SerializeField, Min(0f)] private float interruptedBurstSplashScale = 1.4f;

    private float timeUntilNextSplash;
    private bool isConfigured;
    private bool hasLoggedConfigurationWarning;

    private void OnEnable()
    {
        if (fishingSessionController != null)
        {
            fishingSessionController.BurstInterrupted += PlayInterruptedBurstSplash;
        }
    }

    private void Start()
    {
        isConfigured = ValidateConfiguration();
        if (!isConfigured)
        {
            return;
        }

        SpawnSplash(regularSplashPrefab, regularSplashScale);
        ResetSplashTimer();
    }

    private void OnDisable()
    {
        if (fishingSessionController != null)
        {
            fishingSessionController.BurstInterrupted -= PlayInterruptedBurstSplash;
        }
    }

    private void Update()
    {
        if (!isConfigured ||
            fishingSessionController.State != FishingState.Active)
        {
            return;
        }

        timeUntilNextSplash -= Time.deltaTime;
        if (timeUntilNextSplash > 0f)
        {
            return;
        }

        SpawnSplash(regularSplashPrefab, regularSplashScale);
        ResetSplashTimer();
    }

    /// <summary>
    /// Joue le gros splash lorsqu'un sort interrompt la ruee du poisson.
    /// </summary>
    public void PlayInterruptedBurstSplash()
    {
        if (!isConfigured)
        {
            return;
        }

        SpawnSplash(
            interruptedBurstSplashPrefab,
            interruptedBurstSplashScale);
    }

    private void SpawnSplash(GameObject splashPrefab, float scaleMultiplier)
    {
        if (splashPrefab == null)
        {
            return;
        }

        GameObject splashInstance = Instantiate(
            splashPrefab,
            GetSurfacePosition(),
            Quaternion.identity);

        splashInstance.SetActive(true);
        splashInstance.transform.localScale *= scaleMultiplier;

        ParticleSystem[] particleSystems =
            splashInstance.GetComponentsInChildren<ParticleSystem>(true);

        float requiredLifetime = particleLifetime;
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            ParticleSystem.MainModule mainModule = particleSystem.main;
            float systemLifetime =
                mainModule.duration + mainModule.startLifetime.constantMax;
            requiredLifetime = Mathf.Max(requiredLifetime, systemLifetime);

            particleSystem.gameObject.SetActive(true);
            particleSystem.Clear(true);
            particleSystem.Play(false);
        }

        Destroy(splashInstance, requiredLifetime);
    }

    private Vector3 GetSurfacePosition()
    {
        Vector3 position = fishMovementController.Position;
        position.y = waterSurface.position.y + surfaceOffset;
        return position;
    }

    private void ResetSplashTimer()
    {
        float maximumInterval = Mathf.Max(
            minimumSplashInterval,
            maximumSplashInterval);

        timeUntilNextSplash = Random.Range(
            minimumSplashInterval,
            maximumInterval);
    }

    private bool ValidateConfiguration()
    {
        bool valid =
            fishingSessionController != null &&
            fishMovementController != null &&
            waterSurface != null &&
            regularSplashPrefab != null &&
            interruptedBurstSplashPrefab != null;

        if (!valid && !hasLoggedConfigurationWarning)
        {
            Debug.LogWarning(
                $"{nameof(FishSplashPresenter)} on '{name}' has missing references.",
                this);
            hasLoggedConfigurationWarning = true;
        }

        return valid;
    }

    private void OnValidate()
    {
        maximumSplashInterval = Mathf.Max(
            minimumSplashInterval,
            maximumSplashInterval);
    }
}

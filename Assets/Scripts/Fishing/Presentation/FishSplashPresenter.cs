using UnityEngine;

/// <summary>
/// Génère les effets d'eau du poisson et un unique splash par ouverture de break.
/// </summary>
public sealed class FishSplashPresenter : MonoBehaviour
{
    private const float MinimumInterval = 0.05f;
    private const int NoSplashFrame = -1;

    [Header("References")]
    [SerializeField] private FishingSessionController fishingSessionController;
    [SerializeField] private FishMovementController fishMovementController;
    [SerializeField] private Transform waterSurface;

    [Header("Optional Splash Prefabs")]
    [SerializeField] private GameObject regularSplashPrefab;
    [Tooltip("Splash commun à toutes les causes de break. Le nom historique préserve les références existantes.")]
    [SerializeField] private GameObject interruptedBurstSplashPrefab;

    [Header("Timing")]
    [SerializeField, Min(MinimumInterval)] private float minimumSplashInterval = 1.2f;
    [SerializeField, Min(MinimumInterval)] private float maximumSplashInterval = 2.2f;
    [SerializeField, Min(0f)] private float particleLifetime = 5f;

    [Header("Placement")]
    [SerializeField] private float surfaceOffset = 0.02f;
    [SerializeField, Min(0f)] private float regularSplashScale = 0.65f;
    [SerializeField, Min(0f)] private float interruptedBurstSplashScale = 1.4f;

    private FishingSessionController subscribedSession;
    private float timeUntilNextSplash;
    private int lastBreakSplashFrame = NoSplashFrame;
    private bool isConfigured;
    private bool hasLoggedConfigurationWarning;

    private void Awake()
    {
        if (fishMovementController == null)
        {
            fishMovementController = GetComponent<FishMovementController>();
        }
        if (fishingSessionController == null)
        {
            fishingSessionController = FindFirstObjectByType<FishingSessionController>();
        }
        isConfigured = ValidateConfiguration();
    }

    private void OnEnable()
    {
        subscribedSession = fishingSessionController;
        if (subscribedSession != null)
        {
            // BurstInterrupted peut être émis pour le même break : ne pas s'y abonner aussi.
            subscribedSession.FishBreakStarted += HandleFishBreakStarted;
        }
        ResetSplashTimer();
    }

    private void Start()
    {
        if (isConfigured && fishingSessionController.State == FishingState.Active &&
            lastBreakSplashFrame != Time.frameCount)
        {
            SpawnSplash(regularSplashPrefab, regularSplashScale);
            ResetSplashTimer();
        }
    }

    private void OnDisable()
    {
        if (subscribedSession != null)
        {
            subscribedSession.FishBreakStarted -= HandleFishBreakStarted;
        }
        subscribedSession = null;
    }

    private void Update()
    {
        if (!isConfigured || fishingSessionController == null ||
            fishMovementController == null || waterSurface == null ||
            fishingSessionController.State != FishingState.Active ||
            lastBreakSplashFrame == Time.frameCount)
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
    /// Joue manuellement le splash de break commun ; conservé pour les appelants historiques.
    /// Les événements de session passent uniquement par FishBreakStarted, jamais BurstInterrupted.
    /// </summary>
    public void PlayInterruptedBurstSplash()
    {
        if (!isConfigured || fishingSessionController == null ||
            fishMovementController == null || waterSurface == null ||
            fishingSessionController.State != FishingState.Active ||
            lastBreakSplashFrame == Time.frameCount)
        {
            return;
        }

        lastBreakSplashFrame = Time.frameCount;
        GameObject splashPrefab = interruptedBurstSplashPrefab != null
            ? interruptedBurstSplashPrefab : regularSplashPrefab;
        SpawnSplash(splashPrefab, interruptedBurstSplashScale);
        // Empêche un splash régulier de doubler visuellement le break dans cette frame.
        ResetSplashTimer();
    }

    private void HandleFishBreakStarted(FishBreakCause cause)
    {
        // Toutes les causes réutilisent un seul effet, une fois par transition de la session.
        PlayInterruptedBurstSplash();
    }

    private void SpawnSplash(GameObject splashPrefab, float scaleMultiplier)
    {
        if (splashPrefab == null)
        {
            return;
        }

        GameObject splashInstance = Instantiate(splashPrefab, GetSurfacePosition(), Quaternion.identity);
        splashInstance.SetActive(true);
        splashInstance.transform.localScale *= scaleMultiplier;

        ParticleSystem[] particleSystems = splashInstance.GetComponentsInChildren<ParticleSystem>(true);
        float requiredLifetime = particleLifetime;
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            ParticleSystem.MainModule mainModule = particleSystem.main;
            float systemLifetime = mainModule.duration + mainModule.startLifetime.constantMax;
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
        float minimumInterval = Mathf.Max(MinimumInterval, minimumSplashInterval);
        float maximumInterval = Mathf.Max(minimumInterval, maximumSplashInterval);
        timeUntilNextSplash = Random.Range(minimumInterval, maximumInterval);
    }

    private bool ValidateConfiguration()
    {
        bool valid = fishingSessionController != null && fishMovementController != null && waterSurface != null;
        if (!valid && !hasLoggedConfigurationWarning)
        {
            Debug.LogWarning(
                $"{nameof(FishSplashPresenter)} sur '{name}' nécessite session, moteur et surface d'eau.", this);
            hasLoggedConfigurationWarning = true;
        }
        // Les prefabs sont optionnels : leur absence ne désactive pas les autres effets.
        return valid;
    }

    private void OnValidate()
    {
        minimumSplashInterval = Mathf.Max(MinimumInterval, minimumSplashInterval);
        maximumSplashInterval = Mathf.Max(minimumSplashInterval, maximumSplashInterval);
    }
}

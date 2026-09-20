using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Génère et déplace les vagues rythmiques de l'ATB, puis évalue les actions du joueur.</summary>
public sealed class ATBTimingController : MonoBehaviour
{
    private const int MinimumConcurrentWaves = 1;
    private const float MinimumWaveWidth = 1f;
    private const float MinimumWaveSpeed = 1f;

    [Header("UI")]
    [SerializeField] private RectTransform waveContainer;
    [SerializeField] private RectTransform waveTemplate;
    [SerializeField, Min(0f)] private float indicatorHalfWidth = 8f;

    [Header("Rhythm")]
    [SerializeField, Min(MinimumConcurrentWaves)] private int maximumConcurrentWaves = 2;
    [SerializeField, Min(0f)] private float minimumGap = 20f;
    [SerializeField, Min(0.1f)] private float spawnInterval = 4.2f;
    [SerializeField, Min(MinimumWaveWidth)] private float minimumWaveWidth = 90f;
    [SerializeField, Min(MinimumWaveWidth)] private float maximumWaveWidth = 150f;
    [SerializeField, Min(MinimumWaveSpeed)] private float waveSpeed = 100f;

    [Header("Boost")]
    [SerializeField, Min(1f)] private float actionPotencyMultiplier = 1.5f;
    [SerializeField, Range(0f, 1f)] private float cooldownMultiplier = 0.7f;
    [SerializeField, Min(1f)] private float continuousActionMultiplier = 1.5f;

    private readonly List<WaveState> activeWaves = new List<WaveState>();
    private readonly Stack<RectTransform> wavePool = new Stack<RectTransform>();
    private float spawnTimer;
    private bool previousBoostWindowState;

    public bool IsBoostWindowActive { get; private set; }
    public float ContinuousActionMultiplier => continuousActionMultiplier;

    public event Action<bool> BoostWindowChanged;
    public event Action<ATBActionModifiers> ActionEvaluated;

    private void Awake()
    {
        if (waveContainer == null)
        {
            waveContainer = transform as RectTransform;
        }

        if (waveTemplate == null && waveContainer != null && waveContainer.childCount > 0)
        {
            waveTemplate = waveContainer.GetChild(0) as RectTransform;
        }

        if (waveTemplate != null)
        {
            waveTemplate.gameObject.SetActive(false);
            PrewarmWavePool();
        }

        ScheduleNextWave();
    }

    private void Update()
    {
        if (waveContainer == null || waveTemplate == null)
        {
            return;
        }

        MoveWaves(Time.deltaTime);
        UpdateBoostWindow();
        UpdateSpawning(Time.deltaTime);
    }

    /// <summary>Capture les multiplicateurs d'une action ponctuelle au moment exact de son déclenchement.</summary>
    public ATBActionModifiers CaptureAction()
    {
        ATBActionModifiers modifiers = IsBoostWindowActive
            ? new ATBActionModifiers(actionPotencyMultiplier, cooldownMultiplier, true)
            : ATBActionModifiers.Normal;

        ActionEvaluated?.Invoke(modifiers);
        return modifiers;
    }

    /// <summary>Signale la réussite d'une action maintenue démarrée dans la fenêtre ATB.</summary>
    public void ReportContinuousActionSuccess()
    {
        ATBActionModifiers modifiers = new ATBActionModifiers(
            continuousActionMultiplier,
            1f,
            true);
        ActionEvaluated?.Invoke(modifiers);
    }

    private void UpdateSpawning(float deltaTime)
    {
        spawnTimer -= deltaTime;
        if (spawnTimer > 0f || activeWaves.Count >= maximumConcurrentWaves)
        {
            return;
        }

        float width = UnityEngine.Random.Range(
            Mathf.Min(minimumWaveWidth, maximumWaveWidth),
            Mathf.Max(minimumWaveWidth, maximumWaveWidth));

        if (!CanSpawn(width))
        {
            return;
        }

        SpawnWave(width);
        ScheduleNextWave();
    }

    private bool CanSpawn(float width)
    {
        float spawnLeftEdge = waveContainer.rect.xMax;
        for (int index = 0; index < activeWaves.Count; index++)
        {
            WaveState wave = activeWaves[index];
            float rightEdge = wave.RectTransform.anchoredPosition.x + wave.Width * 0.5f;
            if (rightEdge + minimumGap > spawnLeftEdge)
            {
                return false;
            }
        }

        return width >= MinimumWaveWidth;
    }

    private void PrewarmWavePool()
    {
        for (int waveIndex = 0; waveIndex < maximumConcurrentWaves; waveIndex++)
        {
            RectTransform wave = Instantiate(waveTemplate, waveContainer);
            wave.name = "ATB Wave";
            wave.gameObject.SetActive(false);
            wavePool.Push(wave);
        }
    }

    private void SpawnWave(float width)
    {
        RectTransform wave = wavePool.Count > 0
            ? wavePool.Pop()
            : Instantiate(waveTemplate, waveContainer);

        wave.name = "ATB Wave";
        wave.SetParent(waveContainer, false);
        wave.gameObject.SetActive(true);
        wave.SetAsLastSibling();
        wave.sizeDelta = new Vector2(width, waveTemplate.sizeDelta.y);
        wave.anchoredPosition = new Vector2(
            waveContainer.rect.xMax + width * 0.5f,
            waveTemplate.anchoredPosition.y);

        activeWaves.Add(new WaveState(wave, width));
    }

    private void MoveWaves(float deltaTime)
    {
        float leftBoundary = waveContainer.rect.xMin;
        for (int index = activeWaves.Count - 1; index >= 0; index--)
        {
            WaveState wave = activeWaves[index];
            Vector2 position = wave.RectTransform.anchoredPosition;
            position.x -= waveSpeed * deltaTime;
            wave.RectTransform.anchoredPosition = position;

            if (position.x + wave.Width * 0.5f < leftBoundary)
            {
                ReleaseWave(index);
            }
        }
    }

    private void UpdateBoostWindow()
    {
        IsBoostWindowActive = false;
        for (int index = 0; index < activeWaves.Count; index++)
        {
            WaveState wave = activeWaves[index];
            float activationDistance = wave.Width * 0.5f + indicatorHalfWidth;
            if (Mathf.Abs(wave.RectTransform.anchoredPosition.x) <= activationDistance)
            {
                IsBoostWindowActive = true;
                break;
            }
        }

        if (IsBoostWindowActive != previousBoostWindowState)
        {
            previousBoostWindowState = IsBoostWindowActive;
            BoostWindowChanged?.Invoke(IsBoostWindowActive);
        }
    }

    private void ReleaseWave(int index)
    {
        RectTransform wave = activeWaves[index].RectTransform;
        activeWaves.RemoveAt(index);
        wave.gameObject.SetActive(false);
        wavePool.Push(wave);
    }

    private void ScheduleNextWave()
    {
        spawnTimer += Mathf.Max(0.1f, spawnInterval);
    }

    private sealed class WaveState
    {
        public RectTransform RectTransform { get; }
        public float Width { get; }

        public WaveState(RectTransform rectTransform, float width)
        {
            RectTransform = rectTransform;
            Width = width;
        }
    }
}

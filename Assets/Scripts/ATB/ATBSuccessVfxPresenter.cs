using UnityEngine;

/// <summary>Affiche un VFX dans le monde lorsqu'une action bénéficie du bonus ATB.</summary>
public sealed class ATBSuccessVfxPresenter : MonoBehaviour
{
    private const float MinimumVisualDuration = 0.05f;

    [SerializeField] private ATBTimingController timingController;
    [SerializeField] private Transform spawnAnchor;
    [SerializeField] private GameObject successPrefab;
    [SerializeField, Min(MinimumVisualDuration)] private float visualDuration = 1.5f;

    private void Awake()
    {
        if (spawnAnchor == null)
        {
            spawnAnchor = transform;
        }
    }

    private void OnEnable()
    {
        if (timingController != null)
        {
            timingController.ActionEvaluated += HandleActionEvaluated;
        }
    }

    private void OnDisable()
    {
        if (timingController != null)
        {
            timingController.ActionEvaluated -= HandleActionEvaluated;
        }
    }

    private void HandleActionEvaluated(ATBActionModifiers modifiers)
    {
        if (!modifiers.IsBoosted || successPrefab == null || spawnAnchor == null)
        {
            return;
        }

        GameObject visual = Instantiate(
            successPrefab,
            spawnAnchor.position,
            spawnAnchor.rotation,
            spawnAnchor);
        Destroy(visual, Mathf.Max(MinimumVisualDuration, visualDuration));
    }
}

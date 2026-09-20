using UnityEngine;

public sealed class FishSpellcastingAI : MonoBehaviour
{
    private const float MinimumDecisionDelay = 0.1f;
    private const float GroundProbeHeight = 20f;
    private const float GroundProbeDistance = 50f;
    private const int GroundHitBufferCapacity = 16;

    [SerializeField] private SpellCaster spellCaster;
    [SerializeField] private Transform target;
    [SerializeField] private SpellType[] availableSpells;
    [SerializeField, Range(0f, 1f)] private float minimumEnduranceToCast = 0.55f;
    [SerializeField, Min(MinimumDecisionDelay)] private float minimumCastInterval = 2.5f;
    [SerializeField, Min(MinimumDecisionDelay)] private float maximumCastInterval = 5f;
    [SerializeField] private LayerMask groundLayers = -1;

    private float timeUntilCastDecision;
    private ISpendableEndurance enduranceResource;
    private readonly RaycastHit[] groundHitBuffer = new RaycastHit[GroundHitBufferCapacity];

    /// <summary>Initialise la cible, la ressource d'endurance et redémarre la temporisation de décision.</summary>
    public void Configure(
        Transform configuredTarget,
        ISpendableEndurance configuredEnduranceResource)
    {
        target = configuredTarget;
        enduranceResource = configuredEnduranceResource;
        timeUntilCastDecision = RollDecisionDelay();
    }

    /// <summary>Adapte les décisions de sort à l'endurance actuelle du poisson.</summary>
    public void Tick(float deltaTime, float normalizedEndurance)
    {
        if (spellCaster == null ||
            target == null ||
            enduranceResource == null ||
            availableSpells == null ||
            availableSpells.Length == 0)
        {
            return;
        }

        if (normalizedEndurance < minimumEnduranceToCast)
        {
            timeUntilCastDecision = Mathf.Max(timeUntilCastDecision, MinimumDecisionDelay);
            return;
        }

        timeUntilCastDecision -= deltaTime;
        if (timeUntilCastDecision > 0f || spellCaster.IsCasting)
        {
            return;
        }

        SpellType selectedSpell = FindReadySpell(enduranceResource.CurrentEndurance);
        if (selectedSpell != null && enduranceResource.TrySpendEndurance(selectedSpell.enduranceCost))
        {
            bool castStarted = spellCaster.TryCast(
                selectedSpell,
                ResolveGroundTarget(target.position));

            if (!castStarted)
            {
                enduranceResource.RestoreEndurance(selectedSpell.enduranceCost);
            }
        }

        timeUntilCastDecision = RollDecisionDelay();
    }

    private SpellType FindReadySpell(float currentEndurance)
    {
        int startIndex = Random.Range(0, availableSpells.Length);
        for (int offset = 0; offset < availableSpells.Length; offset++)
        {
            SpellType candidate = availableSpells[(startIndex + offset) % availableSpells.Length];
            if (candidate != null &&
                candidate.enduranceCost <= currentEndurance &&
                spellCaster.IsReady(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private Vector3 ResolveGroundTarget(Vector3 targetPosition)
    {
        Vector3 rayOrigin = targetPosition + Vector3.up * GroundProbeHeight;
        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            groundHitBuffer,
            GroundProbeDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);
        float nearestDistance = float.MaxValue;
        Vector3 groundPosition = targetPosition;

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            RaycastHit hit = groundHitBuffer[hitIndex];
            if (hit.collider == null ||
                hit.collider.transform.root == target.root ||
                hit.distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = hit.distance;
            groundPosition = hit.point;
        }

        return groundPosition;
    }

    private float RollDecisionDelay()
    {
        return Random.Range(
            Mathf.Min(minimumCastInterval, maximumCastInterval),
            Mathf.Max(minimumCastInterval, maximumCastInterval));
    }
}

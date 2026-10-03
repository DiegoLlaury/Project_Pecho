using UnityEngine;

/// <summary>Service de sorts du poisson : disponibilité et exécution, sans décision autonome.</summary>
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
    [Tooltip("Zéro utilise l'identifiant local du composant, sans modifier UnityEngine.Random.")]
    [SerializeField] private int behaviorSeed;

    private float timeUntilCastDecision;
    private ISpendableEndurance enduranceResource;
    private System.Random localRandom;
    private readonly RaycastHit[] groundHitBuffer = new RaycastHit[GroundHitBufferCapacity];

    /// <summary>État réel du lanceur, incluant projectile et télégraphe jusqu'à la résolution.</summary>
    public bool IsCasting => spellCaster != null && spellCaster.IsCasting;

    private void Awake()
    {
        if (spellCaster == null)
        {
            spellCaster = GetComponent<SpellCaster>();
        }
    }

    /// <summary>Initialise la cible, la ressource et les cooldowns au début d'une nouvelle session.</summary>
    public void Configure(Transform configuredTarget, ISpendableEndurance configuredEnduranceResource)
    {
        CancelCasting();
        target = configuredTarget;
        enduranceResource = configuredEnduranceResource;
        localRandom = new System.Random(behaviorSeed != 0 ? behaviorSeed : GetInstanceID());
        spellCaster?.ResetCombatState();
        timeUntilCastDecision = RollDecisionDelay();
    }

    /// <summary>Annule les visuels et l'incantation, sans rembourser ni réinitialiser les cooldowns engagés.</summary>
    public void CancelCasting()
    {
        spellCaster?.CancelCasting();
    }

    /// <summary>Avance uniquement la disponibilité ; la session l'appelle avant FishEscapeAI.Tick.</summary>
    public void AdvanceCooldown(float deltaTime)
    {
        if (!IsFinite(deltaTime) || deltaTime <= 0f)
        {
            return;
        }

        timeUntilCastDecision = Mathf.Max(0f, timeUntilCastDecision - deltaTime);
        if (IsCasting && !HasValidParticipants())
        {
            CancelCasting();
        }
    }

    /// <summary>Teste sans débit ni tirage aléatoire la présence d'un sort prêt et abordable.</summary>
    public bool CanStartCast(float normalizedEndurance)
    {
        return IsFinite(normalizedEndurance) &&
               Mathf.Clamp01(normalizedEndurance) >= Unit(minimumEnduranceToCast) &&
               timeUntilCastDecision <= 0f && !IsCasting && HasValidParticipants() &&
               FindReadySpell(0) != null;
    }

    /// <summary>Lance un sort abordable ; seul un échec de démarrage rembourse le coût à la session.</summary>
    public bool TryStartCast(float normalizedEndurance)
    {
        if (!CanStartCast(normalizedEndurance))
        {
            return false;
        }

        EnsureLocalRandom();
        SpellType selectedSpell = FindReadySpell(localRandom.Next(availableSpells.Length));
        if (selectedSpell == null)
        {
            return false;
        }

        Vector3 groundTarget = ResolveGroundTarget(target.position);
        float cost = selectedSpell.enduranceCost;
        if (!enduranceResource.TrySpendEndurance(cost))
        {
            return false;
        }

        bool started = spellCaster.TryCast(selectedSpell, groundTarget);
        if (!started)
        {
            enduranceResource.RestoreEndurance(cost);
            return false;
        }

        timeUntilCastDecision = RollDecisionDelay();
        return true;
    }

    private bool HasValidParticipants()
    {
        bool hasResource = enduranceResource != null &&
            !(enduranceResource is Object resourceObject && resourceObject == null);
        return isActiveAndEnabled && spellCaster != null && spellCaster.isActiveAndEnabled &&
               target != null && target.gameObject.activeInHierarchy && hasResource &&
               availableSpells != null && availableSpells.Length > 0;
    }

    private SpellType FindReadySpell(int startIndex)
    {
        float currentEndurance = enduranceResource.CurrentEndurance;
        if (!IsFinite(currentEndurance) || currentEndurance < 0f)
        {
            return null;
        }

        for (int offset = 0; offset < availableSpells.Length; offset++)
        {
            SpellType candidate = availableSpells[(startIndex + offset) % availableSpells.Length];
            if (candidate != null && IsFinite(candidate.enduranceCost) &&
                candidate.enduranceCost >= 0f && candidate.enduranceCost <= currentEndurance &&
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
        int hitCount = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, groundHitBuffer,
            GroundProbeDistance, groundLayers, QueryTriggerInteraction.Ignore);
        float nearestDistance = float.MaxValue;
        Vector3 groundPosition = targetPosition;

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            RaycastHit hit = groundHitBuffer[hitIndex];
            if (hit.collider == null || hit.collider.transform.root == target.root ||
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
        EnsureLocalRandom();
        float minimum = AtLeast(minimumCastInterval, MinimumDecisionDelay);
        float maximum = AtLeast(maximumCastInterval, minimum);
        return Mathf.Lerp(minimum, maximum, (float)localRandom.NextDouble());
    }

    private void EnsureLocalRandom()
    {
        if (localRandom == null)
        {
            localRandom = new System.Random(behaviorSeed != 0 ? behaviorSeed : GetInstanceID());
        }
    }

    private void OnDisable() => CancelCasting();

    private void OnValidate()
    {
        minimumEnduranceToCast = Unit(minimumEnduranceToCast);
        minimumCastInterval = AtLeast(minimumCastInterval, MinimumDecisionDelay);
        maximumCastInterval = AtLeast(maximumCastInterval, minimumCastInterval);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static float Unit(float value) => Mathf.Clamp01(AtLeast(value, 0f));
    private static float AtLeast(float value, float minimum) => IsFinite(value) ? Mathf.Max(minimum, value) : minimum;
}

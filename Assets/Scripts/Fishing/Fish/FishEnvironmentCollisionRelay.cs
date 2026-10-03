using System.Collections.Generic;
using UnityEngine;

/// <summary>Transmet un impact par racine de décor jusqu'à séparation de tous ses contacts physiques.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(FishMovementController))]
public sealed class FishEnvironmentCollisionRelay : MonoBehaviour
{
    private const float MinimumIncomingNormalSpeed = 0f;
    private const float MinimumNormalSquaredMagnitude = 0.000001f;
    private const int SingleCollisionCount = 1;

    [SerializeField] private FishMovementController fishMovementController;
    [SerializeField] private FishingSessionController fishingSessionController;

    private readonly Dictionary<Collider, FishingBreakObstacle> obstacleCache =
        new Dictionary<Collider, FishingBreakObstacle>();
    private readonly Dictionary<Collider, FishingBreakObstacle> contactObstacles =
        new Dictionary<Collider, FishingBreakObstacle>();
    private readonly Dictionary<Collider, int> colliderContactCounts =
        new Dictionary<Collider, int>();
    private readonly Dictionary<FishingBreakObstacle, int> obstacleContactCounts =
        new Dictionary<FishingBreakObstacle, int>();
    private readonly List<Collider> invalidContactColliders = new List<Collider>();

    private void Awake()
    {
        CacheMovementController();
    }

    /// <summary>Met la session en cache et annule les contacts d'une configuration précédente.</summary>
    public void Configure(FishingSessionController configuredSession)
    {
        fishingSessionController = configuredSession;
        CacheMovementController();
        ResetContacts();
    }

    /// <summary>Indique si au moins une collision solide touche encore cette racine.</summary>
    public bool IsTouching(FishingBreakObstacle obstacle)
    {
        PruneInvalidContacts();
        return obstacle != null && obstacleContactCounts.ContainsKey(obstacle);
    }

    /// <summary>Efface contacts et caches au redémarrage, sans débiter d'endurance ni notifier la session.</summary>
    public void ResetContacts()
    {
        contactObstacles.Clear();
        colliderContactCounts.Clear();
        obstacleContactCounts.Clear();
        obstacleCache.Clear();
        invalidContactColliders.Clear();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isActiveAndEnabled || collision == null || !IsSolidCollider(collision.collider))
        {
            return;
        }

        Collider obstacleCollider = collision.collider;
        FishingBreakObstacle obstacle = GetObstacle(obstacleCollider);
        if (obstacle == null || !obstacle.isActiveAndEnabled)
        {
            return;
        }

        Vector3 incomingVelocity = fishMovementController != null
            ? fishMovementController.PreImpactVelocity
            : Vector3.zero;
        bool hasFiniteIncomingVelocity = IsFinite(incomingVelocity);
        bool hasSolidFishContact = false;
        float incomingNormalSpeed = MinimumIncomingNormalSpeed;
        Vector3 impactNormal = Vector3.zero;
        for (int contactIndex = 0; contactIndex < collision.contactCount; contactIndex++)
        {
            ContactPoint contact = collision.GetContact(contactIndex);
            Collider fishCollider;
            Vector3 normalTowardsFish;
            if (contact.otherCollider == obstacleCollider)
            {
                fishCollider = contact.thisCollider;
                normalTowardsFish = contact.normal;
            }
            else if (contact.thisCollider == obstacleCollider)
            {
                fishCollider = contact.otherCollider;
                normalTowardsFish = -contact.normal;
            }
            else
            {
                continue;
            }

            if (!IsSolidCollider(fishCollider))
            {
                continue;
            }

            hasSolidFishContact = true;
            if (!hasFiniteIncomingVelocity || !IsFinite(normalTowardsFish) ||
                normalTowardsFish.sqrMagnitude <= MinimumNormalSquaredMagnitude ||
                !IsFinite(normalTowardsFish.sqrMagnitude))
            {
                continue;
            }

            normalTowardsFish.Normalize();
            float contactIncomingSpeed = -Vector3.Dot(incomingVelocity, normalTowardsFish);
            if (!IsFinite(contactIncomingSpeed) || contactIncomingSpeed <= incomingNormalSpeed)
            {
                continue;
            }

            incomingNormalSpeed = contactIncomingSpeed;
            impactNormal = normalTowardsFish;
        }

        if (!hasSolidFishContact)
        {
            return;
        }

        PruneInvalidContacts();
        // Compter les callbacks de paires, pas les points du manifold : plusieurs colliders
        // du poisson peuvent toucher le même collider externe et sortir séparément.
        contactObstacles[obstacleCollider] = obstacle;
        colliderContactCounts.TryGetValue(obstacleCollider, out int existingColliderContactCount);
        colliderContactCounts[obstacleCollider] = existingColliderContactCount + SingleCollisionCount;
        obstacleContactCounts.TryGetValue(obstacle, out int existingObstacleContactCount);
        obstacleContactCounts[obstacle] = existingObstacleContactCount + SingleCollisionCount;

        // Même un premier contact non qualifié doit se séparer complètement avant réarmement.
        if (existingObstacleContactCount > 0 || fishingSessionController == null ||
            fishMovementController == null || incomingNormalSpeed <= MinimumIncomingNormalSpeed)
        {
            return;
        }

        // Qualification joueur, cooldown, break et coût restent sous l'autorité de la session.
        fishingSessionController.NotifyEnvironmentImpact(obstacle, impactNormal, incomingNormalSpeed);
    }

    private void OnCollisionExit(Collision collision)
    {
        // Une sortie 3D ne fournit généralement aucun ContactPoint : seul le collider externe
        // identifie le compteur à décrémenter, sans supprimer les autres paires encore actives.
        if (collision != null)
        {
            ReleaseContact(collision.collider, SingleCollisionCount);
        }
    }

    private void OnDisable()
    {
        ResetContacts();
    }

    private void CacheMovementController()
    {
        if (fishMovementController == null)
        {
            fishMovementController = GetComponent<FishMovementController>();
        }
    }

    private FishingBreakObstacle GetObstacle(Collider obstacleCollider)
    {
        if (!obstacleCache.TryGetValue(obstacleCollider, out FishingBreakObstacle obstacle))
        {
            obstacle = obstacleCollider.GetComponentInParent<FishingBreakObstacle>();
            obstacleCache.Add(obstacleCollider, obstacle);
        }

        return obstacle;
    }

    private void PruneInvalidContacts()
    {
        invalidContactColliders.Clear();
        foreach (var contact in contactObstacles)
        {
            if (!IsSolidCollider(contact.Key) || contact.Value == null || !contact.Value.isActiveAndEnabled)
            {
                invalidContactColliders.Add(contact.Key);
            }
        }

        foreach (Collider obstacleCollider in invalidContactColliders)
        {
            if (colliderContactCounts.TryGetValue(obstacleCollider, out int contactCount))
            {
                ReleaseContact(obstacleCollider, contactCount);
            }
        }

        invalidContactColliders.Clear();
    }

    private void ReleaseContact(Collider obstacleCollider, int releasedContactCount)
    {
        if (ReferenceEquals(obstacleCollider, null) ||
            !contactObstacles.TryGetValue(obstacleCollider, out FishingBreakObstacle obstacle) ||
            !colliderContactCounts.TryGetValue(obstacleCollider, out int colliderContactCount))
        {
            return;
        }

        releasedContactCount = Mathf.Min(releasedContactCount, colliderContactCount);
        int remainingColliderContacts = colliderContactCount - releasedContactCount;
        if (remainingColliderContacts > 0)
        {
            colliderContactCounts[obstacleCollider] = remainingColliderContacts;
        }
        else
        {
            colliderContactCounts.Remove(obstacleCollider);
            contactObstacles.Remove(obstacleCollider);
        }

        if (obstacleContactCounts.TryGetValue(obstacle, out int obstacleContactCount) &&
            obstacleContactCount > releasedContactCount)
        {
            obstacleContactCounts[obstacle] = obstacleContactCount - releasedContactCount;
        }
        else
        {
            obstacleContactCounts.Remove(obstacle);
        }
    }

    private static bool IsSolidCollider(Collider collider)
    {
        return collider != null && collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

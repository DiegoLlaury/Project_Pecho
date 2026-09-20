using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class SpellCaster : MonoBehaviour
{
    private const float MinimumTravelDuration = 0.05f;
    private const float MinimumDirectionMagnitude = 0.0001f;

    [SerializeField] private SpellTeam team = SpellTeam.Neutral;
    [SerializeField] private Transform castOrigin;
    [SerializeField] private LayerMask targetLayers = -1;

    private readonly Dictionary<SpellType, float> nextCastTimes = new Dictionary<SpellType, float>();

    public bool IsCasting { get; private set; }

    /// <summary>Indique si le sort est disponible et si aucun autre lancement n'est en cours.</summary>
    public bool IsReady(SpellType spell)
    {
        if (spell == null || IsCasting)
        {
            return false;
        }

        return !nextCastTimes.TryGetValue(spell, out float nextCastTime) || Time.time >= nextCastTime;
    }

    /// <summary>Lance un sort vers une position figée et retourne faux s'il est indisponible.</summary>
    public bool TryCast(SpellType spell, Vector3 targetPosition)
    {
        if (!IsReady(spell))
        {
            return false;
        }

        nextCastTimes[spell] = Time.time + Mathf.Max(0f, spell.cooldown);
        StartCoroutine(CastSequence(spell, targetPosition));
        return true;
    }

    private IEnumerator CastSequence(SpellType spell, Vector3 targetPosition)
    {
        IsCasting = true;

        float warningDuration = Random.Range(
            Mathf.Min(spell.minimumWarningDuration, spell.maximumWarningDuration),
            Mathf.Max(spell.minimumWarningDuration, spell.maximumWarningDuration));
        warningDuration = Mathf.Max(MinimumTravelDuration, warningDuration);

        Quaternion groundRotation = Quaternion.identity;
        GameObject telegraph = CreateVisual(spell.telegraphPrefab, targetPosition, groundRotation);
        if (telegraph != null)
        {
            telegraph.transform.localScale = Vector3.one * (spell.impactRadius * 2f);
        }

        Vector3 origin = castOrigin != null ? castOrigin.position : transform.position;
        GameObject projectile = CreateVisual(spell.projectilePrefab, origin, Quaternion.identity);
        float elapsed = 0f;

        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / warningDuration);

            if (projectile != null)
            {
                projectile.transform.position = Vector3.Lerp(origin, targetPosition, progress);
                Vector3 direction = targetPosition - projectile.transform.position;
                if (direction.sqrMagnitude > MinimumDirectionMagnitude)
                {
                    projectile.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }
            }

            yield return null;
        }

        if (telegraph != null)
        {
            Destroy(telegraph);
        }

        if (projectile != null)
        {
            Destroy(projectile);
        }

        GameObject impact = CreateVisual(spell.impactPrefab, targetPosition, Quaternion.identity);
        if (impact != null)
        {
            Destroy(impact, Mathf.Max(0.05f, spell.impactVisualDuration));
        }

        ApplyImpact(spell, targetPosition);
        IsCasting = false;
    }

    private void ApplyImpact(SpellType spell, Vector3 targetPosition)
    {
        Collider[] colliders = Physics.OverlapSphere(
            targetPosition,
            Mathf.Max(0f, spell.impactRadius),
            targetLayers,
            QueryTriggerInteraction.Collide);
        HashSet<GameObject> affectedTargets = new HashSet<GameObject>();

        foreach (Collider hitCollider in colliders)
        {
            SpellAffiliation affiliation = hitCollider.GetComponentInParent<SpellAffiliation>();
            GameObject target = affiliation != null
                ? affiliation.gameObject
                : hitCollider.transform.root.gameObject;

            if (target == gameObject || affectedTargets.Contains(target))
            {
                continue;
            }

            if (affiliation != null && team != SpellTeam.Neutral && affiliation.Team == team)
            {
                continue;
            }

            affectedTargets.Add(target);
            ExecuteEffects(spell, target);
        }
    }

    private void ExecuteEffects(SpellType spell, GameObject target)
    {
        if (spell.effects == null)
        {
            return;
        }

        foreach (SpellEffect effect in spell.effects)
        {
            if (effect != null)
            {
                effect.Execute(gameObject, target);
            }
        }
    }

    private static GameObject CreateVisual(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        return prefab == null ? null : Instantiate(prefab, position, rotation);
    }
}

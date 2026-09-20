using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class SpellCaster : MonoBehaviour
{
    private const float MinimumCastDuration = 0.05f;
    private const float MinimumDirectionMagnitude = 0.0001f;
    private const int ImpactColliderBufferCapacity = 32;

    [SerializeField] private SpellTeam team = SpellTeam.Neutral;
    [SerializeField] private Transform castOrigin;
    [SerializeField] private LayerMask targetLayers = -1;

    private readonly Dictionary<SpellType, float> nextCastTimes = new Dictionary<SpellType, float>();
    private readonly HashSet<GameObject> affectedTargets = new HashSet<GameObject>();
    private readonly Collider[] impactColliderBuffer = new Collider[ImpactColliderBufferCapacity];

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

    /// <summary>Retourne le temps de cooldown restant pour un sort.</summary>
    public float GetRemainingCooldown(SpellType spell)
    {
        if (spell == null || !nextCastTimes.TryGetValue(spell, out float nextCastTime))
        {
            return 0f;
        }

        return Mathf.Max(0f, nextCastTime - Time.time);
    }

    /// <summary>Retourne la part normalisée du cooldown encore active.</summary>
    public float GetNormalizedCooldown(SpellType spell)
    {
        return spell == null || spell.cooldown <= 0f
            ? 0f
            : Mathf.Clamp01(GetRemainingCooldown(spell) / spell.cooldown);
    }

    /// <summary>Lance un sort de zone vers une position figée sans bonus d'action.</summary>
    public bool TryCast(SpellType spell, Vector3 targetPosition)
    {
        return TryCast(spell, targetPosition, ATBActionModifiers.Normal);
    }

    /// <summary>Lance un sort de zone avec les multiplicateurs capturés lors de l'action.</summary>
    public bool TryCast(
        SpellType spell,
        Vector3 targetPosition,
        ATBActionModifiers modifiers)
    {
        if (!BeginCast(spell, modifiers))
        {
            return false;
        }

        StartCoroutine(CastAtPositionSequence(spell, targetPosition, modifiers));
        return true;
    }

    /// <summary>Lance un sort joueur sans bonus d'action.</summary>
    public bool TryCast(SpellType spell, GameObject target, Vector3 predictedTargetPosition)
    {
        return TryCast(spell, target, predictedTargetPosition, ATBActionModifiers.Normal);
    }

    /// <summary>Lance un sort joueur avec les multiplicateurs capturés lors de l'action.</summary>
    public bool TryCast(
        SpellType spell,
        GameObject target,
        Vector3 predictedTargetPosition,
        ATBActionModifiers modifiers)
    {
        if (target == null || !BeginCast(spell, modifiers))
        {
            return false;
        }

        StartCoroutine(CastAtTargetSequence(spell, target, predictedTargetPosition, modifiers));
        return true;
    }

    private bool BeginCast(SpellType spell, ATBActionModifiers modifiers)
    {
        if (!IsReady(spell))
        {
            return false;
        }

        float cooldownDuration = Mathf.Max(0f, spell.cooldown) * modifiers.CooldownMultiplier;
        nextCastTimes[spell] = Time.time + cooldownDuration;
        IsCasting = true;
        return true;
    }

    private IEnumerator CastAtPositionSequence(
        SpellType spell,
        Vector3 targetPosition,
        ATBActionModifiers modifiers)
    {
        float castDuration = GetCastDuration(spell);
        GameObject telegraph = CreateVisual(spell.telegraphPrefab, targetPosition, Quaternion.identity);
        if (telegraph != null)
        {
            telegraph.transform.localScale = Vector3.one * (spell.impactRadius * 2f);
        }

        yield return AnimateProjectile(spell, targetPosition, castDuration);
        DestroyVisual(telegraph);
        CreateTimedImpact(spell, targetPosition);
        ApplyAreaImpact(spell, targetPosition, modifiers);
        IsCasting = false;
    }

    private IEnumerator CastAtTargetSequence(
        SpellType spell,
        GameObject target,
        Vector3 predictedTargetPosition,
        ATBActionModifiers modifiers)
    {
        float castDuration = GetCastDuration(spell);

        if (spell.deliveryMode == SpellDeliveryMode.Projectile)
        {
            yield return AnimateProjectile(spell, predictedTargetPosition, castDuration);
            if (target != null)
            {
                CreateTimedImpact(spell, target.transform.position);
                ExecuteEffects(spell, target, modifiers);
            }
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < castDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (target != null)
            {
                if (spell.deliveryMode == SpellDeliveryMode.TargetEffect)
                {
                    CreateTimedImpact(
                        spell,
                        target.transform.position,
                        target.transform);
                }

                ExecuteEffects(spell, target, modifiers);
            }
        }

        IsCasting = false;
    }

    private IEnumerator AnimateProjectile(
        SpellType spell,
        Vector3 targetPosition,
        float travelDuration)
    {
        Vector3 origin = castOrigin != null ? castOrigin.position : transform.position;
        GameObject projectile = CreateVisual(spell.projectilePrefab, origin, Quaternion.identity);
        float elapsed = 0f;

        while (elapsed < travelDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / travelDuration);

            if (projectile != null)
            {
                projectile.transform.position = EvaluateArcPosition(
                    origin,
                    targetPosition,
                    spell.projectileArcHeight,
                    progress);
                Vector3 travelDirection = EvaluateArcDirection(
                    origin,
                    targetPosition,
                    spell.projectileArcHeight,
                    progress);

                if (travelDirection.sqrMagnitude > MinimumDirectionMagnitude)
                {
                    projectile.transform.rotation = Quaternion.LookRotation(
                        travelDirection.normalized,
                        Vector3.up);
                }
            }

            yield return null;
        }

        DestroyVisual(projectile);
    }

    private static float GetCastDuration(SpellType spell)
    {
        float duration = Random.Range(
            Mathf.Min(spell.minimumWarningDuration, spell.maximumWarningDuration),
            Mathf.Max(spell.minimumWarningDuration, spell.maximumWarningDuration));
        return Mathf.Max(MinimumCastDuration, duration);
    }

    private static void CreateTimedImpact(
        SpellType spell,
        Vector3 position,
        Transform parent = null)
    {
        GameObject impact = CreateVisual(spell.impactPrefab, position, Quaternion.identity);
        if (impact != null)
        {
            if (parent != null)
            {
                impact.transform.SetParent(parent, true);
            }

            Destroy(impact, Mathf.Max(MinimumCastDuration, spell.impactVisualDuration));
        }
    }

    private void ApplyAreaImpact(
        SpellType spell,
        Vector3 targetPosition,
        ATBActionModifiers modifiers)
    {
        affectedTargets.Clear();
        int hitCount = Physics.OverlapSphereNonAlloc(
            targetPosition,
            Mathf.Max(0f, spell.impactRadius),
            impactColliderBuffer,
            targetLayers,
            QueryTriggerInteraction.Collide);

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider hitCollider = impactColliderBuffer[hitIndex];
            if (hitCollider == null)
            {
                continue;
            }

            SpellAffiliation affiliation = hitCollider.GetComponentInParent<SpellAffiliation>();
            GameObject target = affiliation != null
                ? affiliation.gameObject
                : hitCollider.transform.root.gameObject;

            if (target == gameObject || !affectedTargets.Add(target))
            {
                continue;
            }

            if (affiliation != null && team != SpellTeam.Neutral && affiliation.Team == team)
            {
                continue;
            }

            ExecuteEffects(spell, target, modifiers);
        }
    }

    private void ExecuteEffects(
        SpellType spell,
        GameObject target,
        ATBActionModifiers modifiers)
    {
        if (spell.effects == null)
        {
            return;
        }

        foreach (SpellEffect effect in spell.effects)
        {
            if (effect != null)
            {
                effect.Execute(gameObject, target, modifiers);
            }
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        IsCasting = false;
        affectedTargets.Clear();
    }

    private static Vector3 EvaluateArcPosition(
        Vector3 origin,
        Vector3 target,
        float arcHeight,
        float progress)
    {
        Vector3 linearPosition = Vector3.Lerp(origin, target, progress);
        float verticalOffset = Mathf.Sin(progress * Mathf.PI) * Mathf.Max(0f, arcHeight);
        return linearPosition + Vector3.up * verticalOffset;
    }

    private static Vector3 EvaluateArcDirection(
        Vector3 origin,
        Vector3 target,
        float arcHeight,
        float progress)
    {
        Vector3 linearDirection = target - origin;
        float verticalSlope = Mathf.Cos(progress * Mathf.PI) * Mathf.PI * Mathf.Max(0f, arcHeight);
        return linearDirection + Vector3.up * verticalSlope;
    }

    private static GameObject CreateVisual(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        return prefab == null ? null : Instantiate(prefab, position, rotation);
    }

    private static void DestroyVisual(GameObject visual)
    {
        if (visual != null)
        {
            Destroy(visual);
        }
    }
}

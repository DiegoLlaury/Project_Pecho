using UnityEngine;

[CreateAssetMenu(fileName = "WhirlwindEffect", menuName = "Scriptable Objects/Spells/Effects/Whirlwind")]
public sealed class WhirlwindEffect : SpellEffect
{
    [SerializeField, Min(0f)] private float duration = 2.5f;
    [SerializeField, Min(0f)] private float pullAcceleration = 18f;

    /// <summary>Force temporairement le déplacement de la cible vers le lanceur.</summary>
    public override void Execute(GameObject caster, GameObject target)
    {
        Execute(caster, target, ATBActionModifiers.Normal);
    }

    /// <summary>Applique un tourbillon modifié par le timing ATB capturé.</summary>
    public override void Execute(
        GameObject caster,
        GameObject target,
        ATBActionModifiers modifiers)
    {
        FishingFishSpellHitReceiver receiver = target.GetComponentInParent<FishingFishSpellHitReceiver>();
        receiver?.ApplyWhirlwind(
            duration * modifiers.PotencyMultiplier,
            pullAcceleration * modifiers.PotencyMultiplier);
    }
}

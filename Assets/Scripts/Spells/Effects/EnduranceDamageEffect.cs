using UnityEngine;

[CreateAssetMenu(fileName = "EnduranceDamageEffect", menuName = "Scriptable Objects/Spells/Effects/Endurance Damage")]
public sealed class EnduranceDamageEffect : SpellEffect
{
    [SerializeField, Min(0f)] private float enduranceDamage = 25f;

    /// <summary>Applique une perte d'endurance à la cible si elle expose la ressource correspondante.</summary>
    public override void Execute(GameObject caster, GameObject target)
    {
        Execute(caster, target, ATBActionModifiers.Normal);
    }

    /// <summary>Applique une perte d'endurance modifiée par le timing ATB capturé.</summary>
    public override void Execute(
        GameObject caster,
        GameObject target,
        ATBActionModifiers modifiers)
    {
        IEnduranceReceiver receiver = target.GetComponentInParent<IEnduranceReceiver>();
        receiver?.ApplyEnduranceDamage(enduranceDamage * modifiers.PotencyMultiplier);
    }
}

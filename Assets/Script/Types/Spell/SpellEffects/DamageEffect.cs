using UnityEngine;

[CreateAssetMenu(fileName = "DamageEffect", menuName = "Scriptable Objects/Spells/Effects/Damage")]
public class DamageEffect : SpellEffect
{
    public int damage;

    /// <summary>Applique des dégâts à toute cible compatible.</summary>
    public override void Execute(GameObject caster, GameObject target)
    {
        Execute(caster, target, ATBActionModifiers.Normal);
    }

    /// <summary>Applique des dégâts modifiés par le timing ATB capturé.</summary>
    public override void Execute(
        GameObject caster,
        GameObject target,
        ATBActionModifiers modifiers)
    {
        IDamage damageable = target.GetComponentInParent<IDamage>();
        if (damageable != null)
        {
            int modifiedDamage = Mathf.RoundToInt(damage * modifiers.PotencyMultiplier);
            damageable.GiveDamage(modifiedDamage, caster);
        }
    }
}

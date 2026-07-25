using UnityEngine;

public class DamageEffect : SpellEffect
{
    public int damage;
    public override void Execute(
        GameObject caster,
        GameObject target
    )
    {
        IDamage damageable = target.GetComponent<IDamage>();
        if (damageable != null)
        {
            damageable.GiveDamage(damage, target);
        }   
    }
}

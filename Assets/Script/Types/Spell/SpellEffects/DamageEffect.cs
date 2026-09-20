using UnityEngine;

[CreateAssetMenu(fileName = "DamageEffect", menuName = "Scriptable Objects/Spells/Effects/Damage")]
public class DamageEffect : SpellEffect
{
    public int damage;

    /// <summary>Applique des dégâts à toute cible compatible.</summary>
    public override void Execute(GameObject caster, GameObject target)
    {
        IDamage damageable = target.GetComponentInParent<IDamage>();
        if (damageable != null)
        {
            damageable.GiveDamage(damage, caster);
        }
    }
}

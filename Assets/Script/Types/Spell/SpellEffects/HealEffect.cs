using UnityEngine;

public class HealEffect : SpellEffect
{
    
    public int healAmount;
    public override void Execute(
        GameObject caster,
        GameObject target
    )
    {
        /*
        IHealable healable = target.GetComponent<IHealable>();
        if (healable != null)
        {
            healable.Heal(healAmount, target);
        }
        */
    }
}

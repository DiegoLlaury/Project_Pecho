using UnityEngine;

[CreateAssetMenu(fileName = "SpellEffect", menuName = "Scriptable Objects/SpellEffect")]
public abstract class SpellEffect : ScriptableObject
{
    public abstract void Execute(
        GameObject caster,
        GameObject target
    );

    /// <summary>Exécute l'effet avec les multiplicateurs capturés au lancement du sort.</summary>
    public virtual void Execute(
        GameObject caster,
        GameObject target,
        ATBActionModifiers modifiers)
    {
        Execute(caster, target);
    }
}

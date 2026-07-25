using UnityEngine;

[CreateAssetMenu(fileName = "SpellEffect", menuName = "Scriptable Objects/SpellEffect")]
public abstract class SpellEffect : ScriptableObject
{
    public abstract void Execute(
        GameObject caster,
        GameObject target
    );
}

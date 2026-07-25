using UnityEngine;

[CreateAssetMenu(fileName = "EffectsType", menuName = "Scriptable Objects/EffectsType")]
public abstract class EffectsType : ScriptableObject
{
    public abstract void Execute(
        GameObject caster,
        GameObject target
    );
}

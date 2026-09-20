using UnityEngine;

[CreateAssetMenu(fileName = "IdentifyEffect", menuName = "Scriptable Objects/Spells/Effects/Identify")]
public sealed class IdentifyEffect : SpellEffect
{
    /// <summary>Révèle les informations d'identification portées par la cible.</summary>
    public override void Execute(GameObject caster, GameObject target)
    {
        FishIdentificationState state = target.GetComponentInParent<FishIdentificationState>();
        state?.Reveal();
    }
}

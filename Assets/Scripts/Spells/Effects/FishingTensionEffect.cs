using UnityEngine;

[CreateAssetMenu(fileName = "FishingTensionEffect", menuName = "Scriptable Objects/Spells/Effects/Fishing Tension")]
public sealed class FishingTensionEffect : SpellEffect
{
    [SerializeField, Range(0f, 1f)] private float normalizedTensionSpike = 0.35f;

    /// <summary>Ajoute une surtension normalisée à la canne de la cible.</summary>
    public override void Execute(GameObject caster, GameObject target)
    {
        IFishingTensionReceiver receiver = target.GetComponentInParent<IFishingTensionReceiver>();
        receiver?.ApplyTensionSpike(normalizedTensionSpike);
    }
}

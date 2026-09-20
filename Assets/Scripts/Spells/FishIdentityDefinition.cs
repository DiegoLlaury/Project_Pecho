using UnityEngine;

public enum FishElementWeakness
{
    None,
    Fire,
    Lightning,
    Water,
    Wind
}

public enum FishNature
{
    Calm,
    Aggressive,
    Cunning,
    Resilient
}

[CreateAssetMenu(fileName = "FishIdentity", menuName = "Scriptable Objects/Fishing/Fish Identity")]
public sealed class FishIdentityDefinition : ScriptableObject
{
    public string displayName = "Poisson inconnu";
    public FishElementWeakness elementalWeakness = FishElementWeakness.None;
    public FishNature nature = FishNature.Calm;
}

using UnityEngine;

[CreateAssetMenu(fileName = "FishDefinition", menuName = "Scriptable Objects/FishDefinition")]
public sealed class FishDefinition : ScriptableObject
{
    public int maxEndurance;
    public float escapeAcceleration;
    public float maxSpeed;
    public float turnResponsiveness;
    public float resistanceToPull;
    public float lateralResistance;
    public float tensionGainMultiplier;
    public float enduranceDrainMultiplier;
    public float catchRadius;

    void OnValidate()
    {
        maxEndurance = Mathf.Max(1, maxEndurance);
        escapeAcceleration = Mathf.Max(0f, escapeAcceleration);
        maxSpeed = Mathf.Max(0f, maxSpeed);
        turnResponsiveness = Mathf.Max(0f, turnResponsiveness);
        resistanceToPull = Mathf.Clamp01(resistanceToPull);
        lateralResistance = Mathf.Clamp01(lateralResistance);
        tensionGainMultiplier = Mathf.Max(1f, tensionGainMultiplier);
        enduranceDrainMultiplier = Mathf.Max(1f, enduranceDrainMultiplier);
        catchRadius = Mathf.Max(0.2f, catchRadius);
    }
}

using UnityEngine;

[CreateAssetMenu(fileName = "FishDefinition", menuName = "Scriptable Objects/FishDefinition")]
public sealed class FishDefinition : ScriptableObject
{
    public int maxEndurance;
    public float escapeAcceleration;
    public float maxSpeed;
    public float turnResponssiveness;
    public float resistanceToPull;
    public float lateralRestistance;
    public float tensionGainMultiplier;
    public float enduranceDrainMultiplier;
    public float catchRadius;

    void OnValidate()
    {
        maxEndurance = Mathf.Max(1, maxEndurance);
        escapeAcceleration = Mathf.Max(0f, escapeAcceleration);
        maxSpeed = Mathf.Max(0f, maxSpeed);
        turnResponssiveness = Mathf.Max(0f, turnResponssiveness);
        resistanceToPull = Mathf.Max(0f, resistanceToPull);
        lateralRestistance = Mathf.Max(0f, lateralRestistance);
        tensionGainMultiplier = Mathf.Max(0f, tensionGainMultiplier);
        enduranceDrainMultiplier = Mathf.Max(0f, enduranceDrainMultiplier);
        catchRadius = Mathf.Max(0f, catchRadius);
    }
}

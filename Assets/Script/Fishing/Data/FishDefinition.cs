using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Profil de comportement d'un poisson. Toutes les valeurs ont un défaut jouable.
/// </summary>
[CreateAssetMenu(fileName = "FishDefinition", menuName = "Scriptable Objects/FishDefinition")]
public sealed class FishDefinition : ScriptableObject
{
    [Header("Endurance")]
    [Min(1f)] public float maxEndurance = 100f;
    [Min(0f)] public float enduranceDrainMultiplier = 1f;
    [Min(0f)] public float enduranceRecoveryPerSecond = 4f;

    [Header("Force")]
    [FormerlySerializedAs("escapeAcceleration")]
    [Min(0f)] public float escapeForce = 5f;
    [Range(0f, 1f)] public float idleForceMultiplier = 0.3f;
    [Range(0f, 1f)] public float exhaustedForceMultiplier = 0.15f;
    [Range(0f, 1f)] public float resistanceToPull = 0.25f;
    [Range(0f, 1f)] public float lateralResistance = 0.2f;

    [Header("Movement")]
    [Min(0f)] public float maxSpeed = 4f;
    [Min(0f)] public float drag = 2f;
    [Min(0f)] public float turnResponsiveness = 8f;

    [Header("Bursts")]
    [Min(1f)] public float burstForceMultiplier = 1.6f;
    [Min(0f)] public float burstDuration = 1.5f;
    [Min(0f)] public float minTimeBetweenBursts = 3f;
    [Min(0f)] public float maxTimeBetweenBursts = 6f;
    [Range(0f, 1f)] public float burstMinEndurance = 0.15f;

    [Header("Line")]
    [Min(0f)] public float tensionGainMultiplier = 1f;

    private void OnValidate()
    {
        maxTimeBetweenBursts = Mathf.Max(minTimeBetweenBursts, maxTimeBetweenBursts);
    }
}
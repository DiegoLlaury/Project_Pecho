using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Profil de comportement d'un poisson. Toutes les valeurs ont un défaut jouable.
/// </summary>
[CreateAssetMenu(fileName = "FishDefinition", menuName = "Scriptable Objects/FishDefinition")]
public sealed class FishDefinition : ScriptableObject
{
    private const float MinimumStateDuration = 0.05f;
    private const float MaximumReactionDelay = 2f;

    [Header("Combat State Durations")]
    [Range(0f, 1f)] public float captureEnduranceRatio = 0.1f;
    [Min(MinimumStateDuration)] public float minStruggleDuration = 1.2f;
    [Min(MinimumStateDuration)] public float maxStruggleDuration = 2.2f;
    [Min(MinimumStateDuration)] public float minLateralDodgeDuration = 0.7f;
    [Min(MinimumStateDuration)] public float maxLateralDodgeDuration = 1.1f;
    [Min(MinimumStateDuration)] public float minBurstWindupDuration = 0.4f;
    [Min(MinimumStateDuration)] public float maxBurstWindupDuration = 0.6f;
    [Min(MinimumStateDuration)] public float minBurstDuration = 0.65f;
    [Min(MinimumStateDuration)] public float maxBurstDuration = 0.9f;
    [Min(MinimumStateDuration)] public float minRecoveryDuration = 0.65f;
    [Min(MinimumStateDuration)] public float maxRecoveryDuration = 1f;
    [Min(MinimumStateDuration)] public float fishBreakDuration = 2.5f;
    [Min(MinimumStateDuration)] public float shoreReboundDuration = 0.45f;

    [Header("State Effort")]
    [Tooltip("Fatigue intégrée une seule fois à EffortMultiplier ; résistance décroissante avec l'endurance.")]
    [Range(0f, 1f)] public float tiredEffortMultiplier = 0.4f;
    [Min(0f)] public float lateralDodgeEffortMultiplier = 1.1f;
    [Range(0f, 1f)] public float burstWindupEffortMultiplier = 0.35f;
    [Range(0f, 1f)] public float castingEffortMultiplier = 0.15f;
    [Range(0f, 1f)] public float recoveryEffortMultiplier = 0.3f;
    [Range(0f, 1f)] public float fishBreakEffortMultiplier = 0.05f;

    [Header("Delayed Reaction and Steering")]
    [Range(0f, MaximumReactionDelay)] public float minReactionDelay = 0.2f;
    [Range(0f, MaximumReactionDelay)] public float maxReactionDelay = 0.35f;
    [Min(MinimumStateDuration)] public float sustainedGuidanceDuration = 0.25f;
    [Range(0f, 1f)] public float guidanceInputThreshold = 0.2f;
    [Min(0f)] public float playerVelocityGuidanceWeight = 0.2f;
    [Min(0f)] public float struggleSteeringResponse = 4f;
    [Range(0f, 1f)] public float struggleLateralStrength = 0.65f;
    [Range(0f, 1f)] public float dodgeForwardStrength = 0.25f;
    [Min(0f)] public float minimumDodgeSpace = 1.5f;
    [Tooltip("Correction maximale relative au cap annoncé ; ne s'accumule jamais par frame.")]
    [Range(0f, 30f)] public float burstHeadingCorrectionDegrees = 8f;

    [Header("Decision Weights")]
    [Min(0f)] public float burstDecisionWeight = 1f;
    [Min(0f)] public float dodgeDecisionWeight = 1f;
    [Min(0f)] public float spellDecisionWeight = 1f;
    [Min(0f)] public float pullPressureDecisionWeight = 1f;
    [Min(0f)] public float sustainedGuidanceDecisionWeight = 2f;

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
    [Tooltip("Ancien champ conservé ; la machine à états utilise minBurstDuration/maxBurstDuration.")]
    [Min(0f)] public float burstDuration = 1.5f;
    [Min(0f)] public float minTimeBetweenBursts = 4f;
    [Min(0f)] public float maxTimeBetweenBursts = 6f;
    [Range(0f, 1f)] public float burstMinEndurance = 0.15f;

    [Header("Line")]
    [Min(0f)] public float tensionGainMultiplier = 1f;

    private void OnValidate()
    {
        ValidateInterval(ref minStruggleDuration, ref maxStruggleDuration, MinimumStateDuration);
        ValidateInterval(ref minLateralDodgeDuration, ref maxLateralDodgeDuration, MinimumStateDuration);
        ValidateInterval(ref minBurstWindupDuration, ref maxBurstWindupDuration, MinimumStateDuration);
        ValidateInterval(ref minBurstDuration, ref maxBurstDuration, MinimumStateDuration);
        ValidateInterval(ref minRecoveryDuration, ref maxRecoveryDuration, MinimumStateDuration);
        ValidateInterval(ref minTimeBetweenBursts, ref maxTimeBetweenBursts, 0f);
        minReactionDelay = Mathf.Clamp(AtLeast(minReactionDelay, 0f), 0f, MaximumReactionDelay);
        maxReactionDelay = Mathf.Clamp(AtLeast(maxReactionDelay, minReactionDelay), minReactionDelay, MaximumReactionDelay);
        fishBreakDuration = AtLeast(fishBreakDuration, MinimumStateDuration);
        shoreReboundDuration = AtLeast(shoreReboundDuration, MinimumStateDuration);
        sustainedGuidanceDuration = AtLeast(sustainedGuidanceDuration, MinimumStateDuration);
        captureEnduranceRatio = Unit(captureEnduranceRatio);
        tiredEffortMultiplier = Unit(tiredEffortMultiplier);
        guidanceInputThreshold = Unit(guidanceInputThreshold);
        playerVelocityGuidanceWeight = AtLeast(playerVelocityGuidanceWeight, 0f);
        struggleSteeringResponse = AtLeast(struggleSteeringResponse, 0f);
        struggleLateralStrength = Unit(struggleLateralStrength);
        dodgeForwardStrength = Unit(dodgeForwardStrength);
        minimumDodgeSpace = AtLeast(minimumDodgeSpace, 0f);
        burstHeadingCorrectionDegrees = Mathf.Clamp(AtLeast(burstHeadingCorrectionDegrees, 0f), 0f, 30f);
        burstDecisionWeight = AtLeast(burstDecisionWeight, 0f);
        dodgeDecisionWeight = AtLeast(dodgeDecisionWeight, 0f);
        spellDecisionWeight = AtLeast(spellDecisionWeight, 0f);
        pullPressureDecisionWeight = AtLeast(pullPressureDecisionWeight, 0f);
        sustainedGuidanceDecisionWeight = AtLeast(sustainedGuidanceDecisionWeight, 0f);
        lateralDodgeEffortMultiplier = AtLeast(lateralDodgeEffortMultiplier, 0f);
        burstWindupEffortMultiplier = Unit(burstWindupEffortMultiplier);
        castingEffortMultiplier = Unit(castingEffortMultiplier);
        recoveryEffortMultiplier = Unit(recoveryEffortMultiplier);
        fishBreakEffortMultiplier = Unit(fishBreakEffortMultiplier);
        maxEndurance = AtLeast(maxEndurance, 1f);
        enduranceDrainMultiplier = AtLeast(enduranceDrainMultiplier, 0f);
        enduranceRecoveryPerSecond = AtLeast(enduranceRecoveryPerSecond, 0f);
        escapeForce = AtLeast(escapeForce, 0f);
        idleForceMultiplier = Unit(idleForceMultiplier);
        exhaustedForceMultiplier = Unit(exhaustedForceMultiplier);
        resistanceToPull = Unit(resistanceToPull);
        lateralResistance = Unit(lateralResistance);
        maxSpeed = AtLeast(maxSpeed, 0f);
        drag = AtLeast(drag, 0f);
        turnResponsiveness = AtLeast(turnResponsiveness, 0f);
        burstForceMultiplier = AtLeast(burstForceMultiplier, 1f);
        burstDuration = AtLeast(burstDuration, 0f);
        burstMinEndurance = Unit(burstMinEndurance);
        tensionGainMultiplier = AtLeast(tensionGainMultiplier, 0f);
    }

    private static void ValidateInterval(ref float minimum, ref float maximum, float lowerBound)
    {
        minimum = AtLeast(minimum, lowerBound);
        maximum = AtLeast(maximum, minimum);
    }

    private static float Unit(float value) => Mathf.Clamp01(AtLeast(value, 0f));

    private static float AtLeast(float value, float minimum) =>
        float.IsNaN(value) || float.IsInfinity(value) ? minimum : Mathf.Max(minimum, value);
}
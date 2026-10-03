using UnityEngine;

/// <summary>Instantané immuable du dernier pas physique, sans accès aux ressources de la session.</summary>
public readonly struct FishCombatContext
{
    /// <summary>Position physique du poisson.</summary>
    public Vector3 FishPosition { get; }
    /// <summary>Vitesse horizontale effective du poisson.</summary>
    public Vector3 FishVelocity { get; }
    /// <summary>Position du joueur.</summary>
    public Vector3 PlayerPosition { get; }
    /// <summary>Déplacement horizontal effectif du joueur, hors saut.</summary>
    public Vector3 PlayerVelocity { get; }
    /// <summary>Pression de traction, entre zéro et un.</summary>
    public float PullInput { get; }
    /// <summary>Relâchement, entre zéro et un.</summary>
    public float ReleaseInput { get; }
    /// <summary>Guidage signé, entre moins un et un.</summary>
    public float LateralInput { get; }
    /// <summary>Endurance restante, entre zéro et un.</summary>
    public float NormalizedEndurance { get; }
    /// <summary>Tension du fil, entre zéro et un.</summary>
    public float NormalizedTension { get; }

    /// <summary>Capture les données de décision et neutralise les entrées non finies.</summary>
    public FishCombatContext(
        Vector3 fishPosition, Vector3 fishVelocity,
        Vector3 playerPosition, Vector3 playerVelocity,
        float pullInput, float releaseInput, float lateralInput,
        float normalizedEndurance, float normalizedTension)
    {
        FishPosition = FiniteVector(fishPosition);
        FishVelocity = Horizontal(fishVelocity);
        PlayerPosition = FiniteVector(playerPosition);
        PlayerVelocity = Horizontal(playerVelocity);
        PullInput = Mathf.Clamp01(Finite(pullInput));
        ReleaseInput = Mathf.Clamp01(Finite(releaseInput));
        LateralInput = Mathf.Clamp(Finite(lateralInput), -1f, 1f);
        NormalizedEndurance = Mathf.Clamp01(Finite(normalizedEndurance));
        NormalizedTension = Mathf.Clamp01(Finite(normalizedTension));
    }

    private static Vector3 Horizontal(Vector3 value)
    {
        Vector3 horizontal = FiniteVector(value);
        horizontal.y = 0f;
        return horizontal;
    }

    private static Vector3 FiniteVector(Vector3 value) =>
        new Vector3(Finite(value.x), Finite(value.y), Finite(value.z));

    private static float Finite(float value) =>
        float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
}

/// <summary>Action principale exclusive du poisson, distincte du cycle de session et de la rupture du fil.</summary>
public enum FishCombatState
{
    Struggle,
    LateralDodge,
    BurstWindup,
    Burst,
    CastSpell,
    Recovery,
    ShoreRebound,
    Break,
    Exhausted
}

/// <summary>Origine d'un break ; coûts, impulsions et immunités appartiennent à la session.</summary>
public enum FishBreakCause
{
    Elemental,
    Shore,
    Environment
}

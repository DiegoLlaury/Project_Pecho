using UnityEngine;

public sealed class SpellAffiliation : MonoBehaviour
{
    [SerializeField] private SpellTeam team = SpellTeam.Neutral;

    public SpellTeam Team => team;
}

using UnityEngine;

[CreateAssetMenu(fileName = "SpellType", menuName = "Scriptable Objects/Spells/Spell")]
public class SpellType : ScriptableObject
{
    [Header("Identity")]
    public string spellName;
    public Sprite icon;

    [Header("Timing")]
    [Min(0f)] public float cooldown = 4f;
    [Min(0f)] public float minimumWarningDuration = 1f;
    [Min(0f)] public float maximumWarningDuration = 3f;
    public int manaCost;

    [Header("Cost")]
    [Min(0f)] public float enduranceCost = 20f;

    [Header("Impact")]
    [Min(0f)] public float impactRadius = 2f;
    [Min(0.05f)] public float impactVisualDuration = 0.5f;
    public SpellEffect[] effects;

    [Header("Presentation")]
    public GameObject telegraphPrefab;
    public GameObject projectilePrefab;
    public GameObject impactPrefab;

    private void OnValidate()
    {
        maximumWarningDuration = Mathf.Max(minimumWarningDuration, maximumWarningDuration);
    }
}

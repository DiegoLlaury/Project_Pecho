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
    public SpellDeliveryMode deliveryMode = SpellDeliveryMode.Projectile;
    [Tooltip("Hauteur maximale ajoutée à la trajectoire du projectile.")]
    [Min(0f)] public float projectileArcHeight = 3f;
    public GameObject telegraphPrefab;
    public GameObject projectilePrefab;
    public GameObject impactPrefab;

    private void OnValidate()
    {
        cooldown = Mathf.Max(0f, cooldown);
        minimumWarningDuration = Mathf.Max(0f, minimumWarningDuration);
        maximumWarningDuration = Mathf.Max(minimumWarningDuration, maximumWarningDuration);
        manaCost = Mathf.Max(0, manaCost);
        enduranceCost = Mathf.Max(0f, enduranceCost);
        impactRadius = Mathf.Max(0f, impactRadius);
        impactVisualDuration = Mathf.Max(0.05f, impactVisualDuration);
        projectileArcHeight = Mathf.Max(0f, projectileArcHeight);
    }
}

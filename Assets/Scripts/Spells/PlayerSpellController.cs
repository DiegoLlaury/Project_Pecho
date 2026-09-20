using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerSpellController : MonoBehaviour
{
    private const int MaximumSpellSlots = 4;

    [SerializeField] private SpellCaster spellCaster;
    [SerializeField] private CombatMana combatMana;
    [SerializeField] private FishMovementController targetFish;
    [SerializeField] private ATBTimingController atbTimingController;
    [SerializeField] private SpellType[] equippedSpells = new SpellType[MaximumSpellSlots];

    public int SlotCount => Mathf.Min(MaximumSpellSlots, equippedSpells?.Length ?? 0);

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.digit1Key.wasPressedThisFrame) TryCastSlot(0);
        else if (keyboard.digit2Key.wasPressedThisFrame) TryCastSlot(1);
        else if (keyboard.digit3Key.wasPressedThisFrame) TryCastSlot(2);
        else if (keyboard.digit4Key.wasPressedThisFrame) TryCastSlot(3);
    }

    private void OnValidate()
    {
        if (equippedSpells == null || equippedSpells.Length != MaximumSpellSlots)
        {
            System.Array.Resize(ref equippedSpells, MaximumSpellSlots);
        }
    }

    /// <summary>Tente de lancer le sort équipé dans l'emplacement demandé.</summary>
    public bool TryCastSlot(int slotIndex)
    {
        SpellType spell = GetSpell(slotIndex);
        if (spell == null || spellCaster == null || combatMana == null || targetFish == null)
        {
            return false;
        }

        if (!combatMana.CanSpend(spell.manaCost) || !spellCaster.IsReady(spell))
        {
            return false;
        }

        float predictedTravelDuration = Mathf.Max(
            0f,
            (spell.minimumWarningDuration + spell.maximumWarningDuration) * 0.5f);
        Vector3 predictedTargetPosition = targetFish.Position +
                                          targetFish.CurrentVelocity * predictedTravelDuration;

        ATBActionModifiers modifiers = atbTimingController != null
            ? atbTimingController.CaptureAction()
            : ATBActionModifiers.Normal;

        if (!combatMana.TrySpend(spell.manaCost))
        {
            return false;
        }

        bool castStarted = spellCaster.TryCast(
            spell,
            targetFish.gameObject,
            predictedTargetPosition,
            modifiers);

        if (!castStarted)
        {
            combatMana.Restore(spell.manaCost);
        }

        return castStarted;
    }

    /// <summary>Retourne le sort équipé dans un emplacement ou null.</summary>
    public SpellType GetSpell(int slotIndex)
    {
        if (equippedSpells == null || slotIndex < 0 || slotIndex >= SlotCount)
        {
            return null;
        }

        return equippedSpells[slotIndex];
    }

    /// <summary>Retourne la progression normalisée restante du cooldown.</summary>
    public float GetNormalizedCooldown(int slotIndex)
    {
        SpellType spell = GetSpell(slotIndex);
        return spellCaster == null || spell == null
            ? 0f
            : spellCaster.GetNormalizedCooldown(spell);
    }

    /// <summary>Indique si le sort peut être payé avec le mana actuel.</summary>
    public bool HasEnoughMana(int slotIndex)
    {
        SpellType spell = GetSpell(slotIndex);
        return spell != null && combatMana != null && combatMana.CanSpend(spell.manaCost);
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerSpellHudPresenter : MonoBehaviour
{
    private const string ProgressPropertyName = "_Progress";
    private const string RuntimeMaterialSuffix = " (Runtime)";
    private const int MaximumSpellSlots = 4;

    [SerializeField] private PlayerSpellController spellController;
    [SerializeField] private CombatMana combatMana;
    [SerializeField] private Image manaProgressBar;
    [SerializeField] private Image[] spellIcons = new Image[MaximumSpellSlots];
    [SerializeField] private Image[] cooldownOverlays = new Image[MaximumSpellSlots];
    [SerializeField] private TMP_Text[] keyLabels = new TMP_Text[MaximumSpellSlots];

    private Material manaRuntimeMaterial;

    private void Awake()
    {
        if (manaProgressBar != null && manaProgressBar.material != null)
        {
            manaRuntimeMaterial = new Material(manaProgressBar.material)
            {
                name = manaProgressBar.material.name + RuntimeMaterialSuffix
            };
            manaProgressBar.material = manaRuntimeMaterial;
        }

        ConfigureSlots();
        Refresh();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void ConfigureSlots()
    {
        for (int slotIndex = 0; slotIndex < MaximumSpellSlots; slotIndex++)
        {
            SpellType spell = spellController != null ? spellController.GetSpell(slotIndex) : null;
            if (spellIcons != null && slotIndex < spellIcons.Length && spellIcons[slotIndex] != null)
            {
                spellIcons[slotIndex].sprite = spell != null ? spell.icon : null;
                spellIcons[slotIndex].preserveAspect = true;
            }

            if (cooldownOverlays != null && slotIndex < cooldownOverlays.Length && cooldownOverlays[slotIndex] != null)
            {
                cooldownOverlays[slotIndex].type = Image.Type.Filled;
                cooldownOverlays[slotIndex].fillMethod = Image.FillMethod.Radial360;
                cooldownOverlays[slotIndex].fillOrigin = (int)Image.Origin360.Top;
                cooldownOverlays[slotIndex].fillClockwise = false;
            }

            if (keyLabels != null && slotIndex < keyLabels.Length && keyLabels[slotIndex] != null)
            {
                keyLabels[slotIndex].text = (slotIndex + 1).ToString();
            }
        }
    }

    private void Refresh()
    {
        if (manaRuntimeMaterial != null && combatMana != null)
        {
            manaRuntimeMaterial.SetFloat(ProgressPropertyName, Mathf.Clamp01(combatMana.NormalizedMana));
        }

        for (int slotIndex = 0; slotIndex < MaximumSpellSlots; slotIndex++)
        {
            float cooldown = spellController != null
                ? spellController.GetNormalizedCooldown(slotIndex)
                : 0f;

            if (cooldownOverlays != null && slotIndex < cooldownOverlays.Length && cooldownOverlays[slotIndex] != null)
            {
                cooldownOverlays[slotIndex].fillAmount = cooldown;
                cooldownOverlays[slotIndex].enabled = cooldown > 0f;
            }

            if (spellIcons != null && slotIndex < spellIcons.Length && spellIcons[slotIndex] != null)
            {
                bool available = spellController != null &&
                                 spellController.HasEnoughMana(slotIndex) &&
                                 cooldown <= 0f;
                spellIcons[slotIndex].color = available ? Color.white : new Color(0.35f, 0.35f, 0.35f, 1f);
            }
        }
    }

    private void OnDestroy()
    {
        if (manaRuntimeMaterial != null)
        {
            Destroy(manaRuntimeMaterial);
        }
    }
}

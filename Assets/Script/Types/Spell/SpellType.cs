using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SpellType", menuName = "Scriptable Objects/SpellType")]
public class SpellType : ScriptableObject
{
    public string spellName;
    public Sprite icon;

    public float cooldown;
    public int manaCost;

    //public SpellEffect[] effects;
}

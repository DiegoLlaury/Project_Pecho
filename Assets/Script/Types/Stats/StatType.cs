using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "Stat_Type", menuName = "Scriptable Objects/Stat_Type")]
public class StatType : ScriptableObject
{
    public int level = 0;
    public int strength = 0;
    public int agility = 0;
    public int stamina = 0;
    public int magic = 0;
    public int elementalDamage = 0;
    public int elementalResistance = 0;
    public int luck = 0;

    public bool isInvincible = false;   
}

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class RuntimeStats
{
    public int level;
    public int strength;
    public int agility;
    public int stamina;
    public int magic;
    public int elementalDamage;
    public int elementalResistance;
    public int luck;

    public bool isInvincible;

    public RuntimeStats(StatType baseStats)
    {
        level = baseStats.level;
        strength = baseStats.strength;
        agility = baseStats.agility;
        stamina = baseStats.stamina;
        magic = baseStats.magic;
        elementalDamage = baseStats.elementalDamage;
        elementalResistance = baseStats.elementalResistance;
        luck = baseStats.luck;
        isInvincible = baseStats.isInvincible;
    }

    public void SetStat(string statName, object statValue)
    {
        FieldInfo field = typeof(StatType).GetField(
            statName,
            BindingFlags.Public | BindingFlags.Instance
        );

        // La stat n'existe pas
        if (field == null)
        {
            Debug.LogWarning(
                $"La stat '{statName}' n'existe pas dans {nameof(StatType)}."
            );
            return;
        }

        // Vérifie si la valeur peut être convertie dans le type du champ
        try
        {
            object convertedValue = Convert.ChangeType(
                statValue,
                field.FieldType
            );

            field.SetValue(this, convertedValue);
        }
        catch (Exception)
        {
            Debug.LogWarning(
                $"Impossible de définir '{statName}' avec la valeur '{statValue}'. " +
                $"Le type attendu est {field.FieldType.Name}."
            );
        }
    }

    public void SetStats(params KeyValuePair<string, object>[] stats)
    {
        foreach (var stat in stats)
        {
            SetStat(stat.Key, stat.Value);
        }
    }

    public void AddToStat(string statName, object statValue)
    {
        FieldInfo field = typeof(StatType).GetField(
        statName,
        BindingFlags.Public | BindingFlags.Instance
        );

        if (field == null)
        {
            Debug.LogWarning(
                $"La stat '{statName}' n'existe pas dans {nameof(StatType)}."
            );
            return;
        }

        try
        {
            object currentValue = field.GetValue(this);

            object convertedValue = Convert.ChangeType(
                statValue,
                field.FieldType
            );

            // Addition pour les int
            if (field.FieldType == typeof(int))
            {
                int current = (int)currentValue;
                int value = (int)convertedValue;

                field.SetValue(this, current + value);
            }

            // Addition pour les float
            else if (field.FieldType == typeof(float))
            {
                float current = (float)currentValue;
                float value = (float)convertedValue;

                field.SetValue(this, current + value);
            }

            // Addition pour les double
            else if (field.FieldType == typeof(double))
            {
                double current = (double)currentValue;
                double value = (double)convertedValue;

                field.SetValue(this, current + value);
            }

            // Addition pour les long
            else if (field.FieldType == typeof(long))
            {
                long current = (long)currentValue;
                long value = (long)convertedValue;

                field.SetValue(this, current + value);
            }

            else
            {
                Debug.LogWarning(
                    $"La stat '{statName}' est de type " +
                    $"{field.FieldType.Name} et ne peut pas être additionnée."
                );
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                $"Impossible d'ajouter '{statValue}' à la stat '{statName}'. " +
                $"Erreur : {e.Message}"
            );
        }

    }

    public void AddToStats(params KeyValuePair<string, object>[] stats)
    {
        foreach (var stat in stats)
        {
            AddToStat(stat.Key, stat.Value);
        }
    }

    public object GetStat(string statName)
    {
        FieldInfo field = typeof(StatType).GetField(
            statName,
            BindingFlags.Public | BindingFlags.Instance
        );
        // La stat n'existe pas
        if (field == null)
        {
            Debug.LogWarning(
                $"La stat '{statName}' n'existe pas dans {nameof(StatType)}."
            );
            return null;
        }
        return field.GetValue(this);
    }

    public object[] GetStats(params string[] statNames)
    {
        List<object> stats = new List<object>();
        foreach (var statName in statNames)
        {
            stats.Add(GetStat(statName));
        }
        return stats.ToArray();
    }

}

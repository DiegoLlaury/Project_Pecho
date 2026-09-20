using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "CollectibleType", menuName = "Scriptable Objects/CollectibleType")]
public class CollectibleType : ScriptableObject
{
    public string ID;
    public string Name;
    /// <summary>
    /// a completer
    /// </summary>

    public bool isInvincible = false;
}

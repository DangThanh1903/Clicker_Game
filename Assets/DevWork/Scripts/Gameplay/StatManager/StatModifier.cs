using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct StatModifier
{
    public StatType statType;
    public float value;

    public static string GetFormattedModifiers(IEnumerable<StatModifier> modifiers)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        foreach (var mod in modifiers)
        {
            bool positive = mod.value >= 0;

            string color = positive ? "#00FF00" : "#FF5050"; // green/red  
            string sign  = positive ? "+" : "-";

            sb.AppendLine(
                $"<color={color}><b>{sign}{Mathf.Abs(mod.value)} {mod.statType}</b></color>"
            );
        }

        return sb.ToString();
    }


}

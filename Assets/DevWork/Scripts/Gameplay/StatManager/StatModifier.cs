using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct StatModifier
{
    public StatType statType;
    public float value;
    public StatModifierMode mode;

    public static string GetFormattedModifiers(IEnumerable<StatModifier> modifiers)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        foreach (var mod in modifiers)
        {
            sb.AppendLine(FormatSingle(mod));
        }

        return sb.ToString();
    }

    public static string FormatSingle(StatModifier mod, float? valueOverride = null)
    {
        float v = valueOverride ?? mod.value;
        if (mod.mode == StatModifierMode.Multiply)
        {
            bool positiveMul = v >= 1f;
            string color = positiveMul ? "#00FF00" : "#FF5050";
            return $"<color={color}><b>x{v:0.###} {mod.statType}</b></color>";
        }

        bool positiveAdd = v >= 0f;
        string addColor = positiveAdd ? "#00FF00" : "#FF5050";
        string sign = positiveAdd ? "+" : "-";
        return $"<color={addColor}><b>{sign}{Mathf.Abs(v):0.###} {mod.statType}</b></color>";
    }

}

public enum StatModifierMode
{
    Add,
    Multiply
}

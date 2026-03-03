using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Buff System/BuffSO")]
public abstract class BuffSO : ScriptableObject
{
    public string buffName;
    public List<StatModifier> modifiers = new List<StatModifier>();
    public Sprite buffIcon;
    public float duration;
    public bool isStackable = false;
    public int maxStack = 1;
    public abstract bool IsPermanent { get; }

    public IEnumerable<StatModifier> GetEffectiveModifiers(int stackCount)
    {
        foreach (var mod in modifiers)
        {
            float effectiveValue = mod.value;
            if (isStackable && stackCount > 1)
            {
                if (mod.mode == StatModifierMode.Multiply)
                {
                    // Repeated multiplicative stacks: x1.1 with 3 stacks => x1.331.
                    effectiveValue = mod.value > 0f ? Mathf.Pow(mod.value, stackCount) : mod.value;
                }
                else
                {
                    effectiveValue = mod.value * stackCount;
                }
            }

            yield return new StatModifier
            {
                statType = mod.statType,
                value = effectiveValue,
                mode = mod.mode
            };
        }
    }
}

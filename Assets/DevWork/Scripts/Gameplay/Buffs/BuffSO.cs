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
        float stackMult = isStackable ? stackCount : 1f;

        foreach (var mod in modifiers)
        {
            yield return new StatModifier
            {
                statType = mod.statType,
                value    = mod.value * stackMult
            };
        }
    }
}
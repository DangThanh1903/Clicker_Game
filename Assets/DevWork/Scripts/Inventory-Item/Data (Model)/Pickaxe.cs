using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PickaxeType
{
    Normal,
    Hold,
    Idle
}

[CreateAssetMenu(fileName = "Pickaxe", menuName = "Inventory/Items/Pickaxe")]
public class Pickaxe : Item, IStatProvider
{
    [SerializeField] private List<StatModifier> modifiers;
    [SerializeField] private List<BuffSO> passiveBuffs;
    public override ItemType Type => ItemType.Pickaxe;
    public PickaxeType currentState = PickaxeType.Normal;
    public ItemPrefix itemPrefix;

    public IEnumerable<StatModifier> GetStatModifiers()
    {
        return modifiers;
    }
    public IEnumerable<BuffSO> GetPassiveBuffs()
    {
        return passiveBuffs;
    }
}

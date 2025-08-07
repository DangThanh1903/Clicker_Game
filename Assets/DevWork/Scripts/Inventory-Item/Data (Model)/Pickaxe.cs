using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Pickaxe", menuName = "Inventory/Items/Pickaxe")]
public class Pickaxe : Item, IStatProvider
{
    [SerializeField] private List<StatModifier> modifiers;
    public override ItemType Type => ItemType.Pickaxe;

    public IEnumerable<StatModifier> GetStatModifiers()
    {
        return modifiers;
    }
}

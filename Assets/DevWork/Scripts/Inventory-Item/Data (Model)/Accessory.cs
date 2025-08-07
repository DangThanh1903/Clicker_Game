using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Accessory", menuName = "Inventory/Items/Accessory")]
public class Accessory : Item, IStatProvider
{
    [SerializeField] private List<StatModifier> modifiers;
    public override ItemType Type => ItemType.Accessory;

    public IEnumerable<StatModifier> GetStatModifiers()
    {
        return modifiers;
    }
}

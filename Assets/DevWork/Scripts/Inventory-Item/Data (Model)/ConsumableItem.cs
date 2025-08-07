using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ConsumableItem", menuName = "Inventory/Items/ConsumableItem")]
public class ConsumableItem : Item
{
    public override ItemType Type => ItemType.Consumable;
    public override int MaxStack => 16;
}

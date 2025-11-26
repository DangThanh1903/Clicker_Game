using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Material", menuName = "Inventory/Items/Material")]
public class ItemMaterial : Item
{
    public override ItemType Type => ItemType.ItemMaterial;
    public override int MaxStack => 32;
}

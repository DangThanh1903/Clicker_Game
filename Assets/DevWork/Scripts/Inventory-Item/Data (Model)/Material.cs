using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Material", menuName = "Inventory/Items/Material")]
public class Material : Item
{
    public override ItemType Type => ItemType.Material;
    public override int MaxStack => 32;
}

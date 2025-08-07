using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class SlotAcceptRuleSO : ScriptableObject
{
    public abstract bool CanAccept(Item item);

    public abstract bool CanAccept(InventoryItem inventoryItem);

}

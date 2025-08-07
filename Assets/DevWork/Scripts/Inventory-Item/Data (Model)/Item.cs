
using UnityEngine;

public enum ItemType
{
    None,
    Pickaxe,
    Consumable,
    Accessory,
    Material
}

public abstract class Item : ScriptableObject
{
    public string itemName;
    [TextArea] public string description;
    public Sprite icon;
    public virtual ItemType Type => ItemType.None;
    public virtual int MaxStack => 1;

    public virtual void Use(GameObject user)
    {
        Debug.Log($"Used item: {itemName}");
    }
}

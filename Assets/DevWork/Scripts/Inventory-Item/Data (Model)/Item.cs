
using UnityEngine;

public enum ItemType
{
    None,
    Pickaxe,
    Consumable,
    Accessory,
    Material,
    BossSummoner
}

public enum ItemPrefix
{
    Gigachad,
    Greedy,
    Overclocked,
    Mighty,
    OPM,
    Bruh,
    Unlucky,
    Shit
}

public abstract class Item : ScriptableObject
{
    public string itemName;
    [TextArea] public string description;
    public Sprite icon;
    public virtual ItemType Type => ItemType.None;
    public BlockSpawnLocation Location;
    public virtual int MaxStack => 1;
}

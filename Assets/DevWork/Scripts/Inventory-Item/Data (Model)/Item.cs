
using UnityEngine;

public enum ItemType
{
    None,
    Pickaxe,
    Consumable,
    Accessory,
    ItemMaterial,
    BossSummoner,
    Lootbox
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
public enum Rarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public abstract class Item : ScriptableObject
{
    public string itemName;
    [TextArea] public string description;
    public Sprite icon;
    public virtual ItemType Type => ItemType.None;
    public Rarity rarity;
    public virtual int MaxStack => 1;
    public string GetFormattedDescription()
    {
        string header = $"<size=120%>{GetColoredName()}</size>";
        string body   = description;

        return $"{header}\n\n{body}";
    }
    public string GetColoredName()
    {
        string colorHex = RarityColors.GetColorHex(rarity);
        return $"<color={colorHex}>{itemName}</color>";
    }
}

public static class RarityColors
{
    public static string GetColorHex(Rarity rarity)
    {
        return rarity switch
        {
            Rarity.Common    => "#FFFFFF", // white
            Rarity.Uncommon  => "#1EFF00", // green
            Rarity.Rare      => "#0070FF", // blue
            Rarity.Epic      => "#A335EE", // purple
            Rarity.Legendary => "#FF8000", // orange
            _                => "#FFFFFF"
        };
    }
}

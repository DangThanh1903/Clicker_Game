
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
public enum Rarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary,
    Exclusive
}

public abstract class Item : ScriptableObject
{
    public string itemName;
    [TextArea] public string description;
    public Sprite icon;
    public virtual ItemType Type => ItemType.None;
    public Rarity rarity;

    [Header("World Visual")]
    public Mesh worldMesh;
    public Material worldFrontMaterial;
    public Material worldSideMaterial;

    public virtual int MaxStack => 1;

    public bool TryGetWorldVisual(out Mesh mesh, out Material frontMaterial, out Material sideMaterial)
    {
        mesh = worldMesh;
        frontMaterial = worldFrontMaterial;
        sideMaterial = worldSideMaterial;
        return mesh != null && frontMaterial != null;
    }

    public string GetFormattedDescription()
    {
        string header = $"<size=120%>{GetColoredName()}</size>";
        string body   = GetBodyText();

        return $"{header}\n\n{body}";
    }

    protected virtual string GetBodyText()
    {
        return description;
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
            Rarity.Exclusive => "#00FFFF", // cyan (rainbow uses dynamic color)
            _                => "#FFFFFF"
        };
    }
}

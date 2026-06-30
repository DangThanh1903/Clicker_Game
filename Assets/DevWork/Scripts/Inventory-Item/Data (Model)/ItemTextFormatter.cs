public static class ItemTextFormatter
{
    public static string GetFormattedDescription(InventoryItem invItem)
    {
        if (invItem == null || invItem.itemData == null)
            return "";

        Item item = invItem.itemData;

        // ----- Name with prefix -----
        string name = item.itemName;

        if (ItemPrefixConfig.TryGetDisplayName(invItem.prefix, out string prefixName))
            name = $"{prefixName} {name}";

        string colorHex = RarityColors.GetColorHex(item.rarity);
        string header = $"<size=120%><color={colorHex}>{name}</color></size>";

        // ----- Body -----
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(item.description);

        // ----- Prefix stats -----
        var prefixMods = ItemPrefixConfig.GetFlatMods(invItem.prefix);
        if (prefixMods.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<b>Prefix Bonus:</b>");
            foreach (var m in prefixMods)
                sb.AppendLine($"+{m.value} {m.statType}");
        }

        return $"{header}\n\n{sb}";
    }
}

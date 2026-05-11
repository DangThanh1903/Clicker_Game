using UnityEngine;

public static class WeaponSelectionService
{
    public static bool TryGetStrongestWeaponItem(InventoryData inventory, out InventoryItem strongestWeaponItem)
    {
        strongestWeaponItem = null;
        if (inventory == null || inventory.Items == null)
            return false;

        float bestPower = float.MinValue;

        for (int i = 0; i < inventory.Items.Count; i++)
        {
            InventoryItem inventoryItem = inventory.Items[i];
            if (inventoryItem == null || inventoryItem.quantity == null || inventoryItem.quantity.Value <= 0)
                continue;

            if (inventoryItem.itemData is not Pickaxe weapon)
                continue;
            if (weapon.Type != ItemType.Weapon)
                continue;

            float powerScore = EvaluateNormalPowerScore(inventoryItem);
            if (powerScore < bestPower)
                continue;

            strongestWeaponItem = inventoryItem;
            bestPower = powerScore;
        }

        return strongestWeaponItem != null;
    }

    public static bool TryGetStrongestWeapon(InventoryData inventory, out Pickaxe strongestWeapon)
    {
        strongestWeapon = null;
        if (!TryGetStrongestWeaponItem(inventory, out InventoryItem strongestItem))
            return false;

        strongestWeapon = strongestItem.itemData as Pickaxe;
        return strongestWeapon != null;
    }

    public static float EvaluateNormalPowerScore(InventoryItem inventoryItem)
    {
        if (inventoryItem == null || inventoryItem.itemData is not IStatProvider provider)
            return 0f;

        float additivePower = 0f;
        float multiplierPower = 1f;

        foreach (var modifier in provider.GetStatModifiers())
        {
            if (modifier.statType != StatType.NormalPower)
                continue;

            if (modifier.mode == StatModifierMode.Multiply)
            {
                multiplierPower *= modifier.value;
                continue;
            }

            additivePower += modifier.value;
        }

        foreach (var prefixModifier in ItemPrefixConfig.GetFlatMods(inventoryItem.prefix))
        {
            if (prefixModifier.statType != StatType.NormalPower)
                continue;

            if (prefixModifier.mode == StatModifierMode.Multiply)
            {
                multiplierPower *= prefixModifier.value;
                continue;
            }

            additivePower += prefixModifier.value;
        }

        return Mathf.Max(0f, additivePower * Mathf.Max(0f, multiplierPower));
    }
}

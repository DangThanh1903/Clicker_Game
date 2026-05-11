using UnityEngine;

public static class InventoryMergeService
{
    private static WeaponMergeDatabaseSO mergeDatabase;
    private static bool loggedLegacyFallback;

    public static void ConfigureDatabase(WeaponMergeDatabaseSO database)
    {
        mergeDatabase = database;
        loggedLegacyFallback = false;
    }

    public static bool TryMergeWeapon(
        InventoryData fromData,
        int fromIndex,
        InventoryData toData,
        int toIndex)
    {
        if (fromData == null || toData == null)
            return false;
        if (fromData != toData)
            return false;
        if (fromData.inventoryType != InventoryType.Inventory)
            return false;
        if (fromIndex == toIndex)
            return false;
        if (!IsValidSlot(fromData, fromIndex) || !IsValidSlot(toData, toIndex))
            return false;

        InventoryItem fromItem = fromData.Items[fromIndex];
        InventoryItem toItem = toData.Items[toIndex];
        if (!CanMerge(
                fromItem,
                toItem,
                out Pickaxe sourceWeapon,
                out Pickaxe resultWeapon,
                out Item requiredRareItem,
                out int requiredRareAmount))
            return false;

        if (requiredRareAmount > 0 && !HasItemQuantity(fromData, requiredRareItem, requiredRareAmount))
        {
            string rareName = requiredRareItem != null ? requiredRareItem.GetColoredName() : "Rare Item";
            Toaster.Show($"Need {requiredRareAmount} {rareName}");
            return false;
        }

        // Consume 1 from each slot, then grant one upgraded weapon.
        if (!fromData.SubtractQuantity(fromIndex, 1, isPlayerAction: true))
            return false;
        if (!toData.SubtractQuantity(toIndex, 1, isPlayerAction: true))
        {
            fromData.AddItem(new InventoryItem(sourceWeapon, 1));
            return false;
        }

        bool consumedRare = false;
        if (requiredRareAmount > 0)
        {
            consumedRare = TryConsumeItemQuantity(fromData, requiredRareItem, requiredRareAmount);
            if (!consumedRare)
            {
                fromData.AddItem(new InventoryItem(sourceWeapon, 1));
                toData.AddItem(new InventoryItem(sourceWeapon, 1));
                return false;
            }
        }

        InventoryItem merged = new InventoryItem(resultWeapon, 1);
        bool addOk = toData.AddItem(merged);
        bool granted = addOk && merged.quantity != null && merged.quantity.Value <= 0;
        if (!granted)
        {
            // Rollback to avoid item loss in unexpected full-slot edge cases.
            toData.AddItem(new InventoryItem(sourceWeapon, 1));
            fromData.AddItem(new InventoryItem(sourceWeapon, 1));
            if (consumedRare && requiredRareItem != null && requiredRareAmount > 0)
                fromData.AddItem(new InventoryItem(requiredRareItem, requiredRareAmount));
            return false;
        }

        int previousMergeProgress = DataSaver.Ins != null ? DataSaver.Ins.MergeProgress : 0;
        if (DataSaver.Ins != null)
        {
            DataSaver.Ins.IncreaseMergeProgress();
            int currentMergeProgress = DataSaver.Ins.MergeProgress;
            BlockSpawnLocation biome = ResolveRewardBiome();
            BiomeMilestoneRewardService.TryGrantRewardsForProgressIncrease(
                biome,
                previousMergeProgress,
                currentMergeProgress);
        }

        Toaster.Show($"Merged into {resultWeapon.GetColoredName()}");
        return true;
    }

    private static bool CanMerge(
        InventoryItem fromItem,
        InventoryItem toItem,
        out Pickaxe sourceWeapon,
        out Pickaxe resultWeapon,
        out Item requiredRareItem,
        out int requiredRareAmount)
    {
        sourceWeapon = null;
        resultWeapon = null;
        requiredRareItem = null;
        requiredRareAmount = 0;
        if (fromItem == null || toItem == null)
            return false;
        if (fromItem.quantity == null || toItem.quantity == null)
            return false;
        if (fromItem.quantity.Value <= 0 || toItem.quantity.Value <= 0)
            return false;
        if (!ReferenceEquals(fromItem.itemData, toItem.itemData))
            return false;
        if (fromItem.itemData is not Pickaxe weapon)
            return false;

        sourceWeapon = weapon;
        return TryResolveMergeRecipe(
            weapon,
            out resultWeapon,
            out requiredRareItem,
            out requiredRareAmount);
    }

    private static bool TryResolveMergeRecipe(
        Pickaxe fromWeapon,
        out Pickaxe resultWeapon,
        out Item requiredRareItem,
        out int requiredRareAmount)
    {
        resultWeapon = null;
        requiredRareItem = null;
        requiredRareAmount = 0;
        if (fromWeapon == null || fromWeapon.Type != ItemType.Weapon)
            return false;

        if (mergeDatabase != null)
        {
            if (!mergeDatabase.TryGetRecipe(fromWeapon, out WeaponMergeRecipeEntry recipe))
                return false;

            resultWeapon = recipe.toWeapon;
            requiredRareItem = recipe.rareItem;
            requiredRareAmount = Mathf.Max(0, recipe.rareAmount);
            if (resultWeapon == null)
                return false;
            if (requiredRareAmount > 0 && requiredRareItem == null)
            {
                DevLog.Log($"[InventoryMergeService] Recipe '{fromWeapon.name}' requires rare amount but rare item is null.");
                return false;
            }
            return true;
        }

        if (!loggedLegacyFallback)
        {
            DevLog.Log("[InventoryMergeService] WeaponMergeDatabase is missing. Using legacy per-item merge fields.");
            loggedLegacyFallback = true;
        }

        if (!fromWeapon.IsMergeable || fromWeapon.MergeNextWeapon == null)
            return false;

        resultWeapon = fromWeapon.MergeNextWeapon;
        return true;
    }

    private static bool HasItemQuantity(InventoryData inventoryData, Item item, int requiredAmount)
    {
        if (inventoryData == null || item == null || requiredAmount <= 0)
            return false;

        int total = 0;
        for (int i = 0; i < inventoryData.Items.Count; i++)
        {
            InventoryItem slot = inventoryData.Items[i];
            if (slot == null || slot.itemData != item || slot.quantity == null)
                continue;

            total += Mathf.Max(0, slot.quantity.Value);
            if (total >= requiredAmount)
                return true;
        }

        return false;
    }

    private static bool TryConsumeItemQuantity(InventoryData inventoryData, Item item, int amount)
    {
        if (inventoryData == null || item == null || amount <= 0)
            return false;

        int remain = amount;
        for (int i = 0; i < inventoryData.Items.Count && remain > 0; i++)
        {
            InventoryItem slot = inventoryData.Items[i];
            if (slot == null || slot.itemData != item || slot.quantity == null)
                continue;

            int available = Mathf.Max(0, slot.quantity.Value);
            if (available <= 0)
                continue;

            int consume = Mathf.Min(available, remain);
            if (!inventoryData.SubtractQuantity(i, consume, isPlayerAction: true))
                return false;

            remain -= consume;
        }

        return remain <= 0;
    }

    private static bool IsValidSlot(InventoryData inventoryData, int index)
    {
        if (inventoryData == null || inventoryData.Items == null)
            return false;

        return index >= 0 && index < inventoryData.Items.Count;
    }

    private static BlockSpawnLocation ResolveRewardBiome()
    {
        if (BlockManager.Ins != null)
            return BlockManager.Ins.CurrentLocation;
        if (DataSaver.Ins != null && DataSaver.Ins.currentLocation.HasValue)
            return DataSaver.Ins.currentLocation.Value;

        return BlockSpawnLocation.Plain;
    }
}

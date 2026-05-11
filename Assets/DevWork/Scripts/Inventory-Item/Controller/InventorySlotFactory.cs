using UniRx;
using UnityEngine;
using Lean.Pool;

public static class InventorySlotFactory
{
    private static DontAccpetAnyRuleSO craftingOutAcceptRule;

    public static void CreateSlots(InventorySection section)
    {
        if (section == null)
        {
            DevLog.Log("[InventorySlotFactory] Skip null section.");
            return;
        }
        if (section.inventoryData == null)
        {
            DevLog.Log($"[InventorySlotFactory] Section '{section.name}' missing inventoryData.");
            return;
        }
        if (section.slotParent == null)
        {
            DevLog.Log($"[InventorySlotFactory] Section '{section.name}' missing slotParent.");
            return;
        }
        if (section.slotPrefab == null)
        {
            DevLog.Log($"[InventorySlotFactory] Section '{section.name}' missing slotPrefab.");
            return;
        }

        section.slotUIs ??= new System.Collections.Generic.List<InventorySlotUI>();
        section.disposables ??= new CompositeDisposable();

        // Clean previous
        foreach (Transform child in section.slotParent)
            LeanPool.Despawn(child.gameObject);
        section.slotUIs.Clear();
        section.disposables.Dispose();
        section.disposables = new CompositeDisposable();

        var inventory = section.inventoryData;

        for (int i = 0; i < inventory.Items.Count; i++)
        {
            var slotGO = LeanPool.Spawn(section.slotPrefab, section.slotParent);
            var slotUI = slotGO.GetComponent<InventorySlotUI>();
            if (slotUI == null)
            {
                DevLog.Log($"[InventorySlotFactory] Prefab '{section.slotPrefab.name}' has no InventorySlotUI.");
                LeanPool.Despawn(slotGO);
                continue;
            }

            if (inventory.inventoryType == InventoryType.CraftingOut)
            {
                craftingOutAcceptRule ??= ScriptableObject.CreateInstance<DontAccpetAnyRuleSO>();
                slotUI.SetAcceptRule(craftingOutAcceptRule);
            }

            slotUI.Bind(inventory, i);
            slotUI.UpdateSlotUI(inventory.Items[i]);
            slotUI.SetEquippedWeaponVisual(false);

            section.slotUIs.Add(slotUI);
        }

        // Observe Replace
        inventory.Items
            .ObserveReplace()
            .Subscribe(x =>
            {
                var index = x.Index;
                if (index >= 0 && index < section.slotUIs.Count)
                {
                    section.slotUIs[index].UpdateSlotUI(x.NewValue);
                    if (ShouldRefreshWeaponHighlight(section.inventoryData.inventoryType))
                        RefreshWeaponHighlight(section);
                }
            })
            .AddTo(section.disposables);

        // Observe Reset
        inventory.Items
            .ObserveReset()
            .Subscribe(_ =>
            {
                for (int i = 0; i < inventory.Items.Count; i++)
                {
                    if (i < section.slotUIs.Count)
                        section.slotUIs[i].UpdateSlotUI(inventory.Items[i]);
                }

                if (ShouldRefreshWeaponHighlight(section.inventoryData.inventoryType))
                    RefreshWeaponHighlight(section);
            })
            .AddTo(section.disposables);

        if (ShouldRefreshWeaponHighlight(section.inventoryData.inventoryType))
            RefreshWeaponHighlight(section);
    }

    private static bool ShouldRefreshWeaponHighlight(InventoryType inventoryType)
    {
        return inventoryType == InventoryType.Inventory;
    }

    private static void RefreshWeaponHighlight(InventorySection section)
    {
        var inventory = section != null ? section.inventoryData : null;
        if (inventory == null || section.slotUIs == null)
            return;

        WeaponSelectionService.TryGetStrongestWeaponItem(inventory, out InventoryItem strongestWeaponItem);

        for (int i = 0; i < section.slotUIs.Count; i++)
        {
            InventorySlotUI slotUI = section.slotUIs[i];
            if (slotUI == null)
                continue;

            InventoryItem slotItem = i < inventory.Items.Count ? inventory.Items[i] : null;
            bool isEquippedWeaponSlot = strongestWeaponItem != null && ReferenceEquals(slotItem, strongestWeaponItem);
            slotUI.SetEquippedWeaponVisual(isEquippedWeaponSlot);
        }
    }

}

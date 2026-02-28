using System;
using UniRx;
using UnityEngine;
using Lean.Pool;

public static class InventorySlotFactory
{
    private static DontAccpetAnyRuleSO craftingOutAcceptRule;

    public static void CreateSlots(InventorySection section)
    {
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

            if (inventory.inventoryType == InventoryType.CraftingOut)
            {
                craftingOutAcceptRule ??= ScriptableObject.CreateInstance<DontAccpetAnyRuleSO>();
                slotUI.SetAcceptRule(craftingOutAcceptRule);
            }

            slotUI.Bind(inventory, i);
            slotUI.UpdateSlotUI(inventory.Items[i]);

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
                    if (section.inventoryData.inventoryType == InventoryType.Pickaxe)
                    {
                        HandlePickaxeState(x.NewValue.itemData);
                    }
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
            })
            .AddTo(section.disposables);
    }

    public static void HandlePickaxeState(Item item)
    {
        var player = PlayerController.Instance;
        if (player == null) return;

        if (item is not Pickaxe pickaxe || item.Type == ItemType.None)
            player.SetEquippedPickaxe(null);
        else
            player.SetEquippedPickaxe(pickaxe);
    }
}

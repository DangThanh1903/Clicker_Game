using System;
using UniRx;
using UnityEngine;

public static class InventorySlotFactory
{
    public static void CreateSlots(InventorySection section)
    {
        // Clean previous
        foreach (Transform child in section.slotParent)
            UnityEngine.Object.Destroy(child.gameObject);
        section.slotUIs.Clear();
        section.disposables.Dispose();
        section.disposables = new CompositeDisposable();

        var inventory = section.inventoryData;

        for (int i = 0; i < inventory.Items.Count; i++)
        {
            var slotGO = UnityEngine.Object.Instantiate(section.slotPrefab, section.slotParent);
            var slotUI = slotGO.GetComponent<InventorySlotUI>();
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
                    if (section.name == "pickaxe")
                    {
                        HandlePickaxeState(x.NewValue.itemData as Pickaxe ?? ScriptableObject.CreateInstance<Pickaxe>());
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
        if (item is not Pickaxe pickaxe || item == null || item.Type == ItemType.None)
        {
            PlayerController.Instance.SetState(new NormalState());
        }
        else
        {
            PlayerController.Instance.SetStateByType(pickaxe.currentState);
        }
    }
}

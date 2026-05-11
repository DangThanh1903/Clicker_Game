using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System.Linq;

public class InventoryUIManager : MonoBehaviour
{
    public InventorySection[] inventorySections;
    private readonly CompositeDisposable equipmentWatchDisposables = new CompositeDisposable();
    public event Action OnEquippedItemsChanged;

    private void Start()
    {
        CreateAllInventorySlots();
    }

    private void OnDestroy()
    {
        equipmentWatchDisposables.Dispose();
    }

    public bool AddItemToInventorySection(InventoryItem inventoryItem)
    {
        if (inventorySections == null || inventorySections.Length == 0)
        {
            Debug.LogWarning("Inventory sections are missing or empty.");
            return false;
        }

        foreach (var section in inventorySections)
        {
            if (section == null || section.inventoryData == null)
                continue;

            if (section.inventoryData.inventoryType == InventoryType.Inventory)
            {
                return section.inventoryData.AddItem(inventoryItem);
            }
        }

        Debug.LogWarning("No Inventory section found with type 'Inventory'.");
        return false;
    }

    public bool HasInventorySection()
    {
        if (inventorySections == null || inventorySections.Length == 0)
            return false;

        foreach (var section in inventorySections)
        {
            if (section == null || section.inventoryData == null)
                continue;

            if (section.inventoryData.inventoryType == InventoryType.Inventory)
                return true;
        }

        return false;
    }
    public void SortInventory()
    {
        var section = inventorySections != null
            ? inventorySections.FirstOrDefault(s =>
                s != null &&
                s.inventoryData != null &&
                s.inventoryData.inventoryType == InventoryType.Inventory)
            : null;
        if (section != null)
            section.inventoryData.SortAndRepack_UsingAddItem();
    }


    public void CreateAllInventorySlots()
    {
        if (inventorySections == null || inventorySections.Length == 0)
        {
            DevLog.Log("[InventoryUIManager] inventorySections is empty.");
            return;
        }

        foreach (var section in inventorySections)
        {
            InventorySlotFactory.CreateSlots(section);
        }

        RebindEquipmentWatchers();
        NotifyEquippedItemsChanged();
    }

    public InventoryData GetInventoryForSlot(InventorySlotUI slotUI)
    {
        foreach (var section in inventorySections)
        {
            if (section == null || section.inventoryData == null || section.slotUIs == null)
                continue;

            if (section.slotUIs.Contains(slotUI))
                return section.inventoryData;
        }
        return null;
    }

    public InventoryData GetInventoryData(InventoryType type)
    {
        foreach (var section in inventorySections)
        {
            if (section == null || section.inventoryData == null)
                continue;
            if (section.inventoryData.inventoryType == type)
                return section.inventoryData;
        }
        return null;
    }
    public InventoryItem GetStrongestWeaponItem()
    {
        var mainInventory = GetInventoryData(InventoryType.Inventory);
        if (mainInventory == null)
            return null;

        WeaponSelectionService.TryGetStrongestWeaponItem(mainInventory, out InventoryItem strongest);
        return strongest;
    }

    public PetItem GetEquippedPet()
    {
        var petInventory = GetInventoryData(InventoryType.Pet);
        if (petInventory == null || petInventory.Items == null || petInventory.Items.Count == 0)
            return null;

        return petInventory.Items[0]?.itemData as PetItem;
    }

    // Helper 

    public IEnumerable<InventoryItem> GetEquippedItems()
    {
        var mainInventory = GetInventoryData(InventoryType.Inventory);
        if (WeaponSelectionService.TryGetStrongestWeaponItem(mainInventory, out InventoryItem strongestWeaponItem))
            yield return strongestWeaponItem;

        foreach (var section in inventorySections)
        {
            if (section == null || section.inventoryData == null)
                continue;

            if (section.inventoryData.inventoryType != InventoryType.Accessory &&
                section.inventoryData.inventoryType != InventoryType.Pet)
                continue;

            foreach (var item in section.inventoryData.Items)
            {
                if (item?.itemData == null) continue;
                yield return item;
            }
        }
    }

    public IEnumerable<(InventoryType type, InventoryItem item)> GetEquippedItemSlots()
    {
        var mainInventory = GetInventoryData(InventoryType.Inventory);
        if (WeaponSelectionService.TryGetStrongestWeaponItem(mainInventory, out InventoryItem strongestWeaponItem))
            yield return (InventoryType.Inventory, strongestWeaponItem);

        foreach (var section in inventorySections)
        {
            if (section == null || section.inventoryData == null)
                continue;

            if (section.inventoryData.inventoryType != InventoryType.Accessory &&
                section.inventoryData.inventoryType != InventoryType.Pet)
                continue;

            foreach (var item in section.inventoryData.Items)
            {
                if (item?.itemData == null) continue;
                yield return (section.inventoryData.inventoryType, item);
            }
        }
    }

    public void NotifyEquippedItemsChanged()
    {
        OnEquippedItemsChanged?.Invoke();
    }

    private void RebindEquipmentWatchers()
    {
        equipmentWatchDisposables.Clear();
        if (inventorySections == null)
            return;

        foreach (var section in inventorySections)
        {
            if (section == null || section.inventoryData == null)
                continue;

            InventoryType inventoryType = section.inventoryData.inventoryType;
            if (!IsEquipmentInventoryType(inventoryType))
                continue;

            section.inventoryData.Items
                .ObserveReplace()
                .Subscribe(_ => NotifyEquippedItemsChanged())
                .AddTo(equipmentWatchDisposables);

            section.inventoryData.Items
                .ObserveReset()
                .Subscribe(_ => NotifyEquippedItemsChanged())
                .AddTo(equipmentWatchDisposables);
        }
    }

    private static bool IsEquipmentInventoryType(InventoryType inventoryType)
    {
        return inventoryType == InventoryType.Inventory ||
               inventoryType == InventoryType.Accessory ||
               inventoryType == InventoryType.Pet;
    }
}

[Serializable]
public class InventorySection
{
    public string name;
    public InventoryData inventoryData;
    public Transform slotParent;
    public GameObject slotPrefab;

    [NonSerialized] public List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();
    [NonSerialized] public CompositeDisposable disposables = new CompositeDisposable();
}

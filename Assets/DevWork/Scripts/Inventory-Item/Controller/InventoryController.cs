using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UniRx;

public class InventoryController : MonoBehaviour
{
    public static InventoryController Instance;
    [SerializeField] private InventoryUIManager uiManager;
    [SerializeField] private CraftingController craftingController;
    [SerializeField] private TMP_Text description;
    [SerializeField] private Button useButton;
    [SerializeField] private BuffManager buffManager;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);


        InventoryItem.LoadNoneItem(() =>
        {
            Debug.Log("'None' item ready for inventory.");
        });

    }

    void Start()
    {
        buffManager.Initialize(StatsManager.Ins);
        useButton.gameObject.SetActive(false);
    }

    // ==============================
    // SECTION: Inventory Logic
    // ==============================

    public void AddItemToInventory(InventoryItem inventoryItem)
    {
        uiManager.AddItemToInventorySection(inventoryItem);
    }

    public bool TrySwap(
    InventoryData fromData, int fromIndex,
    InventoryData toData, int toIndex,
    SlotAcceptRuleSO fromRule, SlotAcceptRuleSO toRule)
    {
        Debug.Log("Attempting swap...");

        if (fromData == null || toData == null) return false;
        if (fromIndex < 0 || fromIndex >= fromData.Items.Count) return false;
        if (toIndex < 0 || toIndex >= toData.Items.Count) return false;

        var itemA = fromData.Items[fromIndex];
        var itemB = toData.Items[toIndex];

        // Self-drop: same slot, same inventory
        if (fromData == toData && fromIndex == toIndex)
            return false;

        // Reject if either can't accept the other's item (cross-inventory)
        if (fromData != toData)
        {
            if (!fromRule.CanAccept(itemB) && itemB.itemData.Type != ItemType.None)
            {
                Debug.LogError("Drop rejected: fromRule");
                return false;
            }
            if (!toRule.CanAccept(itemA) && itemA.itemData.Type != ItemType.None)
            {
                Debug.LogError("Drop rejected: toRule");
                return false;
            }
        }

        // Same item type and stackable → Proper stacking logic
        if (itemA.itemData == itemB.itemData && itemA.itemData.MaxStack > 1)
        {
            Debug.Log("Stacking items");

            int totalQuantity = itemA.quantity.Value + itemB.quantity.Value;
            int maxStack = itemB.itemData.MaxStack;

            if (totalQuantity <= maxStack)
            {
                // All can be stacked into the target slot
                toData.SetItem(toIndex, new InventoryItem(itemB.itemData, totalQuantity), true);
                fromData.RemoveItemAt(fromIndex, true);
            }
            else
            {
                // Only stack up to max in target
                toData.SetItem(toIndex, new InventoryItem(itemB.itemData, maxStack), true);

                // Calculate remaining amount
                int remainder = totalQuantity - maxStack;

                // Put the remainder back in the from slot
                fromData.SetItem(fromIndex, new InventoryItem(itemA.itemData, remainder), true);
            }

            return true;
        }


        // Perform regular swap
        fromData.SetItem(fromIndex, itemB, true);
        toData.SetItem(toIndex, itemA, true);

        Debug.Log("Swapped items");

        if (fromData.inventoryType != InventoryType.Inventory || toData.inventoryType != InventoryType.Inventory)
        {
            UpdateStat();
        }
        return true;
    }
    public void SetUseButton(Item item, int index, InventoryData inventoryData)
    {
        useButton.gameObject.SetActive(item.Type == ItemType.Consumable || item.Type == ItemType.BossSummoner);
        useButton.onClick.RemoveAllListeners();
        if (item is ConsumableItem consumableItem)
        {
            useButton.onClick.AddListener(() =>
            {
                UseConsumable(consumableItem);
                if (!inventoryData.SubtractQuantity(index, 1, true))
                {
                    useButton.gameObject.SetActive(false);
                }
            });
        }
        else if (item is BossSummoner bossSummoner)
        {
            useButton.onClick.AddListener(() =>
            {
                UseSummonBoss(bossSummoner);
                inventoryData.RemoveItemAt(index);
                useButton.gameObject.SetActive(false);
            });
        }
    }
    public void SetDescription(string des)
    {
        description.text = des;
        useButton.gameObject.SetActive(false);
    }
    // ==============================
    // SECTION: Stat Logic
    // ==============================

    void UpdateStat()
    {
        // Reset stats to base
        StatsManager.Ins.ClearAll();

        // Remove all item buffs (so we can reapply)
        foreach (var section in uiManager.inventorySections)
            foreach (var item in section.inventoryData.Items)
                if (item?.itemData != null)
                    buffManager.RemoveBuffsFromItem(item.itemData);

        // Apply buffs from equipped items
        foreach (var section in uiManager.inventorySections)
        {
            if (section.inventoryData.inventoryType != InventoryType.Pickaxe &&
                section.inventoryData.inventoryType != InventoryType.Accessory)
                continue;

            foreach (var item in section.inventoryData.Items)
            {
                if (item?.itemData == null) continue;

                // Apply stats from IStatProvider
                if (item.itemData is IStatProvider provider)
                    foreach (var mod in provider.GetStatModifiers())
                        StatsManager.Ins.Add(mod.statType, mod.value);

                // Apply buffs from accessory / passive items
                if (item.itemData is Accessory accessory)
                    buffManager.ApplyItemBuffs(item.itemData, accessory.GetPassiveBuffs());
            }
        }

        // Apply all active buffs (item conditional + consumable)
        foreach (var buff in buffManager.GetActiveBuffs())
        {
            if (buff.SourceItem == null) // only consumables
            {
                StatsManager.Ins.Add(
                    buff.buffData.statType,
                    buff.buffData.amount * (buff.buffData.isStackable ? buff.StackCount : 1)
                );
            }
        }


        // Update UI description
        UpdateStatDescription();
    }



    void UpdateStatDescription()
    {
        SetDescription(GetStatDescriptionWithBuffs(buffManager.GetActiveBuffs().ToList()));
    }

    string GetStatDescriptionWithBuffs(List<BuffInstance> buffs)
    {
        var desc = GetStatDescription(); // base item stats
        foreach (var buff in buffs)
        {
            if (buff.IsActive)
                desc += $"\n{buff.buffData.buffName}: +{buff.buffData.amount}";
        }
        return desc;
    }

    string GetStatDescription()
    {
        var HP = StatsManager.Ins.Get(StatType.HP);
        var Mana = StatsManager.Ins.Get(StatType.Mana);
        var damage = GetDamageByState();
        var def = StatsManager.Ins.Get(StatType.Def);
        var crit = StatsManager.Ins.Get(StatType.CritChance);

        return $"Max HP: {HP:F0}\nMana: {Mana}\nDamage: {damage}\nCrit Chance: {crit:F0}%\nDefense: {def:F0}\n";
    }

    float GetDamageByState()
    {
        return PlayerController.Instance.currentState switch
        {
            NormalState => StatsManager.Ins.Get(StatType.NormalPower),
            HoldState => StatsManager.Ins.Get(StatType.HoldPower),
            IdleState => StatsManager.Ins.Get(StatType.IdlePower),
            _ => StatsManager.Ins.Get(StatType.NormalPower),
        };
    }

    void UseConsumable(ConsumableItem consumable)
    {
        if (consumable.buffToApply != null)
        {
            buffManager.ApplyBuff(consumable.buffToApply);
            Debug.Log($"Applied buff: {consumable.buffToApply.buffName}");
        }
    }

    void UseSummonBoss(BossSummoner bossSummoner)
    {
        BlockManager.Ins.Summon(bossSummoner.bossBase);
        UIManager.Ins.MoveToMain();
    }
}

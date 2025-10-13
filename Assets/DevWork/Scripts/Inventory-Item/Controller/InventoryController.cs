using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System.Threading.Tasks;
using System.Text;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class InventoryController : MonoBehaviour
{
    public static InventoryController Instance;
    [SerializeField] private InventoryUIManager uiManager;
    [SerializeField] private CraftingController craftingController;
    [SerializeField] private TMP_Text description;
    [SerializeField] private Button useButton;
    [SerializeField] private BuffManager buffManager;
    [SerializeField] private Button sortInventoryButton;
    [SerializeField] private PopupView lootboxPrefab;
    [SerializeField] private CaseRollController caseRollController;

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
        UpdateStat();

        sortInventoryButton.onClick.AddListener(() =>
        {
            uiManager.SortInventory();
        });
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
        useButton.gameObject.SetActive(item.Type == ItemType.Consumable
                                    || item.Type == ItemType.BossSummoner
                                    || item.Type == ItemType.Lootbox);
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
        else if (item is Lootbox lootbox)
        {
            useButton.onClick.AddListener(async () =>
            {
                await UseLootBox(lootbox);
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

                if (item.itemData is Pickaxe pickaxe)
                    buffManager.ApplyItemBuffs(item.itemData, pickaxe.GetPassiveBuffs());
            }
        }

        foreach (var buff in buffManager.GetActiveBuffs())
        {
            if (buff.SourceItem == null)
            {
                buff.ApplyEffect();
            }
        }

        // Update UI description
        UpdateStatDescription();
    }



    async void UpdateStatDescription()
    {
        string text = await GetStatDescriptionWithBuffs(buffManager.GetActiveBuffs().ToList(), buffManager.GetConditionBuffs().ToList());
        SetDescription(text);
    }

    public async Task<string> GetStatDescriptionWithBuffs(List<BuffInstance> buffs, List<BuffInstance> conditionBuffs)
    {
        // wait for base description
        string desc = await GetStatDescriptionAsync();

        var sb = new StringBuilder(desc);

        foreach (var buff in buffs)
        {
            if (buff.IsActive)
            {
                // if buffName is LocalizedString, resolve it here:
                string buffName = buff.buffData.buffName;
                sb.AppendLine($"{buffName}: +{buff.buffData.amount} {buff.buffData.statType}");
            }
        }

        foreach (var buff in conditionBuffs)
        {
            if (buff.buffData is ConditionalBuffSO conditionalBuffSO)
            {
                sb.AppendLine(
                    $"{conditionalBuffSO.buffName}: +{conditionalBuffSO.amount} {conditionalBuffSO.statType} [{conditionalBuffSO.conditionType}]"
                    );
            }
        }

        return sb.ToString();
    }

    public async Task<string> GetStatDescriptionAsync()
    {
        var HP    = StatsManager.Ins.Get(StatType.HP);
        var Mana  = StatsManager.Ins.Get(StatType.Mana);
        var dmg   = GetDamageByState();
        var def   = StatsManager.Ins.Get(StatType.Def);
        var crit  = StatsManager.Ins.Get(StatType.CritChance);

        var sb = new StringBuilder();

        // Resolve từng LocalizedString 1 lần
        async Task<string> L(StatType type)
        {
            var handle = type.ToLocalized().GetLocalizedStringAsync();
            await handle.Task;
            string result = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : type.ToString();
            Addressables.Release(handle);
            return result;
        }

        string hpLabel   = await L(StatType.HP);
        string manaLabel = await L(StatType.Mana);
        string dmgLabel  = await L(StatType.NormalPower); // hoặc key riêng "stat_damage"
        string defLabel  = await L(StatType.Def);
        string critLabel = await L(StatType.CritChance);

        sb.AppendLine($"{hpLabel}: {HP:F0}");
        sb.AppendLine($"{manaLabel}: {Mana}");
        sb.AppendLine($"{dmgLabel}: {dmg}");
        sb.AppendLine($"{critLabel}: {crit:F0}%");
        sb.AppendLine($"{defLabel}: {def:F0}");

        return sb.ToString();
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
        BlockManager.Ins.Summon(bossSummoner.bossLocation, bossSummoner.bossType);
        UIManager.Ins.MoveToMain();
    }

    async Task UseLootBox(Lootbox lootbox)
    {
        var popup = await PopupController.Instance.Show(lootboxPrefab);
        var caseRollerUI = popup.GetComponentInChildren<CaseRollerUI>(true);

        caseRollController.SetUI(caseRollerUI);
        caseRollController.UseLootbox(lootbox);
    }
}

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
    [SerializeField] private InventoryUIManager inventoryUiManager;
    [SerializeField] private InventorySlider inventorySlider;
    [SerializeField] private UIManager mainPageManager;
    [SerializeField] private CraftingController craftingController;
    [SerializeField] private TMP_Text description;
    [SerializeField] private List<TMP_Text> statTexts;
    [SerializeField] private Button useButton;
    [SerializeField] private Button sortInventoryButton;
    [SerializeField] private PopupView lootboxPrefab;
    [SerializeField] private CaseRollController caseRollController;

    public InventoryUIManager InventoryUIManager => inventoryUiManager;
    public InventorySlider InventorySlider => inventorySlider;
    public CraftingController CraftingController => craftingController;

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
        useButton.gameObject.SetActive(false);
        UpdateStat();

        sortInventoryButton.onClick.AddListener(() =>
        {
            inventoryUiManager.SortInventory();
        });

        StatsManager.Ins.OnStatsRecalculated += UpdateStatDescription;

        if (inventorySlider != null)
            inventorySlider.OnPageChanged += HandleInventoryPageChanged;

        if (mainPageManager == null)
            mainPageManager = UIManager.Ins;
        if (mainPageManager != null)
            mainPageManager.OnPageChanged += HandleMainPageChanged;
    }

    void OnDestroy()
    {
        if (StatsManager.Ins != null)
            StatsManager.Ins.OnStatsRecalculated -= UpdateStatDescription;

        if (inventorySlider != null)
            inventorySlider.OnPageChanged -= HandleInventoryPageChanged;

        if (mainPageManager != null)
            mainPageManager.OnPageChanged -= HandleMainPageChanged;
    }

    // ==============================
    // SECTION: Inventory Logic
    // ==============================

    public void AddItemToInventory(InventoryItem inventoryItem)
    {
        _ = TryAddItemToInventory(inventoryItem);
    }

    public bool TryAddItemToInventory(InventoryItem inventoryItem)
    {
        if (inventoryUiManager == null)
        {
            Debug.LogWarning("InventoryUIManager is null, cannot add item.");
            return false;
        }

        bool ok = inventoryUiManager.AddItemToInventorySection(inventoryItem);
        if (!ok)
        {
            bool hasSection = inventoryUiManager.HasInventorySection();
            Debug.LogWarning(hasSection
                ? "AddItemToInventory failed (inventory full)."
                : "AddItemToInventory failed (no Inventory section).");
        }
        return ok;
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

    public void GoToStatsPage()
    {
        if (inventorySlider == null)
        {
            Debug.LogWarning("InventorySlider is null, cannot go to stats page.");
            return;
        }

        inventorySlider.GoToStatsPage();
    }

    private void HandleInventoryPageChanged(int fromPage, int toPage)
    {
        if (craftingController == null)
            return;

        const int craftingPageIndex = 1;
        if (toPage != craftingPageIndex)
            craftingController.ReturnItemsToInventory();
    }

    private void HandleMainPageChanged(int fromPage, int toPage)
    {
        if (craftingController == null)
            return;

        const int inventoryPageIndex = 0;
        if (fromPage == inventoryPageIndex && toPage != inventoryPageIndex)
            craftingController.ReturnItemsToInventory();
    }
    // ==============================
    // SECTION: Stat Logic
    // ==============================

    void UpdateStat()
    {
        StatsManager.Ins.RecalculateAllStats();
        UpdateStatTextsImmediate();     // ✅ update list text ngay lập tức
        UpdateStatDescription();        // ✅ update description (buffs)
    }


    void UpdateStatDescription()
    {
        var activeBuffs    = StatsManager.Ins.ActiveBuffs?.ToList()    ?? new List<BuffInstance>();
        var conditionBuffs = StatsManager.Ins.ConditionBuffs?.ToList() ?? new List<BuffInstance>();

        // ✅ Update stat list (đề phòng thay đổi state/buff)
        UpdateStatTextsImmediate();

        // ✅ Chỉ show buff vào description
        string buffText = GetBuffOnlyDescription(activeBuffs, conditionBuffs);
        SetDescription(buffText);
    }

    void UpdateStatTextsImmediate()
    {
        if (statTexts == null || statTexts.Count == 0) return;

        // Order: HP, Mana, Damage, Crit, Def (giống bạn đang build)
        var hp   = StatsManager.Ins.Get(StatType.HP);
        var mana = StatsManager.Ins.Get(StatType.Mana);
        var dmg  = GetDamageByState();
        var crit = StatsManager.Ins.Get(StatType.CritChance);
        var def  = StatsManager.Ins.Get(StatType.Def);
        var luck = StatsManager.Ins.Get(StatType.Lucky);

        // đảm bảo list đủ phần tử
        void Set(int idx, string value)
        {
            if (idx >= 0 && idx < statTexts.Count && statTexts[idx] != null)
                statTexts[idx].text = value;
        }

        Set(0, $"{hp:F0}");
        Set(1, $"{mana:F0}");
        Set(2, $"{dmg:F0}");
        Set(3, $"{crit:F0}%");
        Set(4, $"{def:F0}");
        Set(5, $"{luck:F0}%");
    }

    string GetBuffOnlyDescription(List<BuffInstance> buffs, List<BuffInstance> conditionBuffs)
    {
        var sb = new StringBuilder();

        foreach (var buff in buffs)
        {
            if (!buff.IsActive) continue;

            string buffName = buff.buffData.buffName;
            sb.AppendLine($"{buffName} [x{buff.StackCount}]:");
            foreach (var m in buff.buffData.modifiers)
                sb.AppendLine($"   +{m.value * buff.StackCount} {m.statType}");
            sb.AppendLine();
        }

        foreach (var buff in conditionBuffs)
        {
            if (buff.buffData is not ConditionalBuffSO conditional) continue;

            sb.AppendLine($"{conditional.buffName} [{conditional.conditionType}]:");
            foreach (var m in buff.buffData.modifiers)
                sb.AppendLine($"   +{m.value} {m.statType}");
            sb.AppendLine();
        }

        if (sb.Length == 0)
            sb.AppendLine("No active buffs.");

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
            StatsManager.Ins.ApplyConsumableBuff(consumable.buffToApply);
            Debug.Log($"Applied buff: {consumable.buffToApply.buffName}");
        }
    }


    void UseSummonBoss(BossSummoner bossSummoner)
    {
        BlockManager.Ins.Summon(bossSummoner.bossLocation, bossSummoner.bossType);
        mainPageManager.MoveToMain();
    }

    async Task UseLootBox(Lootbox lootbox)
    {
        var popup = await PopupController.Instance.Show(lootboxPrefab);
        var caseRollerUI = popup.GetComponentInChildren<CaseRollerUI>(true);

        caseRollController.SetUI(caseRollerUI);
        caseRollController.UseLootbox(lootbox);
    }
}

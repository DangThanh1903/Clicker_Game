using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class GameplayUIManager : MonoBehaviour
{
    [SerializeField] private TMP_Text clickNumberUI;
    [SerializeField] private TMP_Text clickPerTickUI;
    [SerializeField] private TMP_Text diamondUI;
    [SerializeField] private TMP_Text blockNameUI;
    [SerializeField] private TMP_Text runTimerUI;
    [SerializeField] private Image HpUI;
    [SerializeField] private Image ManaUI;
    [SerializeField] private Sprite manaSprite;
    [SerializeField] private Sprite staminaSprite;
    private float displayedManaFill = 0f;
    private int lastShownRunSecond = int.MinValue;
    private ResourceDisplayMode currentResourceMode = ResourceDisplayMode.Mana;

    private enum ResourceDisplayMode
    {
        Stamina,
        Mana
    }

    private string lastShownBlockName = string.Empty;
    private readonly CompositeDisposable blockNameObserverDisposables = new CompositeDisposable();

    void OnEnable()
    {
        if (StatsManager.Ins != null)
            StatsManager.Ins.OnStatsRecalculated += RefreshBars;

        BindBlockNameObserver();
        RefreshBars();
        RefreshBlockName();
    }

    void OnDisable()
    {
        if (StatsManager.Ins != null)
            StatsManager.Ins.OnStatsRecalculated -= RefreshBars;

        blockNameObserverDisposables.Clear();
    }

    void Start()
    {
        AddReactToUI();
        ApplyResourceModeVisual(ResolveResourceMode());
        RefreshBars();
        RefreshBlockName();
    }

    void AddReactToUI()
    {
        StatsManager.Ins.GetReactive(StatType.Clicks)
            .DistinctUntilChanged()
            .Subscribe(val => clickNumberUI.SetText("{0} click", (int)val))
            .AddTo(this);
        StatsManager.Ins.GetReactive(StatType.ClickPerTick)
            .DistinctUntilChanged()
            .Throttle(TimeSpan.FromSeconds(0.1))
            .Subscribe(val => clickPerTickUI.SetText("{0} cpt", val))
            .AddTo(this);
        StatsManager.Ins.GetReactive(StatType.Diamond)
            .DistinctUntilChanged()
            .Throttle(TimeSpan.FromSeconds(0.1))
            .Subscribe(val => diamondUI.SetText("{0}", val))
            .AddTo(this);

        Observable.CombineLatest(
                StatsManager.Ins.GetReactive(StatType.CurrentHP),
                StatsManager.Ins.GetReactive(StatType.HP),
                (cur, max) => new { cur, max })
            .Subscribe(x =>
            {
                if (HpUI == null) return;
                float fill = (x.max > 0f) ? (x.cur / x.max) : 0f;
                HpUI.fillAmount = Mathf.Clamp01(fill);
            })
            .AddTo(this);
        Observable.EveryUpdate()
            .Subscribe(_ => UpdateResourceUI())
            .AddTo(this);

    }

    void UpdateResourceUI()
    {
        if (ManaUI == null)
        {
            UpdateRunTimerUI();
            return;
        }

        ResourceDisplayMode mode = ResolveResourceMode();
        if (mode != currentResourceMode)
            ApplyResourceModeVisual(mode);

        float targetFill = GetTargetResourceFill(mode);
        displayedManaFill = Mathf.Lerp(displayedManaFill, targetFill, Time.deltaTime * 10f);
        ManaUI.fillAmount = displayedManaFill;
        UpdateRunTimerUI();
    }

    private ResourceDisplayMode ResolveResourceMode()
    {
        // Click/manual and pet/auto both use stamina resource display in current design.
        return ResourceDisplayMode.Stamina;
    }

    private float GetTargetResourceFill(ResourceDisplayMode mode)
    {
        switch (mode)
        {
            case ResourceDisplayMode.Stamina:
                return PlayerController.Instance != null
                    ? PlayerController.Instance.GetStaminaPercent()
                    : 0f;
            case ResourceDisplayMode.Mana:
                if (StatsManager.Ins == null) return 0f;
                float maxMana = StatsManager.Ins.Get(StatType.Mana);
                float curMana = StatsManager.Ins.Get(StatType.CurrentMana);
                return (maxMana > 0f) ? Mathf.Clamp01(curMana / maxMana) : 0f;
            default:
                return 0f;
        }
    }

    private void ApplyResourceModeVisual(ResourceDisplayMode mode)
    {
        currentResourceMode = mode;

        if (ManaUI == null)
            return;

        ManaUI.gameObject.SetActive(true);

        Sprite useSprite = manaSprite;
        switch (mode)
        {
            case ResourceDisplayMode.Stamina:
                useSprite = staminaSprite;
                break;
            case ResourceDisplayMode.Mana:
            default:
                useSprite = manaSprite;
                break;
        }

        if (useSprite != null)
            ManaUI.sprite = useSprite;
    }

    private void RefreshBars()
    {
        if (StatsManager.Ins == null) return;

        ResourceDisplayMode mode = ResolveResourceMode();
        if (mode != currentResourceMode)
            ApplyResourceModeVisual(mode);

        if (HpUI != null)
        {
            float maxHP = StatsManager.Ins.Get(StatType.HP);
            float curHP = StatsManager.Ins.Get(StatType.CurrentHP);
            float fill = (maxHP > 0f) ? (curHP / maxHP) : 0f;
            HpUI.fillAmount = Mathf.Clamp01(fill);
        }

        displayedManaFill = Mathf.Clamp01(GetTargetResourceFill(mode));
        if (ManaUI != null)
            ManaUI.fillAmount = displayedManaFill;
    }

    private void RefreshBlockName()
    {
        SetBlockName(ResolveCurrentBlockName());
    }

    private void UpdateRunTimerUI()
    {
        if (runTimerUI == null)
            return;

        float remainingSeconds = -1f;
        if (DungeonRunManager.Ins != null && DungeonRunManager.Ins.IsRunning)
            remainingSeconds = DungeonRunManager.Ins.RemainingRunTime;
        else if (BlockManager.Ins != null && BlockManager.Ins.IsBossTimerRunning)
            remainingSeconds = BlockManager.Ins.BossRemainingTime;

        if (remainingSeconds < 0f)
        {
            if (runTimerUI.gameObject.activeSelf)
                runTimerUI.gameObject.SetActive(false);
            lastShownRunSecond = int.MinValue;
            return;
        }

        if (!runTimerUI.gameObject.activeSelf)
            runTimerUI.gameObject.SetActive(true);

        int secondInt = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
        if (secondInt == lastShownRunSecond)
            return;

        lastShownRunSecond = secondInt;
        int minutes = secondInt / 60;
        int seconds = secondInt % 60;
        runTimerUI.SetText("{0:00}:{1:00}", minutes, seconds);
    }

    private void BindBlockNameObserver()
    {
        blockNameObserverDisposables.Clear();

        Observable.EveryUpdate()
            .Select(_ => ResolveCurrentBlockName())
            .DistinctUntilChanged()
            .Subscribe(SetBlockName)
            .AddTo(blockNameObserverDisposables);
    }

    private string ResolveCurrentBlockName()
    {
        if (BlockManager.Ins != null && BlockManager.Ins.MonsterSpawner != null)
        {
            var spawner = BlockManager.Ins.MonsterSpawner;
            if (spawner.HasActiveEncounter)
                return "Monster!";

            return $"Monster: {spawner.CurrentBreakProgress}/{spawner.BlocksPerSpawn}";
        }

        if (BlockManager.Ins != null && BlockManager.Ins.CurrentBlock != null)
            return BlockManager.Ins.CurrentBlock.BlockName;

        if (DataSaver.Ins != null)
            return DataSaver.Ins.currentBlock;

        return string.Empty;
    }

    private void SetBlockName(string blockName)
    {
        if (blockNameUI == null)
            return;

        string safeName = string.IsNullOrWhiteSpace(blockName) ? "Unknown" : blockName;
        if (safeName == lastShownBlockName)
            return;

        lastShownBlockName = safeName;
        blockNameUI.SetText(safeName);
    }

}

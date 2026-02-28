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
    [SerializeField] private Image HpUI;
    [SerializeField] private Image ManaUI;
    private float displayedManaFill = 0f;

    void OnEnable()
    {
        if (StatsManager.Ins != null)
            StatsManager.Ins.OnStatsRecalculated += RefreshBars;

        RefreshBars();
    }

    void OnDisable()
    {
        if (StatsManager.Ins != null)
            StatsManager.Ins.OnStatsRecalculated -= RefreshBars;
    }

    void Start()
    {
        AddReactToUI();
    }

    void AddReactToUI()
    {
        StatsManager.Ins.GetReactive(StatType.Clicks)
            .ThrottleFirst(TimeSpan.FromSeconds(0.1))
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
        ManaUISetUp();

    }
    void ManaUISetUp()
    {
        var manaFillStream = Observable.CombineLatest(
                StatsManager.Ins.GetReactive(StatType.CurrentMana),
                StatsManager.Ins.GetReactive(StatType.Mana),
                (cur, max) => (max > 0f) ? (cur / max) : 0f)
            .DistinctUntilChanged();
        var targetFill = new ReactiveProperty<float>(0f);
        manaFillStream
            .Subscribe(value => targetFill.Value = value)
            .AddTo(this);
        Observable.EveryUpdate()
            .Subscribe(_ =>
            {
                displayedManaFill = Mathf.Lerp(displayedManaFill, targetFill.Value, Time.deltaTime * 10f);
                ManaUI.fillAmount = displayedManaFill;
            })
            .AddTo(this);
    }

    private void RefreshBars()
    {
        if (StatsManager.Ins == null) return;

        if (HpUI != null)
        {
            float maxHP = StatsManager.Ins.Get(StatType.HP);
            float curHP = StatsManager.Ins.Get(StatType.CurrentHP);
            float fill = (maxHP > 0f) ? (curHP / maxHP) : 0f;
            HpUI.fillAmount = Mathf.Clamp01(fill);
        }

        if (ManaUI != null)
        {
            float maxMana = StatsManager.Ins.Get(StatType.Mana);
            float curMana = StatsManager.Ins.Get(StatType.CurrentMana);
            float fill = (maxMana > 0f) ? (curMana / maxMana) : 0f;
            displayedManaFill = Mathf.Clamp01(fill);
            ManaUI.fillAmount = displayedManaFill;
        }
    }

}

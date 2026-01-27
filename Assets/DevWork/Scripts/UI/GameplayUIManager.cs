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

        StatsManager.Ins.GetReactive(StatType.CurrentHP)
            .Subscribe(val =>
            {
                float maxHP = StatsManager.Ins.Get(StatType.HP);
                float fill = (maxHP > 0f) ? (val / maxHP) : 0f;
                HpUI.fillAmount = Mathf.Clamp01(fill);
            })
            .AddTo(this);
        ManaUISetUp();

    }
    void ManaUISetUp()
    {
        var manaFillStream = StatsManager.Ins.GetReactive(StatType.CurrentMana)
            .Select(val =>
            {
                float maxValue = StatsManager.Ins.Get(StatType.Mana);
                return (maxValue > 0f) ? (val / maxValue) : 0f;
            })
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
}

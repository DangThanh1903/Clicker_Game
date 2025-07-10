using TMPro;
using UniRx;
using UnityEngine;

public class GameplayUIManager : MonoBehaviour
{
    [SerializeField] private TMP_Text clickNumberUI;
    [SerializeField] private TMP_Text clickPerTickUI;
    void Start()
    {
        AddReactToUI();
    }

    void AddReactToUI()
    {
        StatsManager.Ins.GetReactive(StatType.Clicks)
            .Subscribe(val => clickNumberUI.text = $"{val} click")
            .AddTo(this);
        StatsManager.Ins.GetReactive(StatType.ClickPerTick)
            .Subscribe(val => clickPerTickUI.text = $"{val} cpt")
            .AddTo(this);
    }
}

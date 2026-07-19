using System;
using TMPro;
using UniRx;

public sealed class GameplayHudStatsBinder : IDisposable
{
    private readonly TMP_Text clickPerTickText;
    private readonly TMP_Text diamondText;
    private readonly CompositeDisposable subscriptions = new CompositeDisposable();

    private StatsManager boundStatsManager;

    public GameplayHudStatsBinder(TMP_Text clickPerTickText, TMP_Text diamondText)
    {
        this.clickPerTickText = clickPerTickText;
        this.diamondText = diamondText;
    }

    public void Bind()
    {
        TryBindStats();
    }

    public void Tick()
    {
        if (boundStatsManager == null && StatsManager.Ins != null)
            TryBindStats();
    }

    public void Dispose()
    {
        UnbindStats();
    }

    private void TryBindStats()
    {
        if (boundStatsManager == StatsManager.Ins && boundStatsManager != null)
            return;

        UnbindStats();

        boundStatsManager = StatsManager.Ins;
        if (boundStatsManager == null)
            return;

        if (clickPerTickText != null)
        {
            boundStatsManager.GetReactive(StatType.ClickPerTick)
                .DistinctUntilChanged()
                .Throttle(TimeSpan.FromSeconds(0.1f))
                .Subscribe(val => clickPerTickText.SetText("{0} cpt", val))
                .AddTo(subscriptions);
        }

        if (diamondText != null)
        {
            boundStatsManager.GetReactive(StatType.Diamond)
                .DistinctUntilChanged()
                .Throttle(TimeSpan.FromSeconds(0.1f))
                .Subscribe(val => diamondText.SetText("{0}", val))
                .AddTo(subscriptions);
        }
    }

    private void UnbindStats()
    {
        subscriptions.Clear();
        boundStatsManager = null;
    }
}

using System;
using TMPro;
using UniRx;
using UnityEngine;

public sealed class GameplayHudStatsBinder : IDisposable
{
    private readonly TMP_Text biomePercentText;
    private readonly TMP_Text clickPerTickText;
    private readonly TMP_Text diamondText;
    private readonly CompositeDisposable subscriptions = new CompositeDisposable();

    private StatsManager boundStatsManager;
    private BiomeCompletionService biomeCompletionService;

    public GameplayHudStatsBinder(TMP_Text biomePercentText, TMP_Text clickPerTickText, TMP_Text diamondText)
    {
        this.biomePercentText = biomePercentText;
        this.clickPerTickText = clickPerTickText;
        this.diamondText = diamondText;
    }

    public void Bind()
    {
        TryBindStats();
        TryBindBiomeCompletion();
    }

    public void Tick()
    {
        if (boundStatsManager == null && StatsManager.Ins != null)
            TryBindStats();

        if (biomeCompletionService == null && BiomeCompletionService.Ins != null)
            TryBindBiomeCompletion();
    }

    public void Dispose()
    {
        UnbindStats();
        UnbindBiomeCompletion();
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

    private void TryBindBiomeCompletion()
    {
        if (biomePercentText == null)
            return;

        if (BiomeCompletionService.Ins == null)
            BiomeCompletionService.GetOrCreate();

        if (biomeCompletionService == BiomeCompletionService.Ins && biomeCompletionService != null)
            return;

        UnbindBiomeCompletion();

        biomeCompletionService = BiomeCompletionService.Ins;
        if (biomeCompletionService == null)
            return;

        biomeCompletionService.OnProgressChanged += HandleBiomeCompletionChanged;
        biomeCompletionService.Recalculate();
        HandleBiomeCompletionChanged(biomeCompletionService.CurrentSnapshot);
    }

    private void UnbindBiomeCompletion()
    {
        if (biomeCompletionService != null)
            biomeCompletionService.OnProgressChanged -= HandleBiomeCompletionChanged;

        biomeCompletionService = null;
    }

    private void HandleBiomeCompletionChanged(BiomeCompletionSnapshot snapshot)
    {
        if (biomePercentText == null)
            return;

        int percent = Mathf.Clamp(Mathf.FloorToInt(snapshot.Percent * 100f), 0, 100);
        biomePercentText.SetText("{0}%", percent);
    }
}

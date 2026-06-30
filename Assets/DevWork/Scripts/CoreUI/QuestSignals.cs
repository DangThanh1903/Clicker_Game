using UniRx;
using System;

public static class QuestSignals
{
    // (targetId, amount)
    public static readonly Subject<(string targetId, int amount)> OnBreakBlock = new();
    public static readonly Subject<(string targetId, int amount)> OnCollectItem = new();
    public static readonly Subject<(string targetId, int amount)> OnCraftItem  = new();

    // ReachStat: bạn có thể emit mỗi lần stat thay đổi (DPS, HP, MaxCombo, v.v.)
    // (statKey, valueNow)
    public static readonly Subject<(string statKey, double value)> OnStatChanged = new();

    // Helper: emit
    public static void BreakBlock(string id, int amount=1) => OnBreakBlock.OnNext((id, amount));
    public static void CollectItem(string id, int amount=1) => OnCollectItem.OnNext((id, amount));
    public static void CraftItem(string id, int amount=1)  => OnCraftItem.OnNext((id, amount));
    public static void StatChanged(string key, double value) => OnStatChanged.OnNext((key, value));
}

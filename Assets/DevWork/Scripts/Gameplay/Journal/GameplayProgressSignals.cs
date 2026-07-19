using System;

public static class GameplayProgressSignals
{
    public static event Action<string, string, int> BlockBroken;
    public static event Action<string, int> ItemCollected;
    public static event Action<string, int> ItemCrafted;
    public static event Action<string, string> BossKilled;
    public static event Action<string, string> BlockDiscovered;

    public static void RaiseBlockBroken(string blockId, string biomeId, int amount = 1)
    {
        BlockBroken?.Invoke(blockId ?? string.Empty, biomeId ?? string.Empty, amount);
    }

    public static void RaiseItemCollected(string itemId, int amount)
    {
        ItemCollected?.Invoke(itemId ?? string.Empty, amount);
    }

    public static void RaiseItemCrafted(string itemId, int amount = 1)
    {
        ItemCrafted?.Invoke(itemId ?? string.Empty, amount);
    }

    public static void RaiseBossKilled(string bossId, string biomeId)
    {
        BossKilled?.Invoke(bossId ?? string.Empty, biomeId ?? string.Empty);
    }

    public static void RaiseBlockDiscovered(string blockId, string biomeId)
    {
        BlockDiscovered?.Invoke(blockId ?? string.Empty, biomeId ?? string.Empty);
    }
}

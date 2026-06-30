using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class BlockDropFlow
{
    public static IEnumerator Play_Co(string blockName, Vector3 dropOrigin, BlockUVDatabase blockUVDatabase)
    {
        if (blockUVDatabase == null || string.IsNullOrWhiteSpace(blockName))
            yield break;

        float luck = StatsManager.Ins != null ? StatsManager.Ins.Get(StatType.Lucky) : 0f;
        var drops = blockUVDatabase.GetDropResultsByName(blockName, luck);
        if (drops == null || drops.Count == 0)
        {
            LogNoDrops(blockName);
            yield break;
        }

        var resolvedDrops = new List<DropGrantEntry>(drops.Count);
        for (int i = 0; i < drops.Count; i++)
        {
            ItemDropResult result = drops[i];
            Item item = null;
            yield return DropGrantService.ResolveItemFromDrop_Co(
                result.drop,
                $"block '{blockName}'",
                resolved => item = resolved);

            if (item == null)
            {
                Debug.LogWarning($"[Drop] Null item in drop list for block: {blockName}");
                continue;
            }

            int requested = Mathf.Max(0, result.amount);
            if (requested <= 0)
                continue;

            resolvedDrops.Add(new DropGrantEntry(item, requested));
        }

        if (resolvedDrops.Count == 0)
        {
            LogNoDrops(blockName);
            yield break;
        }

        void OnItemGranted(Item grantedItem, int added)
        {
            string itemId = Game.Discovery.BlockDiscoveryService.GetItemId(grantedItem);
            Game.Discovery.BlockDiscoveryService.Ins?.DiscoverDrop(blockName, itemId);
        }

        bool addedAny = false;
        string dropSummary = string.Empty;

        if (BlockDropCollectAnimator.Ins != null)
        {
            yield return BlockDropCollectAnimator.Ins.PlayThenGrantDrops_Co(
                resolvedDrops,
                dropOrigin,
                OnItemGranted,
                "[BlockDrop]",
                (granted, summary) =>
                {
                    addedAny = granted;
                    dropSummary = summary;
                });
        }
        else
        {
            addedAny = DropGrantService.TryGrantDrops(
                resolvedDrops,
                out dropSummary,
                OnItemGranted,
                "[BlockDrop]");
        }

        if (!addedAny)
        {
            LogNoDrops(blockName);
            yield break;
        }

        GameDebugHandler.LogStaticKey(
            "UI_Debug",
            "block_drops",
            new { block = blockName, items = dropSummary });
    }

    private static void LogNoDrops(string blockName)
    {
        DevLog.Log("There is no item drop");
        GameDebugHandler.LogStaticKey(
            "UI_Debug",
            "block_drops_none",
            new { block = blockName });
    }
}

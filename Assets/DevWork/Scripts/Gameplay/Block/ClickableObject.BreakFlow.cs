using UnityEngine;

public partial class ClickableObject
{
    private void HandleItemDrop()
    {
        StartCoroutine(BlockDropFlow.Play_Co(blockName, transform.position, blockUVDatabase));
    }

    private void FinalizeBreak()
    {
        if (breakFinalized)
            return;

        breakFinalized = true;

        UpdateCrackVisual(MaxHealth);
        Game.Discovery.BlockDiscoveryService.Ins?.DiscoverBlock(blockName);
        HandleItemDrop();
        isDyingEffect = false;
        PlayBreakedSound();

        DamageStatsRecorder.RecordBlockBreak();
        BlockManager.Ins.OnBlockBroken();
    }

    private string GetLocationString()
    {
        return DataSaver.Ins != null && DataSaver.Ins.currentLocation.HasValue
            ? DataSaver.Ins.currentLocation.Value.ToString()
            : "unknown";
    }
}

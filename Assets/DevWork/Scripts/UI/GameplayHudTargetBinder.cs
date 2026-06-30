using TMPro;
using UniRx;
using UnityEngine;

public sealed class GameplayHudTargetBinder
{
    private readonly TMP_Text blockNameText;
    private readonly TMP_Text blockHealthText;
    private readonly CompositeDisposable healthSubscriptions = new CompositeDisposable();

    private BlockManager boundBlockManager;
    private string lastShownBlockName = string.Empty;

    public GameplayHudTargetBinder(TMP_Text blockNameText, TMP_Text blockHealthText)
    {
        this.blockNameText = blockNameText;
        this.blockHealthText = blockHealthText;
    }

    public void Bind()
    {
        TryBindBlockManager();
        RefreshBlockName();
        RebindBlockHealth();
    }

    public void Tick()
    {
        if (boundBlockManager == null && BlockManager.Ins != null)
            TryBindBlockManager();

        RefreshBlockName();
    }

    public void Unbind()
    {
        if (boundBlockManager != null)
            boundBlockManager.CurrentBlockChanged -= HandleCurrentBlockChanged;

        boundBlockManager = null;
        healthSubscriptions.Clear();
    }

    private void TryBindBlockManager()
    {
        if (boundBlockManager == BlockManager.Ins && boundBlockManager != null)
            return;

        Unbind();

        boundBlockManager = BlockManager.Ins;
        if (boundBlockManager == null)
            return;

        boundBlockManager.CurrentBlockChanged += HandleCurrentBlockChanged;
    }

    private void HandleCurrentBlockChanged(string _)
    {
        RebindBlockHealth();
        RefreshBlockName();
    }

    private void RefreshBlockName()
    {
        SetBlockName(ResolveCurrentBlockName());
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
        if (blockNameText == null)
            return;

        string safeName = string.IsNullOrWhiteSpace(blockName) ? "Unknown" : blockName;
        if (safeName == lastShownBlockName)
            return;

        lastShownBlockName = safeName;
        blockNameText.SetText(safeName);
    }

    private void RebindBlockHealth()
    {
        healthSubscriptions.Clear();

        if (blockHealthText == null)
            return;

        if (!TryGetVisibleBlock(out ClickableObject block) || block.CurrentHealth == null)
        {
            HideBlockHealthText();
            return;
        }

        blockHealthText.gameObject.SetActive(true);
        block.CurrentHealth
            .DistinctUntilChanged()
            .Subscribe(currentHealth => SetBlockHealthText(currentHealth, block.MaxHealth))
            .AddTo(healthSubscriptions);
    }

    private bool TryGetVisibleBlock(out ClickableObject block)
    {
        block = null;

        if (BlockManager.Ins == null)
            return false;

        if (BlockManager.Ins.MonsterSpawner != null && BlockManager.Ins.MonsterSpawner.HasActiveEncounter)
            return false;

        block = BlockManager.Ins.CurrentBlock;
        return block != null && block.gameObject.activeInHierarchy && block.MaxHealth > 0f;
    }

    private void SetBlockHealthText(float currentHealth, float maxHealth)
    {
        if (blockHealthText == null)
            return;

        int shownCurrent = Mathf.CeilToInt(Mathf.Clamp(currentHealth, 0f, maxHealth));
        int shownMax = Mathf.CeilToInt(Mathf.Max(0f, maxHealth));
        blockHealthText.SetText("{0} / {1}", shownCurrent, shownMax);
    }

    private void HideBlockHealthText()
    {
        if (blockHealthText != null && blockHealthText.gameObject.activeSelf)
            blockHealthText.gameObject.SetActive(false);
    }
}

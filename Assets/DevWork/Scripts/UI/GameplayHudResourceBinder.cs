using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameplayHudResourceBinder
{
    private readonly TMP_Text runTimerText;
    private readonly Image resourceImage;
    private readonly Sprite manaSprite;
    private readonly Sprite staminaSprite;
    private readonly Sprite idleSprite;

    private float displayedFill;
    private int lastShownRunSecond = int.MinValue;
    private ResourceDisplayMode currentMode = ResourceDisplayMode.Mana;

    private enum ResourceDisplayMode
    {
        Stamina,
        Mana,
        Idle
    }

    public GameplayHudResourceBinder(
        TMP_Text runTimerText,
        Image resourceImage,
        Sprite manaSprite,
        Sprite staminaSprite,
        Sprite idleSprite)
    {
        this.runTimerText = runTimerText;
        this.resourceImage = resourceImage;
        this.manaSprite = manaSprite;
        this.staminaSprite = staminaSprite;
        this.idleSprite = idleSprite;
    }

    public void RefreshImmediate()
    {
        ResourceDisplayMode mode = ResolveMode();
        if (mode != currentMode)
            ApplyModeVisual(mode);

        displayedFill = Mathf.Clamp01(GetTargetFill(mode));
        if (resourceImage != null)
            resourceImage.fillAmount = displayedFill;

        UpdateRunTimer();
    }

    public void Tick()
    {
        if (resourceImage == null)
        {
            UpdateRunTimer();
            return;
        }

        ResourceDisplayMode mode = ResolveMode();
        if (mode != currentMode)
            ApplyModeVisual(mode);

        float targetFill = GetTargetFill(mode);
        displayedFill = Mathf.Lerp(displayedFill, targetFill, Time.deltaTime * 10f);
        resourceImage.fillAmount = displayedFill;

        UpdateRunTimer();
    }

    private ResourceDisplayMode ResolveMode()
    {
        var player = PlayerController.Instance;
        if (player == null || player.currentState == null)
            return ResourceDisplayMode.Mana;

        if (player.currentState is HoldState)
            return ResourceDisplayMode.Mana;
        if (player.currentState is NormalState)
            return ResourceDisplayMode.Stamina;
        if (player.currentState is IdleState)
            return ResourceDisplayMode.Idle;

        return ResourceDisplayMode.Mana;
    }

    private float GetTargetFill(ResourceDisplayMode mode)
    {
        switch (mode)
        {
            case ResourceDisplayMode.Stamina:
                return PlayerController.Instance != null ? PlayerController.Instance.GetStaminaPercent() : 0f;
            case ResourceDisplayMode.Mana:
                if (StatsManager.Ins == null)
                    return 0f;

                float maxMana = StatsManager.Ins.Get(StatType.Mana);
                float currentMana = StatsManager.Ins.Get(StatType.CurrentMana);
                return maxMana > 0f ? Mathf.Clamp01(currentMana / maxMana) : 0f;
            case ResourceDisplayMode.Idle:
                return PlayerController.Instance != null ? PlayerController.Instance.GetIdleStackPercent() : 0f;
            default:
                return 0f;
        }
    }

    private void ApplyModeVisual(ResourceDisplayMode mode)
    {
        currentMode = mode;

        if (resourceImage == null)
            return;

        resourceImage.gameObject.SetActive(true);

        Sprite useSprite = manaSprite;
        switch (mode)
        {
            case ResourceDisplayMode.Stamina:
                useSprite = staminaSprite;
                break;
            case ResourceDisplayMode.Idle:
                useSprite = idleSprite;
                break;
        }

        if (useSprite != null)
            resourceImage.sprite = useSprite;
    }

    private void UpdateRunTimer()
    {
        if (runTimerText == null)
            return;

        float remainingSeconds = -1f;
        if (DungeonRunManager.Ins != null && DungeonRunManager.Ins.IsRunning)
            remainingSeconds = DungeonRunManager.Ins.RemainingRunTime;
        else if (BlockManager.Ins != null && BlockManager.Ins.IsBossTimerRunning)
            remainingSeconds = BlockManager.Ins.BossRemainingTime;

        if (remainingSeconds < 0f)
        {
            if (runTimerText.gameObject.activeSelf)
                runTimerText.gameObject.SetActive(false);

            lastShownRunSecond = int.MinValue;
            return;
        }

        if (!runTimerText.gameObject.activeSelf)
            runTimerText.gameObject.SetActive(true);

        int secondInt = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
        if (secondInt == lastShownRunSecond)
            return;

        lastShownRunSecond = secondInt;
        int minutes = secondInt / 60;
        int seconds = secondInt % 60;
        runTimerText.SetText("{0:00}:{1:00}", minutes, seconds);
    }
}

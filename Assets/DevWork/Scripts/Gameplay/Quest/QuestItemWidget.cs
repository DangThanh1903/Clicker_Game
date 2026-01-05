using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UniRx;
using System.Linq;

public class QuestItemWidget : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button rootButton;             // whole panel is a button
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image fillImage;               // filled progress image
    [SerializeField] private TMP_Text progressNumberText;   // "current / required"
    [SerializeField] private Image rewardIcon;
    [SerializeField] private Image background;

    [Header("Reward Icon")]
    [SerializeField] private Sprite gemIcon;                // used if only gem reward

    private CompositeDisposable _cd = new();
    private string _questId;
    private bool _canClaim;

    public void Bind(QuestDef def, QuestTracker tr)
    {
        _cd.Clear();
        _questId = def.id;
        _canClaim = false;

        // ---- Title ----
        if (titleText)
            titleText.text = def.title;

        // ---- Initial description (current step) ----
        if (descriptionText)
            descriptionText.text = BuildCurrentStepDescription(def, tr);

        // ---- Progress (bar + numeric + dynamic description) ----
        var stepChangeStream =
            Observable.Merge(tr.Steps.Select(s => s.Current.Select(_ => Unit.Default)));

        stepChangeStream
            .StartWith(Unit.Default)
            .Subscribe(_ =>
            {
                int totalCurrent  = tr.Steps.Sum(s => s.Current.Value);
                int totalRequired = tr.Steps.Sum(s => s.Required.Value);

                float ratio = (totalRequired > 0)
                    ? (float)totalCurrent / totalRequired
                    : 0f;

                if (fillImage != null)
                    fillImage.fillAmount = Mathf.Clamp01(ratio);

                if (progressNumberText != null)
                    progressNumberText.text = $"{totalCurrent} / {totalRequired}";

                // Update description to reflect the current step and (stepIndex/totalSteps)
                if (descriptionText != null)
                    descriptionText.text = BuildCurrentStepDescription(def, tr);
            })
            .AddTo(_cd);

        // ---- Completed / Claimed state ----
        Observable.CombineLatest(tr.Completed, tr.RewardClaimed,
            (completed, rewardClaimed) => (completed, rewardClaimed))
            .Subscribe(state =>
            {
                bool completed = state.completed;
                bool rewardClaimed = state.rewardClaimed;

                _canClaim = completed && !rewardClaimed;

                // Background color feedback (optional)
                if (background)
                {
                    if (rewardClaimed)
                        background.color = new Color(0.55f, 0.55f, 0.55f);   // grey for claimed
                    else if (_canClaim)
                        background.color = new Color(0.7f, 0.9f, 0.7f);      // green-ish for ready
                    else
                        background.color = Color.white;                      // normal
                }

                if (rootButton != null)
                    rootButton.interactable = _canClaim;
            })
            .AddTo(_cd);

        // ---- Reward icon ----
        SetupRewardIcon(def);

        // ---- Click to claim (whole panel) ----
        if (rootButton == null)
            rootButton = GetComponent<Button>();

        if (rootButton != null)
        {
            rootButton.onClick.RemoveAllListeners();
            rootButton.onClick.AddListener(OnRootClicked);
        }
    }

    /// <summary>
    /// Builds description for the *current* active step:
    /// e.g. "Break 10 Stone (1/3)" then later "Collect 5 Apple (2/3)".
    /// </summary>
    private string BuildCurrentStepDescription(QuestDef def, QuestTracker tr)
    {
        if (def.steps == null || def.steps.Count == 0)
            return "";

        int totalSteps = def.steps.Count;

        // Find index of the first not-completed step
        int activeIndex = -1;
        for (int i = 0; i < tr.Steps.Count; i++)
        {
            if (!tr.Steps[i].Completed.Value)
            {
                activeIndex = i;
                break;
            }
        }

        // If all steps are completed, show the last step as "current"
        if (activeIndex < 0)
            activeIndex = totalSteps - 1;

        // Safety clamp
        activeIndex = Mathf.Clamp(activeIndex, 0, totalSteps - 1);

        var stepDef = def.steps[activeIndex];

        string stepText = BuildStepDescription(stepDef);
        string stepProgress = $"{activeIndex + 1}/{totalSteps}";

        if (string.IsNullOrEmpty(stepText))
            return $"Step ({stepProgress})";

        return $"{stepText} ({stepProgress})";
    }

    /// <summary>
    /// Creates text for a single step like:
    /// BreakBlock + Stone + 10 → "Break 10 Stone"
    /// CollectItem + Apple + 5 → "Collect 5 Apple"
    /// </summary>
    private string BuildStepDescription(QuestStepDef step)
    {
        if (step == null) return "";

        string targetRaw = step.targetId;
        int amount = step.requiredAmount;

        // Optional: clean target if it has biome info like "Dirt@Plain"
        string target = targetRaw;
        int atIdx = targetRaw.IndexOf('@');
        if (atIdx >= 0)
            target = targetRaw.Substring(0, atIdx);

        // Capitalize first letter
        if (!string.IsNullOrEmpty(target))
        {
            target = char.ToUpper(target[0]) + (target.Length > 1 ? target.Substring(1) : "");
        }

        switch (step.goalType)
        {
            case GoalType.BreakBlock:
                return $"Break {amount} {target}";
            case GoalType.CollectItem:
                return $"Collect {amount} {target}";
            case GoalType.CraftItem:
                return $"Craft {amount} {target}";
            case GoalType.ReachStat:
                return $"Reach {target} ≥ {amount}";
            case GoalType.Custom:
            default:
                return "";
        }
    }

    private void SetupRewardIcon(QuestDef def)
    {
        if (rewardIcon == null)
            return;

        Sprite icon = null;

        if (def.rewards != null)
        {
            // 1) Try to use the first valid item reward's icon
            foreach (var r in def.rewards)
            {
                var firstItem = r.items?
                    .FirstOrDefault(i => i != null && i.itemData != null && i.quantity.Value > 0);

                if (firstItem != null && firstItem.itemData != null)
                {
                    // Assumes your Item class has a Sprite field named "icon"
                    icon = firstItem.itemData.icon;
                    break;
                }
            }

            // 2) If no item icon found, but there are gems, use gem icon
            if (icon == null && gemIcon != null)
            {
                bool hasGem = def.rewards.Any(r => r.gemAmount > 0);
                if (hasGem)
                    icon = gemIcon;
            }
        }

        if (icon != null)
        {
            rewardIcon.sprite = icon;
            rewardIcon.enabled = true;
        }
        else
        {
            rewardIcon.enabled = false;
        }
    }

    private void OnRootClicked()
    {
        if (!_canClaim)
            return;

        QuestManager.Ins.ClaimReward(_questId);
    }

    private void OnDisable()
    {
        _cd.Clear();
    }
}

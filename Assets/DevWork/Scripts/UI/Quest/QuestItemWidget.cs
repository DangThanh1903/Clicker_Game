using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestItemWidget : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button rootButton;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text progressNumberText;
    [SerializeField] private Image rewardIcon;
    [SerializeField] private Image background;

    [Header("Reward Icon")]
    [SerializeField] private Sprite gemIcon;

    private string questId;
    private bool canClaim;

    public void Bind(QuestRuntimeView view)
    {
        questId = view.QuestId;
        canClaim = view.CanClaim;

        if (rootButton == null)
            rootButton = GetComponent<Button>();

        if (titleText != null)
            titleText.text = view.Title;

        if (descriptionText != null)
            descriptionText.text = view.Description;

        if (fillImage != null)
            fillImage.fillAmount = view.Progress01;

        if (progressNumberText != null)
            progressNumberText.text = $"{view.CurrentAmount} / {view.RequiredAmount}";

        ApplyClaimState(view.Completed, view.RewardClaimed, view.CanClaim);
        ApplyRewardIcon(view);

        if (rootButton != null)
        {
            rootButton.onClick.RemoveAllListeners();
            rootButton.onClick.AddListener(OnRootClicked);
        }
    }

    private void ApplyRewardIcon(QuestRuntimeView view)
    {
        if (rewardIcon == null)
            return;

        Sprite icon = view.RewardIcon;
        if (icon == null && view.UseGemRewardIcon)
            icon = gemIcon;

        rewardIcon.sprite = icon;
        rewardIcon.enabled = icon != null;
    }

    private void OnRootClicked()
    {
        if (!canClaim || string.IsNullOrWhiteSpace(questId) || QuestManager.Ins == null)
            return;

        QuestManager.Ins.ClaimReward(questId);
    }

    private void ApplyClaimState(bool completed, bool rewardClaimed, bool readyToClaim)
    {
        canClaim = readyToClaim;

        if (background != null)
        {
            if (rewardClaimed)
                background.color = new Color(0.55f, 0.55f, 0.55f);
            else if (readyToClaim)
                background.color = new Color(0.7f, 0.9f, 0.7f);
            else
                background.color = Color.white;
        }

        if (rootButton != null)
            rootButton.interactable = readyToClaim;
    }
}

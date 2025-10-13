using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UniRx;
using System.Linq;

public class QuestItemWidget : MonoBehaviour
{
    public TMP_Text titleText;
    public TMP_Text descText;
    public TMP_Text progressText;
    public Button claimButton;

    private CompositeDisposable _cd = new();
    private string _questId;

    public void Bind(QuestDef def, QuestTracker tr)
    {
        _cd.Clear();
        _questId = def.id;

        titleText.text = def.title;
        descText.text  = def.description;

        // cập nhật text khi bất kỳ step progress đổi
        // gộp nhiều stream: Current & Required của từng step
        Observable.Merge(tr.Steps.Select(s => s.Current.Select(_ => Unit.Default)))
            .StartWith(Unit.Default)
            .Subscribe(_ =>
            {
                var lines = def.steps.Zip(tr.Steps, (d, s) => $"{d.stepId}: {s.Current.Value}/{s.Required.Value}");
                progressText.text = string.Join("\n", lines);
            })
            .AddTo(_cd);

        // nút claim
        Observable.CombineLatest(tr.Completed, tr.RewardClaimed, (c, r) => c && !r)
            .Subscribe(canClaim =>
            {
                claimButton.interactable = canClaim;
            })
            .AddTo(_cd);

        claimButton.onClick.RemoveAllListeners();
        claimButton.onClick.AddListener(() => QuestManager.Ins.ClaimReward(_questId));
    }

    private void OnDisable() => _cd.Clear();
}

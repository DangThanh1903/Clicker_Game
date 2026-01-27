using UnityEngine;
using UniRx;

public class QuestDailyUI : MonoBehaviour
{
    public Transform contentRoot;
    public QuestItemWidget itemPrefab;
    private CompositeDisposable _cd = new CompositeDisposable();

    private void OnEnable()
    {
        if (QuestManager.Ins == null)
            return;

        _cd?.Dispose();
        _cd = new CompositeDisposable();

        Observable.Merge(
                QuestManager.Ins.OnAnyQuestListChanged,
                QuestManager.Ins.OnQuestUpdated.Select(_ => Unit.Default)
            )
            .ThrottleFrame(1)
            .Subscribe(_ => Refresh())
            .AddTo(_cd);
        Refresh();
    }

    private void OnDisable()
    {
        _cd?.Dispose();
    }

    private void Refresh()
    {
        foreach (Transform c in contentRoot) Destroy(c.gameObject);
        foreach (var (def, tr) in QuestManager.Ins.GetActiveDaily())
        {
            var w = Instantiate(itemPrefab, contentRoot);
            w.Bind(def, tr);
        }
    }
}

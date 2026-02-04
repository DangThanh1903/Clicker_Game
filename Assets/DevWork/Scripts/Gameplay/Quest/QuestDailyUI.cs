using UnityEngine;
using UniRx;

public class QuestDailyUI : MonoBehaviour
{
    public Transform contentRoot;
    public QuestItemWidget itemPrefab;
    private CompositeDisposable _cd = new CompositeDisposable();
    private readonly System.Collections.Generic.List<QuestItemWidget> _pool = new System.Collections.Generic.List<QuestItemWidget>();
    private int _activeCount = 0;

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
        _activeCount = 0;
        foreach (var (def, tr) in QuestManager.Ins.GetActiveDaily())
        {
            var w = GetOrCreate();
            w.Bind(def, tr);
        }

        for (int i = _activeCount; i < _pool.Count; i++)
            _pool[i].gameObject.SetActive(false);
    }

    private QuestItemWidget GetOrCreate()
    {
        QuestItemWidget widget;
        if (_activeCount < _pool.Count)
        {
            widget = _pool[_activeCount];
            widget.gameObject.SetActive(true);
        }
        else
        {
            widget = Instantiate(itemPrefab, contentRoot);
            _pool.Add(widget);
        }

        if (widget.transform.parent != contentRoot)
            widget.transform.SetParent(contentRoot, false);

        _activeCount++;
        return widget;
    }
}

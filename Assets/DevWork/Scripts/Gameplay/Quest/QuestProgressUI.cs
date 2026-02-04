using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;

public class QuestProgressUI : MonoBehaviour
{
    [Header("ScrollView Content")]
    [SerializeField] private RectTransform contentRoot;   // ScrollView -> Viewport -> Content
    [Header("Prefabs")]
    [SerializeField] private QuestItemWidget itemPrefab;

    private CompositeDisposable _cd = new CompositeDisposable();
    private readonly List<QuestItemWidget> _pool = new List<QuestItemWidget>();
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
        if (contentRoot == null || itemPrefab == null)
        {
            Debug.LogWarning("[QuestProgressUI] Missing references.");
            return;
        }

        _activeCount = 0;

        // Rebuild from quest data using pooled widgets
        foreach (var (def, tr) in QuestManager.Ins.GetAllProgress())
        {
            var widget = GetOrCreate();
            widget.Bind(def, tr);
        }

        // Hide extras
        for (int i = _activeCount; i < _pool.Count; i++)
            _pool[i].gameObject.SetActive(false);

        // Force layout rebuild so VerticalLayoutGroup + ContentSizeFitter update immediately
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
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

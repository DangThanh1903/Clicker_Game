using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuestDailyUI : MonoBehaviour
{
    public Transform contentRoot;
    public QuestItemWidget itemPrefab;

    private readonly List<QuestItemWidget> pool = new List<QuestItemWidget>();
    private QuestManager boundQuestManager;
    private Coroutine bindCo;
    private int activeCount;

    private void OnEnable()
    {
        TryBindQuestManager();
        if (boundQuestManager == null && bindCo == null)
            bindCo = StartCoroutine(BindNextFrame());

        Refresh();
    }

    private void OnDisable()
    {
        if (bindCo != null)
        {
            StopCoroutine(bindCo);
            bindCo = null;
        }

        UnbindQuestManager();
    }

    private IEnumerator BindNextFrame()
    {
        yield return null;
        bindCo = null;
        TryBindQuestManager();
        Refresh();
    }

    private void TryBindQuestManager()
    {
        if (boundQuestManager == QuestManager.Ins && boundQuestManager != null)
            return;

        UnbindQuestManager();

        boundQuestManager = QuestManager.Ins;
        if (boundQuestManager == null)
            return;

        boundQuestManager.QuestListChanged += HandleQuestListChanged;
        boundQuestManager.QuestChanged += HandleQuestChanged;
    }

    private void UnbindQuestManager()
    {
        if (boundQuestManager == null)
            return;

        boundQuestManager.QuestListChanged -= HandleQuestListChanged;
        boundQuestManager.QuestChanged -= HandleQuestChanged;
        boundQuestManager = null;
    }

    private void HandleQuestListChanged(QuestType _)
    {
        Refresh();
    }

    private void HandleQuestChanged(QuestRuntimeEntry _)
    {
        Refresh();
    }

    private void Refresh()
    {
        activeCount = 0;
        TryBindQuestManager();

        if (boundQuestManager != null)
        {
            foreach (var view in boundQuestManager.GetViews(QuestType.Daily))
            {
                QuestItemWidget widget = GetOrCreate();
                widget.Bind(view);
            }
        }

        for (int i = activeCount; i < pool.Count; i++)
            pool[i].gameObject.SetActive(false);
    }

    private QuestItemWidget GetOrCreate()
    {
        QuestItemWidget widget;
        if (activeCount < pool.Count)
        {
            widget = pool[activeCount];
            widget.gameObject.SetActive(true);
        }
        else
        {
            widget = Instantiate(itemPrefab, contentRoot);
            pool.Add(widget);
        }

        if (widget.transform.parent != contentRoot)
            widget.transform.SetParent(contentRoot, false);

        activeCount++;
        return widget;
    }
}

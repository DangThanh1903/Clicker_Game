using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class QuestProgressUI : MonoBehaviour
{
    [Header("List")]
    [SerializeField] private QuestType questType = QuestType.Progress;

    [Header("ScrollView Content")]
    [SerializeField] private RectTransform contentRoot;

    [Header("Prefabs")]
    [SerializeField] private QuestItemWidget itemPrefab;

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
        if (contentRoot == null || itemPrefab == null)
        {
            Debug.LogWarning("[QuestProgressUI] Missing references.");
            return;
        }

        TryBindQuestManager();
        activeCount = 0;

        if (boundQuestManager != null)
        {
            foreach (var view in boundQuestManager.GetViews(questType))
            {
                QuestItemWidget widget = GetOrCreate();
                widget.Bind(view);
            }
        }

        for (int i = activeCount; i < pool.Count; i++)
            pool[i].gameObject.SetActive(false);

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
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

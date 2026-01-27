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

        // Clear old items
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(contentRoot.GetChild(i).gameObject);
        }

        // Rebuild from quest data
        foreach (var (def, tr) in QuestManager.Ins.GetAllProgress())
        {
            var widget = Instantiate(itemPrefab, contentRoot);
            widget.Bind(def, tr);
        }

        // Force layout rebuild so VerticalLayoutGroup + ContentSizeFitter
        // update the content size immediately
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
    }
}

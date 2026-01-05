using UnityEngine;
using UnityEngine.UI;
using UniRx;

public class QuestProgressUI : MonoBehaviour
{
    [Header("ScrollView Content")]
    [SerializeField] private RectTransform contentRoot;   // ScrollView -> Viewport -> Content
    [Header("Prefabs")]
    [SerializeField] private QuestItemWidget itemPrefab;

    private void Awake()
    {
        // Subscribe once for lifetime of this UI
        QuestManager.Ins.OnAnyQuestListChanged
            .Subscribe(_ => Refresh())
            .AddTo(this);

        QuestManager.Ins.OnQuestUpdated
            .Subscribe(_ => Refresh())
            .AddTo(this);
    }

    private void OnEnable()
    {
        // Make sure UI is correct whenever this panel is shown
        Refresh();
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

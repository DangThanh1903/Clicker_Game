using UnityEngine;
using UniRx;

public class QuestDailyUI : MonoBehaviour
{
    public Transform contentRoot;
    public QuestItemWidget itemPrefab;

    private void OnEnable()
    {
        QuestManager.Ins.OnAnyQuestListChanged.Subscribe(_ => Refresh()).AddTo(this);
        QuestManager.Ins.OnQuestUpdated.Subscribe(_ => Refresh()).AddTo(this);
        Refresh();
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

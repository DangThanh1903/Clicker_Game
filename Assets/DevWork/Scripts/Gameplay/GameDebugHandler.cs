using System.Collections.Generic;
using Lean.Pool;
using TMPro;
using UnityEngine;

public class GameDebugHandler : MonoBehaviour
{
    public static GameDebugHandler Ins { get; private set; }

    [Header("UI")]
    [SerializeField] RectTransform content;    // has VerticalLayoutGroup
    [SerializeField] GameObject rowPrefab;     // has AutoFadeDespawn (+ CanvasGroup) + TMP/Text

    [Header("Limit")]
    [SerializeField] int maxRows = 5;

    readonly Queue<GameObject> rows = new();

    void Awake()
    {
        if (Ins != null && Ins != this) { Destroy(gameObject); return; }
        Ins = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Log(string message)
    {
        if (!content || !rowPrefab || string.IsNullOrEmpty(message)) return;

        // 1) Clean out any rows that already auto-despawned
        CompactQueue();

        // 2) Spawn new row at bottom
        var go = LeanPool.Spawn(rowPrefab, content);
        go.transform.SetAsLastSibling();

        // 3) Set text (support child text components too)
        if (!go.TryGetComponent<TMP_Text>(out var tmp))
            tmp = go.GetComponentInChildren<TMP_Text>(true);
        if (tmp) tmp.text = message;
        else
        {
            var ui = go.TryGetComponent<UnityEngine.UI.Text>(out var t0) ? t0
                    : go.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (ui) ui.text = message;
        }

        rows.Enqueue(go);

        // 4) Trim to maxRows (ignore entries that got despawned meanwhile)
        while (rows.Count > Mathf.Max(1, maxRows))
        {
            var oldest = rows.Dequeue();
            if (oldest && oldest.activeInHierarchy) LeanPool.Despawn(oldest);
        }
    }

    public static void LogStatic(string msg) => Ins?.Log(msg);

    public void Clear()
    {
        while (rows.Count > 0)
        {
            var go = rows.Dequeue();
            if (go && go.activeInHierarchy) LeanPool.Despawn(go);
        }
    }

    void CompactQueue()
    {
        // drop stale refs from the front (those already auto-despawned)
        while (rows.Count > 0)
        {
            var peek = rows.Peek();
            if (peek == null || !peek.activeInHierarchy) rows.Dequeue();
            else break;
        }
    }
    public void LogAfter(string message, float delaySeconds, bool unscaledTime = true)
    {
        if (delaySeconds <= 0f) { Log(message); return; }
        StartCoroutine(LogAfter_Co(message, delaySeconds, unscaledTime));
    }

    System.Collections.IEnumerator LogAfter_Co(string msg, float delay, bool unscaled)
    {
        if (unscaled) yield return new WaitForSecondsRealtime(delay);
        else          yield return new WaitForSeconds(delay);

        // still alive? (in case this was destroyed during the wait)
        if (this && gameObject && isActiveAndEnabled) Log(msg);
    }

    // handy static helper
    public static void LogStaticAfter(string message, float delaySeconds, bool unscaledTime = true)
        => Ins?.LogAfter(message, delaySeconds, unscaledTime);

}

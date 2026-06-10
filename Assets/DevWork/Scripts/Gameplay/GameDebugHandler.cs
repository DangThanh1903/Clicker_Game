using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using Lean.Pool;

using UnityEngine.Localization;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

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

    // ========= STRING (existing) =========
    public void Log(string message)
    {
        if (!content || !rowPrefab || string.IsNullOrEmpty(message)) return;

        CompactQueue();

        var go = LeanPool.Spawn(rowPrefab, content);
        go.transform.SetAsLastSibling();

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

        while (rows.Count > Mathf.Max(1, maxRows))
        {
            var oldest = rows.Dequeue();
            if (oldest && oldest.activeInHierarchy) LeanPool.Despawn(oldest);
        }
    }

    public static void LogStatic(string msg) => Ins?.Log(msg);

    // ========= LOCALIZED =========
    /// <summary>
    /// Resolve a LocalizedString (supports Smart Strings via args) and log it.
    /// </summary>
    public void Log(LocalizedString loc, params object[] args)
    {
        if (loc == null) return;
        StartCoroutine(ResolveAndLog_Co(loc, args));
    }

    /// <summary>
    public void LogKey(string tableName, string entryKey, params object[] args)
    {
        if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(entryKey)) return;

        string text = LocalizedTextUtility.GetLocalizedString($"{tableName}/{entryKey}", null);
        if (text == null)
            text = LocalizedTextUtility.GetLocalizedString($"{tableName}.{entryKey}", null);
        if (text == null)
            text = LocalizedTextUtility.GetLocalizedString(entryKey, entryKey);

        text = ApplyFormatArgs(text, args);

        if (!string.IsNullOrEmpty(text))
            Log(text);
    }

    IEnumerator ResolveAndLog_Co(LocalizedString loc, object[] args)
    {
        // assign Smart String args if provided
        if (args != null && args.Length > 0) loc.Arguments = args;

        AsyncOperationHandle<string> handle;
        try
        {
            handle = loc.GetLocalizedStringAsync();
        }
        catch
        {
            yield break;
        }

        // Wait for completion
        yield return handle;
        string text = null;
        if (handle.Status == AsyncOperationStatus.Succeeded)
            text = handle.Result;

        // Always release the handle
        Addressables.Release(handle);

        if (!string.IsNullOrEmpty(text))
            Log(text);
    }

    // ========= DELAYED (existing) =========
    public void LogAfter(string message, float delaySeconds, bool unscaledTime = true)
    {
        if (delaySeconds <= 0f) { Log(message); return; }
        StartCoroutine(LogAfter_Co(message, delaySeconds, unscaledTime));
    }

    public void LogAfter(LocalizedString loc, float delaySeconds, bool unscaledTime = true, params object[] args)
    {
        if (delaySeconds <= 0f) { Log(loc, args); return; }
        StartCoroutine(LogAfterLocalized_Co(loc, delaySeconds, unscaledTime, args));
    }

    public void LogKeyAfter(string tableName, string entryKey, float delaySeconds, bool unscaledTime = true, params object[] args)
    {
        if (delaySeconds <= 0f) { LogKey(tableName, entryKey, args); return; }
        StartCoroutine(LogKeyAfter_Co(tableName, entryKey, delaySeconds, unscaledTime, args));
    }

    IEnumerator LogAfter_Co(string msg, float delay, bool unscaled)
    {
        if (unscaled) yield return new WaitForSecondsRealtime(delay);
        else          yield return new WaitForSeconds(delay);

        if (this && gameObject && isActiveAndEnabled) Log(msg);
    }

    IEnumerator LogAfterLocalized_Co(LocalizedString loc, float delay, bool unscaled, object[] args)
    {
        if (unscaled) yield return new WaitForSecondsRealtime(delay);
        else          yield return new WaitForSeconds(delay);

        if (this && gameObject && isActiveAndEnabled) Log(loc, args);
    }

    IEnumerator LogKeyAfter_Co(string table, string key, float delay, bool unscaled, object[] args)
    {
        if (unscaled) yield return new WaitForSecondsRealtime(delay);
        else          yield return new WaitForSeconds(delay);

        if (this && gameObject && isActiveAndEnabled) LogKey(table, key, args);
    }

    // ========= STATIC HELPERS =========
    public static void LogStatic(LocalizedString loc, params object[] args) => Ins?.Log(loc, args);
    public static void LogStaticKey(string table, string key, params object[] args) => Ins?.LogKey(table, key, args);
    public static void LogStaticAfter(string message, float delaySeconds = 1f, bool unscaledTime = true)
        => Ins?.LogAfter(message, delaySeconds, unscaledTime);
    public static void LogStaticKeyAfter(string table, string key, float delaySeconds = 1f, bool unscaledTime = true, params object[] args)
        => Ins?.LogKeyAfter(table, key, delaySeconds, unscaledTime, args);

    // ========= HOUSEKEEPING =========
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
        while (rows.Count > 0)
        {
            var peek = rows.Peek();
            if (peek == null || !peek.activeInHierarchy) rows.Dequeue();
            else break;
        }
    }

    private static string ApplyFormatArgs(string text, object[] args)
    {
        if (string.IsNullOrEmpty(text) || args == null || args.Length == 0)
            return text;

        foreach (object arg in args)
        {
            if (arg == null)
                continue;

            foreach (var property in arg.GetType().GetProperties())
            {
                if (property.GetIndexParameters().Length > 0)
                    continue;

                string value = property.GetValue(arg, null)?.ToString() ?? string.Empty;
                text = text.Replace("{" + property.Name + "}", value);
                text = text.Replace("{" + char.ToLowerInvariant(property.Name[0]) + property.Name.Substring(1) + "}", value);
            }
        }

        try
        {
            return string.Format(text, args);
        }
        catch (System.FormatException)
        {
            return text;
        }
    }
}

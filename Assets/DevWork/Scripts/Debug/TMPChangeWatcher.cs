using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class TMPChangeWatcher : MonoBehaviour
{
    [Header("Scan")]
    [SerializeField] private bool includeInactive = true;
    [SerializeField] private bool watchText = true;
    [SerializeField] private bool watchRectSize = true;

    [Header("Timing")]
    [SerializeField] private float sampleInterval = 0.5f;
    [SerializeField] private float reportInterval = 3f;
    [SerializeField] private int topN = 10;

    private readonly List<Entry> entries = new List<Entry>(256);
    private float nextSampleTime;
    private float nextReportTime;

    private struct Entry
    {
        public TMP_Text tmp;
        public string lastText;
        public Vector2 lastSize;
        public int textChanges;
        public int sizeChanges;
    }

    private void Awake()
    {
        Scan();
        nextSampleTime = Time.unscaledTime + Mathf.Max(0.05f, sampleInterval);
        nextReportTime = Time.unscaledTime + Mathf.Max(0.2f, reportInterval);
    }

    [ContextMenu("Scan Now")]
    public void Scan()
    {
        entries.Clear();
        var tmps = FindObjectsOfType<TMP_Text>(includeInactive);
        for (int i = 0; i < tmps.Length; i++)
        {
            var t = tmps[i];
            var e = new Entry
            {
                tmp = t,
                lastText = t.text,
                lastSize = t.rectTransform.rect.size,
                textChanges = 0,
                sizeChanges = 0
            };
            entries.Add(e);
        }
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        if (now >= nextSampleTime)
        {
            Sample();
            nextSampleTime = now + Mathf.Max(0.05f, sampleInterval);
        }

        if (now >= nextReportTime)
        {
            Report();
            nextReportTime = now + Mathf.Max(0.2f, reportInterval);
        }
    }

    private void Sample()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.tmp == null)
                continue;

            if (watchText)
            {
                var txt = e.tmp.text;
                if (!string.Equals(txt, e.lastText))
                {
                    e.textChanges++;
                    e.lastText = txt;
                }
            }

            if (watchRectSize)
            {
                var size = e.tmp.rectTransform.rect.size;
                if (size != e.lastSize)
                {
                    e.sizeChanges++;
                    e.lastSize = size;
                }
            }

            entries[i] = e;
        }
    }

    private void Report()
    {
        if (entries.Count == 0)
            return;

        entries.Sort((a, b) =>
        {
            int ca = a.textChanges + a.sizeChanges;
            int cb = b.textChanges + b.sizeChanges;
            return cb.CompareTo(ca);
        });

        int count = Mathf.Min(topN, entries.Count);
        var sb = new StringBuilder(512);
        sb.AppendLine("[TMPChangeWatcher] Top changing TMPs:");
        int shown = 0;

        for (int i = 0; i < entries.Count && shown < count; i++)
        {
            var e = entries[i];
            if (e.tmp == null) continue;

            int total = e.textChanges + e.sizeChanges;
            if (total == 0) continue;

            sb.Append("#").Append(shown + 1).Append(" ");
            sb.Append(GetPath(e.tmp.transform));
            sb.Append(" | text: ").Append(e.textChanges);
            sb.Append(" | size: ").Append(e.sizeChanges);
            sb.AppendLine();

            shown++;
        }

        if (shown > 0)
            Debug.Log(sb.ToString(), this);
    }

    private static string GetPath(Transform t)
    {
        if (t == null) return "<null>";
        var path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}

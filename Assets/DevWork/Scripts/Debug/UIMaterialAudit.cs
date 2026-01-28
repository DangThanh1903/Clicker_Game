using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UIMaterialAudit : MonoBehaviour
{
    [SerializeField] private Transform root;
    [SerializeField] private bool includeInactive = true;
    [SerializeField] private bool onlyIfDifferent = true;
    [SerializeField] private bool logEvenIfSameMaterial = false;
    [SerializeField] private int maxLogs = 50;
    [SerializeField] private bool runOnStart = false;
    [SerializeField] private float startDelaySeconds = 0f;
    [SerializeField] private bool scanAllCanvases = false;
    [SerializeField] private bool logSummary = true;
    [SerializeField] private bool onlyImages = false;
    [SerializeField] private bool onlyTMP = false;
    [SerializeField] private bool logSpriteTexture = true;
    [SerializeField] private string pathContains = "";
    [SerializeField] private bool logUniqueMaterialSummary = true;
    [SerializeField] private bool logUniqueRenderMaterials = false;
    [SerializeField] private bool logUniqueActualMaterials = false;
    [SerializeField] private bool logUniqueDefaultMaterials = false;

    private void Start()
    {
        if (runOnStart)
            StartCoroutine(AuditAfterDelay());
    }

    private System.Collections.IEnumerator AuditAfterDelay()
    {
        if (startDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(startDelaySeconds);
        Audit();
    }

    [ContextMenu("Audit Materials")]
    public void Audit()
    {
        if (root == null)
            root = transform;

        Graphic[] graphics = scanAllCanvases
            ? FindObjectsOfType<Graphic>(includeInactive)
            : root.GetComponentsInChildren<Graphic>(includeInactive);

        int total = graphics.Length;
        int logged = 0;
        int diffCount = 0;
        var uniqueRenderMats = new Dictionary<int, Material>();
        var uniqueActualMats = new Dictionary<int, Material>();
        var uniqueDefaultMats = new Dictionary<int, Material>();
        var uniqueRenderMatPaths = new Dictionary<int, string>();
        var uniqueActualMatPaths = new Dictionary<int, string>();
        var uniqueDefaultMatPaths = new Dictionary<int, string>();

        foreach (var g in graphics)
        {
            if (g == null) continue;

            string path = GetPath(g.transform);
            if (!string.IsNullOrEmpty(pathContains) && !path.Contains(pathContains))
                continue;

            if (onlyImages && g is not Image)
                continue;
            if (onlyTMP && g is not TMP_Text)
                continue;

            var defaultMat = g.defaultMaterial;
            var renderMat = g.materialForRendering;
            var renderer = g.canvasRenderer;
            var actualMat = renderer != null ? renderer.GetMaterial() : null;
            bool diff = (renderMat != defaultMat) || (actualMat != null && actualMat != defaultMat);

            if (renderMat != null && !uniqueRenderMats.ContainsKey(renderMat.GetInstanceID()))
            {
                uniqueRenderMats[renderMat.GetInstanceID()] = renderMat;
                uniqueRenderMatPaths[renderMat.GetInstanceID()] = path;
            }
            if (actualMat != null && !uniqueActualMats.ContainsKey(actualMat.GetInstanceID()))
            {
                uniqueActualMats[actualMat.GetInstanceID()] = actualMat;
                uniqueActualMatPaths[actualMat.GetInstanceID()] = path;
            }
            if (defaultMat != null && !uniqueDefaultMats.ContainsKey(defaultMat.GetInstanceID()))
            {
                uniqueDefaultMats[defaultMat.GetInstanceID()] = defaultMat;
                uniqueDefaultMatPaths[defaultMat.GetInstanceID()] = path;
            }

            if (onlyIfDifferent && !diff && !logEvenIfSameMaterial)
                continue;

            diffCount++;

            var sb = new StringBuilder(256);
            sb.Append("UI Mat: ").Append(path);
            sb.Append(" | type=").Append(g.GetType().Name);
            sb.Append(" | renderMat=").Append(renderMat ? renderMat.name : "null");
            sb.Append(" | defaultMat=").Append(defaultMat ? defaultMat.name : "null");
            sb.Append(" | actualMat=").Append(actualMat ? actualMat.name : "null");
            if (actualMat != null) sb.Append(" | actualId=").Append(actualMat.GetInstanceID());
            if (renderMat != null) sb.Append(" | renderId=").Append(renderMat.GetInstanceID());
            if (defaultMat != null) sb.Append(" | defaultId=").Append(defaultMat.GetInstanceID());

            if (g is Image img && img.sprite != null)
            {
                sb.Append(" | sprite=").Append(img.sprite.name);
                if (logSpriteTexture)
                {
                    var tex = img.sprite.texture;
                    sb.Append(" | tex=").Append(tex ? tex.name : "null");
                    if (tex != null) sb.Append(" | texId=").Append(tex.GetInstanceID());
                }
            }
            if (g is TMP_Text tmp && tmp.font != null)
                sb.Append(" | font=").Append(tmp.font.name);

            if (g.GetComponentInParent<Mask>() != null)
                sb.Append(" | parentMask=Mask");
            if (g.GetComponentInParent<RectMask2D>() != null)
                sb.Append(" | parentMask=RectMask2D");

            Debug.Log(sb.ToString(), g);
            logged++;
            if (logged >= maxLogs) break;
        }

        if (logSummary)
        {
            string scope = scanAllCanvases ? "ALL" : GetPath(root);
            Debug.Log($"[UIMaterialAudit] scope={scope} totalGraphics={total} diffCount={diffCount} logged={logged} onlyIfDifferent={onlyIfDifferent} logEvenIfSameMaterial={logEvenIfSameMaterial} onlyImages={onlyImages} onlyTMP={onlyTMP} pathContains={pathContains}");
        }

        if (logUniqueMaterialSummary)
        {
            Debug.Log($"[UIMaterialAudit] unique renderMat count={uniqueRenderMats.Count}, actualMat count={uniqueActualMats.Count}, defaultMat count={uniqueDefaultMats.Count}");
        }

        if (logUniqueRenderMaterials)
            LogUniqueMaterials("renderMat", uniqueRenderMats, uniqueRenderMatPaths);
        if (logUniqueActualMaterials)
            LogUniqueMaterials("actualMat", uniqueActualMats, uniqueActualMatPaths);
        if (logUniqueDefaultMaterials)
            LogUniqueMaterials("defaultMat", uniqueDefaultMats, uniqueDefaultMatPaths);
    }

    private static void LogUniqueMaterials(string label, Dictionary<int, Material> mats, Dictionary<int, string> paths)
    {
        foreach (var kvp in mats)
        {
            var mat = kvp.Value;
            var name = mat != null ? mat.name : "null";
            paths.TryGetValue(kvp.Key, out var path);
            Debug.Log($"[UIMaterialAudit] unique {label} id={kvp.Key} name={name} firstSeen={path}");
        }
    }

    private static string GetPath(Transform t)
    {
        if (t == null) return "<null>";
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}

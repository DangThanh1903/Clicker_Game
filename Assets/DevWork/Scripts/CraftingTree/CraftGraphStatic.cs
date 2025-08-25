using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CraftGraphStatic : MonoBehaviour
{
    [Header("References")]
    public CraftNodeManager nodeManager;   // your manager that holds allNodes
    public RectTransform container;        // parent RectTransform that holds all CraftNode UI objects
    public Sprite lineSprite;              // 1x1 white sprite (use default UI sprite if null)

    [Header("Style")]
    public float thickness = 3f;
    public Color color = Color.white;

    void Start()
    {
        // wait a frame so layout/anchors are resolved
        StartCoroutine(BuildAfterLayout());
    }

    IEnumerator BuildAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        Build();
    }

    void Build()
    {
        foreach (var node in nodeManager.allNodes)
        {
            if (!node) continue;
            var from = (RectTransform)node.transform;

            foreach (var req in node.requiredNodes)
            {
                if (!req) continue;
                var to = (RectTransform)req.transform;
                CreateLine(from, to);
            }
        }
    }

    void CreateLine(RectTransform from, RectTransform to)
    {
        // create a UI image under the same container so coordinates match
        var go = new GameObject($"Line_{from.name}_to_{to.name}",
            typeof(RectTransform), typeof(Image));
        var lineRT = (RectTransform)go.transform;
        lineRT.SetParent(container, false);
        lineRT.pivot = new Vector2(0f, 0.5f);     // start at 'from', extend toward 'to'
        lineRT.SetAsFirstSibling();               // render behind nodes

        var img = go.GetComponent<Image>();
        img.sprite = lineSprite;
        img.color = color;
        img.raycastTarget = false;

        Vector2 a = WorldCenterToLocal(from);
        Vector2 b = WorldCenterToLocal(to);

        Vector2 dir = b - a;
        float len = dir.magnitude;
        float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        lineRT.anchoredPosition = a;
        lineRT.sizeDelta = new Vector2(len, thickness);
        lineRT.localRotation = Quaternion.Euler(0, 0, angleDeg);
    }

    Vector2 WorldCenterToLocal(RectTransform t)
    {
        Vector3 world = t.TransformPoint(t.rect.center);
        return container.InverseTransformPoint(world);
    }
}

using DG.Tweening;
using Lean.Pool;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class BlockFragment : MonoBehaviour
{
    [Header("Scatter (horizontal)")]
    [SerializeField] Vector2 scatterRadius = new Vector2(0.6f, 2.5f); // increase max for bigger scatter

    [Header("Drop (vertical)")]
    [SerializeField] float maxDrop = 1.6f;          // lower this to reduce how far it drops down
    [SerializeField] Vector2 extraDropRange = new(0.5f, 2f);

    [Header("Arc & Timing")]
    [SerializeField] float baseArc = 0.5f;
    [SerializeField] float arcPerMeter = 0.5f;
    [SerializeField] float minDuration = 0.45f;
    [SerializeField] float maxDuration = 0.75f;
    [SerializeField] float startJitter = 0.12f;

    [Header("Ground snap (optional)")]
    [SerializeField] bool snapToGround = true;
    [SerializeField] LayerMask groundMask = ~0;
    [SerializeField] float rayStartUp = 1.5f;
    [SerializeField] float rayDownDist = 6f;
    [SerializeField] float groundOffsetY = 0.02f;

    Renderer rend;
    MaterialPropertyBlock mpb;

    Vector3 startPoint, controlPoint, endPoint;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        AnimateWithCurve();
    }

    // Call this after spawn to set atlas tile (if needed)
    public void SetupTile(Texture atlas, int cols, int rows, Vector2Int tile, bool flipY)
    {
        if (!rend) return;
        float sx = 1f / Mathf.Max(1, cols);
        float sy = 1f / Mathf.Max(1, rows);
        int ty = flipY ? (rows - 1 - tile.y) : tile.y;
        Vector4 st = new Vector4(sx, sy, tile.x * sx, ty * sy);

        rend.GetPropertyBlock(mpb);
        mpb.SetTexture("_MainTex", atlas);  mpb.SetVector("_MainTex_ST", st);
        mpb.SetTexture("_BaseMap", atlas);  mpb.SetVector("_BaseMap_ST", st);
        rend.SetPropertyBlock(mpb);
    }

    public void AnimateWithCurve()
    {
        startPoint = transform.position + Random.insideUnitSphere * startJitter;

        // ---- Bigger scatter: random distance in [min,max] (sqrt bias → more outward)
        float rMin = Mathf.Max(0f, scatterRadius.x);
        float rMax = Mathf.Max(rMin, scatterRadius.y);
        float u = Mathf.Sqrt(Random.value);
        float dist = Mathf.Lerp(rMin, rMax, u);

        Vector2 dir2 = Random.insideUnitCircle.normalized;
        Vector3 horiz = new Vector3(dir2.x, 0f, dir2.y) * dist;
        Vector3 provisionalEnd = startPoint + horiz;

        // ---- Ground snap with vertical clamp (lower drop)
        endPoint = provisionalEnd;
        float targetY = provisionalEnd.y;

        if (snapToGround)
        {
            Vector3 rayOrigin = provisionalEnd + Vector3.up * rayStartUp;
            if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, rayDownDist + rayStartUp, groundMask, QueryTriggerInteraction.Ignore))
                targetY = hit.point.y + groundOffsetY;
        }

        // Limit how far below the start we can land
        float minY = startPoint.y - Mathf.Max(0f, maxDrop);   // don’t go below this
        endPoint.y = (snapToGround ? targetY : provisionalEnd.y)
             - Random.Range(extraDropRange.x, extraDropRange.y);

        // ---- Arc height & duration scale with travel distance
        float arcHeight = baseArc + arcPerMeter * Vector3.Distance(startPoint, endPoint);
        controlPoint = (startPoint + endPoint) * 0.5f + Vector3.up * arcHeight;

        float tDur = Mathf.Lerp(minDuration, maxDuration, Mathf.InverseLerp(rMin, rMax, dist));

        // ---- Move along bezier
        float t = 0f;
        DOTween.To(() => t, x => t = x, 1f, tDur)
            .SetEase(Ease.Linear)
            .OnUpdate(() => transform.position = Bezier(t, startPoint, controlPoint, endPoint))
            .OnComplete(() => LeanPool.Despawn(gameObject));

        // random spin
        transform.DORotate(new Vector3(Random.Range(-180, 180), Random.Range(-180, 180), Random.Range(-180, 180)),
                           tDur, RotateMode.Fast);
    }

    static Vector3 Bezier(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }
}

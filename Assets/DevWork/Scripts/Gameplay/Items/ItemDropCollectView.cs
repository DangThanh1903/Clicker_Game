using System;
using DG.Tweening;
using Lean.Pool;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Item3DView))]
public class ItemDropCollectView : MonoBehaviour
{
    [SerializeField] private Item3DView item3DView;
    [SerializeField] private SpriteRenderer fallbackSpriteRenderer;

    private Sequence sequence;
    private Vector3 baseScale = Vector3.one;

    private void Awake()
    {
        CacheComponents();
        baseScale = transform.localScale;
    }

    private void OnDisable()
    {
        sequence?.Kill();
        sequence = null;
        transform.localScale = baseScale;
    }

    public void Bind(Item item)
    {
        CacheComponents();

        if (item != null && item.TryGetWorldVisual(out Mesh mesh, out Material frontMaterial, out Material sideMaterial))
        {
            if (fallbackSpriteRenderer != null)
                fallbackSpriteRenderer.enabled = false;

            item3DView.MeshRenderer.enabled = true;
            item3DView.SetVisual(mesh, frontMaterial, sideMaterial);
            return;
        }

        item3DView.ClearVisual();
        item3DView.MeshRenderer.enabled = false;
        EnsureFallbackSpriteRenderer();
        fallbackSpriteRenderer.sprite = item != null ? item.icon : null;
        fallbackSpriteRenderer.enabled = fallbackSpriteRenderer.sprite != null;
    }

    public Tween PlayCollect(
        Vector3 startPosition,
        Vector3 dropApexPosition,
        Vector3 groundPosition,
        Vector3 collectApexPosition,
        Vector3 flyTargetPosition,
        Quaternion displayRotation,
        float visualScale,
        float dropDuration,
        float settleSpinDuration,
        float flyDuration,
        Ease dropEase,
        Ease flyEase,
        bool useUnscaledTime,
        Action onCompleted = null)
    {
        sequence?.Kill();

        float safeScale = Mathf.Max(0.01f, visualScale);
        transform.position = startPosition;
        transform.rotation = displayRotation;
        transform.localScale = Vector3.one * safeScale * 0.65f;

        float safeDropDuration = Mathf.Max(0.01f, dropDuration);
        float safeFlyDuration = Mathf.Max(0.01f, flyDuration);

        sequence = DOTween.Sequence()
            .SetLink(gameObject)
            .SetUpdate(useUnscaledTime);

        sequence.Append(DOTween.To(
                () => 0f,
                t => transform.position = QuadraticBezier(startPosition, dropApexPosition, groundPosition, t),
                1f,
                safeDropDuration)
            .SetEase(dropEase));
        sequence.Join(transform.DOScale(Vector3.one * safeScale, safeDropDuration).SetEase(Ease.OutBack));
        sequence.Join(transform
            .DORotate(new Vector3(0f, 180f, 0f), safeDropDuration, RotateMode.WorldAxisAdd)
            .SetEase(Ease.Linear));

        if (settleSpinDuration > 0f)
        {
            sequence.Append(transform
                .DORotate(new Vector3(0f, 90f, 0f), settleSpinDuration, RotateMode.WorldAxisAdd)
                .SetEase(Ease.Linear));
        }

        sequence.Append(DOTween.To(
                () => 0f,
                t => transform.position = QuadraticBezier(groundPosition, collectApexPosition, flyTargetPosition, t),
                1f,
                safeFlyDuration)
            .SetEase(flyEase));
        sequence.Join(transform.DOScale(Vector3.one * safeScale * 0.32f, safeFlyDuration).SetEase(Ease.InQuad));
        sequence.Join(transform
            .DORotate(new Vector3(0f, 540f, 0f), safeFlyDuration, RotateMode.WorldAxisAdd)
            .SetEase(Ease.Linear));
        sequence.OnComplete(() =>
        {
            onCompleted?.Invoke();
            LeanPool.Despawn(gameObject);
        });

        return sequence;
    }

    public void ResetVisual()
    {
        sequence?.Kill();
        sequence = null;

        if (item3DView != null)
        {
            item3DView.ClearVisual();
            item3DView.MeshRenderer.enabled = true;
        }

        if (fallbackSpriteRenderer != null)
        {
            fallbackSpriteRenderer.sprite = null;
            fallbackSpriteRenderer.enabled = false;
        }
    }

    private void CacheComponents()
    {
        if (item3DView == null)
            item3DView = GetComponent<Item3DView>();
    }

    private void EnsureFallbackSpriteRenderer()
    {
        if (fallbackSpriteRenderer != null)
            return;

        var child = new GameObject("SpriteFallback");
        child.transform.SetParent(transform, false);
        fallbackSpriteRenderer = child.AddComponent<SpriteRenderer>();
        fallbackSpriteRenderer.enabled = false;
        fallbackSpriteRenderer.sortingOrder = 20;
    }

    private static Vector3 QuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
    {
        float inverse = 1f - t;
        return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
    }
}

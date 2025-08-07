using UnityEngine;
using DG.Tweening;
using Lean.Pool;

public class BlockFragment : MonoBehaviour
{
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private float arcHeight = 1f;
    [SerializeField] private float radius = 1f;

    private Vector3 startPoint;
    private Vector3 controlPoint;
    private Vector3 endPoint;
    void OnEnable()
    {
        AnimateWithCurve();
    }

    public void AnimateWithCurve()
    {
        startPoint = transform.position;

        // Random horizontal direction
        Vector2 randomXZ = Random.insideUnitCircle.normalized * radius;
        endPoint = startPoint + new Vector3(randomXZ.x, 0f, randomXZ.y); // end slightly downward

        // Control point: mid-way between start and end, but raised up (for the arc)
        controlPoint = (startPoint + endPoint) / 2f + Vector3.up * arcHeight;

        // Animate along curve using DOTween's OnUpdate
        float t = 0f;
        DOTween.To(() => t, x => t = x, 1f, duration)
            .SetEase(Ease.Linear)
            .OnUpdate(() =>
            {
                transform.position = CalculateQuadraticBezierPoint(t, startPoint, controlPoint, endPoint);
            })
            .OnComplete(() =>
            {
                LeanPool.Despawn(gameObject);
            });

        // Optional: rotate fragment randomly while flying
        transform.DORotate(new Vector3(
            Random.Range(-180, 180),
            Random.Range(-180, 180),
            Random.Range(-180, 180)
        ), duration, RotateMode.Fast);
    }

    Vector3 CalculateQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        // B(t) = (1 - t)^2 * p0 + 2(1 - t)t * p1 + t^2 * p2
        return Mathf.Pow(1 - t, 2) * p0
             + 2 * (1 - t) * t * p1
             + Mathf.Pow(t, 2) * p2;
    }
}

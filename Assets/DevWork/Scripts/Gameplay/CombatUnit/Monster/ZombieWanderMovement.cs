using UnityEngine;
using DG.Tweening;

public class ZombieWanderMovement : MonoBehaviour, IMonsterMovement
{
    [SerializeField] private float radius = 0.6f;
    [SerializeField] private float speed = 0.4f;
    [SerializeField] private float minTargetTime = 0.6f;
    [SerializeField] private float maxTargetTime = 1.6f;

    private Vector3 currentOffset;
    private Tween tween;

    void OnEnable()
    {
        currentOffset = Vector3.zero;
        StartWander();
    }

    void OnDisable()
    {
        StopWander();
        currentOffset = Vector3.zero;
    }

    public Vector3 MoveUpdate(float deltaTime)
    {
        return currentOffset;
    }

    void StartWander()
    {
        StopWander();
        if (!isActiveAndEnabled) return;

        float moveSpeed = Mathf.Abs(speed);
        float roamRadius = Mathf.Abs(radius);
        if (moveSpeed <= 0f || roamRadius <= 0f)
        {
            currentOffset = Vector3.zero;
            return;
        }

        Vector2 offset = Random.insideUnitCircle * roamRadius;
        Vector3 targetOffset = new Vector3(offset.x, 0f, offset.y);

        float distance = Vector3.Distance(currentOffset, targetOffset);
        float duration = distance / Mathf.Max(0.0001f, moveSpeed);
        float minTime = Mathf.Max(0f, Mathf.Min(minTargetTime, maxTargetTime));
        float maxTime = Mathf.Max(minTime, Mathf.Max(minTargetTime, maxTargetTime));
        duration = Mathf.Clamp(duration, minTime, maxTime);
        if (duration <= 0f) duration = 0.01f;

        tween = DOTween.To(() => currentOffset, x => currentOffset = x, targetOffset, duration)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                if (!isActiveAndEnabled) return;
                StartWander();
            });
    }

    void StopWander()
    {
        tween?.Kill();
        tween = null;
    }
}

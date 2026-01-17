using UnityEngine;
using DG.Tweening;

public class SkeletonStrafeMovement : MonoBehaviour, IMonsterMovement
{
    [SerializeField] private float distance = 0.6f;
    [SerializeField] private float speed = 2f;
    [SerializeField] private Vector3 axis = Vector3.right;

    private Vector3 currentOffset;
    private float phase;
    private float phaseOffset;
    private Tween tween;

    void OnEnable()
    {
        phase = 0f;
        phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        StartTween();
    }

    void OnDisable()
    {
        StopTween();
        currentOffset = Vector3.zero;
    }

    public Vector3 MoveUpdate(float deltaTime)
    {
        return currentOffset;
    }

    void StartTween()
    {
        StopTween();

        float angular = Mathf.Abs(speed);
        float dist = Mathf.Abs(distance);
        if (angular <= 0f || dist <= 0f)
        {
            currentOffset = Vector3.zero;
            return;
        }

        Vector3 dir = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.right;
        float period = (Mathf.PI * 2f) / Mathf.Max(0.0001f, angular);

        tween = DOTween.To(
                () => phase,
                x =>
                {
                    phase = x;
                    currentOffset = dir * (Mathf.Sin(phase + phaseOffset) * dist);
                },
                Mathf.PI * 2f,
                period)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart);
    }

    void StopTween()
    {
        tween?.Kill();
        tween = null;
    }
}

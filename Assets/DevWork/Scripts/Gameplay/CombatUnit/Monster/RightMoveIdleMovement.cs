using UnityEngine;
using DG.Tweening;

public class RightMoveIdleMovement : MonoBehaviour, IMonsterMovement
{
    [SerializeField] private float speed = 0.5f;
    [SerializeField] private float moveDuration = 1.2f;
    [SerializeField] private int jumpsPerDirection = 2;
    [SerializeField] private float jumpHeight = 0.2f;
    [SerializeField] private float idleDuration = 0.6f;
    [SerializeField] private float idleBobAmplitude = 0.04f;
    [SerializeField] private float idleBobPeriod = 0.4f;
    [SerializeField] private Vector3 axis = Vector3.right;
    [SerializeField] private Vector3 jumpAxis = Vector3.up;
    [SerializeField] private Vector3 idleBobAxis = Vector3.up;

    private Vector3 moveOffset;
    private Vector3 idleOffset;
    private Sequence sequence;

    void OnEnable()
    {
        moveOffset = Vector3.zero;
        idleOffset = Vector3.zero;
        StartCycle();
    }

    void OnDisable()
    {
        StopTweens();
        moveOffset = Vector3.zero;
        idleOffset = Vector3.zero;
    }

    public Vector3 MoveUpdate(float deltaTime)
    {
        return moveOffset + idleOffset;
    }

    void StartCycle()
    {
        StopTweens();
        if (!isActiveAndEnabled) return;

        float moveTime = Mathf.Max(0f, moveDuration);
        float moveSpeed = Mathf.Abs(speed);
        int hops = Mathf.Max(1, jumpsPerDirection);
        Vector3 dir = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.right;
        Vector3 jumpDir = jumpAxis.sqrMagnitude > 0.0001f ? jumpAxis.normalized : Vector3.up;

        if (moveTime <= 0f || Mathf.Approximately(moveSpeed, 0f))
            return;

        moveOffset = Vector3.zero;
        idleOffset = Vector3.zero;

        sequence = DOTween.Sequence();
        float distance = moveSpeed * moveTime;
        Vector3 step = dir * distance;
        Vector3 start = Vector3.zero;

        for (int i = 0; i < hops; i++)
        {
            Vector3 end = start + step;
            sequence.AppendCallback(() => idleOffset = Vector3.zero);
            AppendJump(sequence, start, end, moveTime, jumpDir);
            AppendIdle(sequence);
            start = end;
        }

        for (int i = 0; i < hops; i++)
        {
            Vector3 end = start - step;
            sequence.AppendCallback(() => idleOffset = Vector3.zero);
            AppendJump(sequence, start, end, moveTime, jumpDir);
            AppendIdle(sequence);
            start = end;
        }

        sequence.SetLoops(-1, LoopType.Restart);
    }

    void StopTweens()
    {
        sequence?.Kill();
        sequence = null;
        idleOffset = Vector3.zero;
    }

    void AppendIdle(Sequence seq)
    {
        float idleTime = Mathf.Max(0f, idleDuration);
        if (idleTime <= 0f)
            return;

        float bobAmp = Mathf.Max(0f, idleBobAmplitude);
        float bobPeriod = Mathf.Max(0f, idleBobPeriod);
        if (bobAmp <= 0f || bobPeriod <= 0f)
        {
            seq.AppendCallback(() => idleOffset = Vector3.zero);
            seq.AppendInterval(idleTime);
            return;
        }

        Vector3 bobDir = idleBobAxis.sqrMagnitude > 0.0001f ? idleBobAxis.normalized : Vector3.up;
        int loops = Mathf.Max(1, Mathf.RoundToInt(idleTime / bobPeriod));
        seq.Append(
            DOVirtual.Float(0f, Mathf.PI * 2f, bobPeriod,
                    phase => idleOffset = bobDir * (Mathf.Sin(phase) * bobAmp))
                .SetEase(Ease.Linear)
                .SetLoops(loops, LoopType.Restart));
        seq.AppendCallback(() => idleOffset = Vector3.zero);
    }

    void AppendJump(Sequence seq, Vector3 start, Vector3 end, float duration, Vector3 jumpDir)
    {
        float height = Mathf.Max(0f, jumpHeight);
        seq.AppendCallback(() => moveOffset = start);
        seq.Append(
            DOVirtual.Float(0f, 1f, duration, t =>
            {
                Vector3 horizontal = Vector3.Lerp(start, end, t);
                float y = Mathf.Sin(t * Mathf.PI) * height;
                moveOffset = horizontal + jumpDir * y;
            }).SetEase(Ease.Linear));
    }
}

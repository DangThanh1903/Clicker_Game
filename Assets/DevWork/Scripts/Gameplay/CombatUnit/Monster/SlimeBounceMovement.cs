using UnityEngine;
using DG.Tweening;

public class SlimeBounceMovement : MonoBehaviour, IMonsterMovement
{
    [SerializeField] private float amplitude = 0.2f;
    [SerializeField] private float frequency = 2f;
    [SerializeField] private float jumpUpTime = 0.12f;
    [SerializeField] private float jumpDownTime = 0.12f;
    [SerializeField] private float idleDuration = 0.8f;
    [SerializeField] private Vector3 axis = Vector3.up;

    private Vector3 currentOffset;
    private Sequence sequence;

    void OnEnable()
    {
        currentOffset = Vector3.zero;
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

        float amp = Mathf.Abs(amplitude);
        if (amp <= 0f)
        {
            currentOffset = Vector3.zero;
            return;
        }

        Vector3 dir = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;
        float upTime = jumpUpTime;
        float downTime = jumpDownTime;
        if (upTime <= 0f || downTime <= 0f)
        {
            float angular = Mathf.Abs(frequency);
            if (angular <= 0f)
            {
                currentOffset = Vector3.zero;
                return;
            }
            float period = (Mathf.PI * 2f) / Mathf.Max(0.0001f, angular);
            upTime = period * 0.5f;
            downTime = period * 0.5f;
        }

        sequence = DOTween.Sequence();
        sequence.Append(
            DOVirtual.Float(0f, amp, upTime, v => currentOffset = dir * v)
                .SetEase(Ease.OutQuad));
        sequence.Append(
            DOVirtual.Float(amp, 0f, downTime, v => currentOffset = dir * v)
                .SetEase(Ease.InQuad));

        float idleTime = Mathf.Max(0f, idleDuration);
        if (idleTime > 0f)
        {
            sequence.AppendCallback(() => currentOffset = Vector3.zero);
            sequence.AppendInterval(idleTime);
        }

        sequence.SetLoops(-1, LoopType.Restart);
    }

    void StopTween()
    {
        sequence?.Kill();
        sequence = null;
    }
}

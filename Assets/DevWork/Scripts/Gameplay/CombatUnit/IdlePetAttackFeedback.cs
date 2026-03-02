using UnityEngine;
using UnityEngine.Serialization;
using DG.Tweening;

public enum PetAttackVisualMode
{
    Animator,
    Dotween
}

[DisallowMultipleComponent]
public class IdlePetAttackFeedback : MonoBehaviour, IIdlePetAttackFeedback
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform lookRoot;
    [SerializeField] private Transform tweenRoot;

    [Header("Mode")]
    [SerializeField] private PetAttackVisualMode visualMode = PetAttackVisualMode.Animator;

    [Header("Animator Params")]
    [SerializeField] private string attackTriggerName = "Attack";

    [Header("Aim")]
    [SerializeField] private bool rotateTowardTarget = true;
    [FormerlySerializedAs("lockYAxis")]
    [SerializeField] private bool lockXAxis = true;

    [Header("Dotween Idle")]
    [SerializeField] private bool dotweenIdleEnabled = true;
    [SerializeField] private Vector3 dotweenIdleOffset = new Vector3(0f, 0.08f, 0f);
    [SerializeField, Min(0.05f)] private float dotweenIdleDuration = 0.6f;
    [SerializeField] private Ease dotweenIdleEase = Ease.InOutSine;

    [Header("Dotween Attack")]
    [SerializeField, Min(0f)] private float dotweenAttackDistance = 0.35f;
    [SerializeField, Min(0.01f)] private float dotweenAttackForwardDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float dotweenAttackReturnDuration = 0.12f;
    [SerializeField] private Ease dotweenAttackForwardEase = Ease.OutQuad;
    [SerializeField] private Ease dotweenAttackReturnEase = Ease.InQuad;
    [SerializeField] private bool dotweenAttackTowardTarget = true;
    [SerializeField] private bool dotweenAttackIgnoreY = true;

    private Tween idleTween;
    private Tween attackTween;
    private Vector3 baseLocalPosition;
    private bool hasBaseLocalPosition;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (lookRoot == null)
            lookRoot = transform;

        if (tweenRoot == null)
            tweenRoot = transform;

        CacheBaseLocalPosition();
    }

    void OnEnable()
    {
        CacheBaseLocalPosition();
        ApplyVisualModeState();
    }

    void OnDisable()
    {
        StopDotweenPlayback(resetPosition: true);
    }

    public void PlayIdleAttack(float _damage, Vector3 targetWorldPosition)
    {
        if (rotateTowardTarget && lookRoot != null)
        {
            Vector3 direction = targetWorldPosition - lookRoot.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                if (lockXAxis)
                {
                    Vector3 currentEuler = lookRoot.rotation.eulerAngles;
                    Vector3 targetEuler = targetRotation.eulerAngles;
                    lookRoot.rotation = Quaternion.Euler(currentEuler.x, targetEuler.y, targetEuler.z);
                }
                else
                {
                    lookRoot.rotation = targetRotation;
                }
            }
        }

        if (visualMode == PetAttackVisualMode.Dotween)
        {
            PlayDotweenAttack(targetWorldPosition);
            return;
        }

        if (animator != null && !string.IsNullOrEmpty(attackTriggerName))
            animator.SetTrigger(attackTriggerName);
    }

    private void ApplyVisualModeState()
    {
        if (visualMode == PetAttackVisualMode.Dotween)
            StartDotweenIdle();
        else
            StopDotweenPlayback(resetPosition: true);
    }

    private void CacheBaseLocalPosition()
    {
        if (tweenRoot == null)
            return;

        baseLocalPosition = tweenRoot.localPosition;
        hasBaseLocalPosition = true;
    }

    private void StartDotweenIdle()
    {
        if (tweenRoot == null || !hasBaseLocalPosition || !dotweenIdleEnabled)
            return;

        if (attackTween != null && attackTween.IsActive())
            return;

        if (idleTween != null && idleTween.IsActive())
            return;

        tweenRoot.localPosition = baseLocalPosition;
        idleTween = tweenRoot
            .DOLocalMove(baseLocalPosition + dotweenIdleOffset, Mathf.Max(0.05f, dotweenIdleDuration))
            .SetEase(dotweenIdleEase)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void PlayDotweenAttack(Vector3 targetWorldPosition)
    {
        if (tweenRoot == null || !hasBaseLocalPosition || dotweenAttackDistance <= 0f)
            return;

        if (idleTween != null)
        {
            idleTween.Kill(false);
            idleTween = null;
        }

        if (attackTween != null)
        {
            attackTween.Kill(false);
            attackTween = null;
        }

        Vector3 worldDirection = dotweenAttackTowardTarget
            ? (targetWorldPosition - tweenRoot.position)
            : tweenRoot.forward;

        if (dotweenAttackIgnoreY)
            worldDirection.y = 0f;

        if (worldDirection.sqrMagnitude <= 0.0001f)
            worldDirection = tweenRoot.forward;

        if (worldDirection.sqrMagnitude <= 0.0001f)
            worldDirection = Vector3.forward;

        worldDirection.Normalize();
        Vector3 worldDelta = worldDirection * dotweenAttackDistance;
        Vector3 localDelta = tweenRoot.parent != null
            ? tweenRoot.parent.InverseTransformVector(worldDelta)
            : worldDelta;

        Vector3 attackTargetLocal = baseLocalPosition + localDelta;
        attackTween = DOTween.Sequence()
            .Append(tweenRoot.DOLocalMove(attackTargetLocal, Mathf.Max(0.01f, dotweenAttackForwardDuration)).SetEase(dotweenAttackForwardEase))
            .Append(tweenRoot.DOLocalMove(baseLocalPosition, Mathf.Max(0.01f, dotweenAttackReturnDuration)).SetEase(dotweenAttackReturnEase))
            .OnComplete(() =>
            {
                attackTween = null;
                StartDotweenIdle();
            });
    }

    private void StopDotweenPlayback(bool resetPosition)
    {
        if (idleTween != null)
        {
            idleTween.Kill(false);
            idleTween = null;
        }

        if (attackTween != null)
        {
            attackTween.Kill(false);
            attackTween = null;
        }

        if (resetPosition && tweenRoot != null && hasBaseLocalPosition)
            tweenRoot.localPosition = baseLocalPosition;
    }
}

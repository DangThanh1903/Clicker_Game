using UnityEngine;
using UnityEngine.Serialization;
using DG.Tweening;

public enum PetAttackVisualMode
{
    Animator,
    Dotween
}

[DisallowMultipleComponent]
public class IdlePetAttackFeedback : MonoBehaviour, IPetAutoAttackFeedback
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform lookRoot;
    [SerializeField] private Transform tweenRoot;

    [Header("Mode")]
    [SerializeField] private PetAttackVisualMode visualMode = PetAttackVisualMode.Animator;

    [Header("Animator Params")]
    [SerializeField] private string attackTriggerName = "Attack";
    [SerializeField] private string specialAttackTriggerName = "SpecialAttack";
    [SerializeField, Range(0f, 1f)] private float specialAttackChance = 0.1f;

    [Header("Aim")]
    [SerializeField] private bool rotateTowardTarget = true;
    [FormerlySerializedAs("lockYAxis")]
    [SerializeField] private bool lockXAxis = true;
    [SerializeField] private bool useLookRootYawAsBase = true;

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
    [SerializeField, Range(0f, 1f)] private float dotweenSpecialAttackChance = 0.1f;
    [SerializeField, Min(1f)] private float dotweenSpecialDistanceMultiplier = 1.4f;
    [SerializeField, Min(0.1f)] private float dotweenSpecialSpinTurns = 1f;
    [SerializeField, Min(0.01f)] private float dotweenSpecialSpinDuration = 0.2f;
    [SerializeField] private Ease dotweenSpecialSpinEase = Ease.OutCubic;

    private Tween idleTween;
    private Tween attackTween;
    private Vector3 baseLocalPosition;
    private bool hasBaseLocalPosition;
    private float cachedLookRootLocalYaw;
    private bool hasCachedLookRootLocalYaw;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (lookRoot == null)
            lookRoot = transform;

        if (tweenRoot == null)
            tweenRoot = transform;

        CacheBaseLocalPosition();
        CacheLookYawBase();
    }

    void OnEnable()
    {
        CacheBaseLocalPosition();
        CacheLookYawBase();
        ApplyVisualModeState();
    }

    void OnDisable()
    {
        StopDotweenPlayback(resetPosition: true);
    }

    public void PlayAutoAttack(float _damage, Vector3 targetWorldPosition)
    {
        if (rotateTowardTarget && lookRoot != null)
        {
            Vector3 direction = targetWorldPosition - lookRoot.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                float yawBase = GetResolvedLookYawBase();
                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(0f, yawBase, 0f);
                if (lockXAxis)
                {
                    Vector3 currentEuler = lookRoot.rotation.eulerAngles;
                    Vector3 targetEuler = targetRotation.eulerAngles;
                    lookRoot.rotation = Quaternion.Euler(currentEuler.x, targetEuler.y, currentEuler.z);
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

        if (animator == null)
            return;

        if (ShouldPlaySpecialAttack())
        {
            animator.SetTrigger(specialAttackTriggerName);
            return;
        }

        if (!string.IsNullOrEmpty(attackTriggerName))
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

    private void CacheLookYawBase()
    {
        if (lookRoot == null)
        {
            hasCachedLookRootLocalYaw = false;
            return;
        }

        cachedLookRootLocalYaw = lookRoot.localEulerAngles.y;
        hasCachedLookRootLocalYaw = true;
    }

    public void RefreshLookYawBaseFromCurrentPose()
    {
        CacheLookYawBase();
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

        bool useSpecialSpin = ShouldPlayDotweenSpecialAttack();
        float distance = dotweenAttackDistance;
        if (useSpecialSpin)
            distance *= Mathf.Max(1f, dotweenSpecialDistanceMultiplier);

        worldDirection.Normalize();
        Vector3 worldDelta = worldDirection * distance;
        Vector3 localDelta = tweenRoot.parent != null
            ? tweenRoot.parent.InverseTransformVector(worldDelta)
            : worldDelta;

        Vector3 attackTargetLocal = baseLocalPosition + localDelta;
        Sequence sequence = DOTween.Sequence();

        Tween moveForward = tweenRoot
            .DOLocalMove(attackTargetLocal, Mathf.Max(0.01f, dotweenAttackForwardDuration))
            .SetEase(dotweenAttackForwardEase);

        sequence.Append(moveForward);

        if (useSpecialSpin)
        {
            Vector3 euler = tweenRoot.localEulerAngles;
            Vector3 spinTarget = euler + new Vector3(0f, 360f * Mathf.Max(1f, dotweenSpecialSpinTurns), 0f);
            Tween spin = tweenRoot
                .DOLocalRotate(spinTarget, Mathf.Max(0.01f, dotweenSpecialSpinDuration), RotateMode.FastBeyond360)
                .SetEase(dotweenSpecialSpinEase);
            sequence.Join(spin);
        }

        sequence
            .Append(tweenRoot.DOLocalMove(baseLocalPosition, Mathf.Max(0.01f, dotweenAttackReturnDuration)).SetEase(dotweenAttackReturnEase))
            .OnComplete(() =>
            {
                attackTween = null;
                StartDotweenIdle();
            });

        attackTween = sequence;
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

    private bool ShouldPlaySpecialAttack()
    {
        if (string.IsNullOrEmpty(specialAttackTriggerName))
            return false;
        if (specialAttackChance <= 0f)
            return false;

        return Random.value < specialAttackChance;
    }

    private bool ShouldPlayDotweenSpecialAttack()
    {
        if (dotweenSpecialAttackChance <= 0f)
            return false;

        return Random.value < dotweenSpecialAttackChance;
    }

    private float GetResolvedLookYawBase()
    {
        float baseYaw = 0f;
        if (useLookRootYawAsBase && hasCachedLookRootLocalYaw)
            baseYaw = cachedLookRootLocalYaw;

        return baseYaw;
    }
}

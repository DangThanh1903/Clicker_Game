using System;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InventoryChestCameraFocus : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform chestLookTarget;
    [SerializeField] private Transform chestViewAnchor;

    [Header("Focus")]
    [SerializeField, Min(0.01f)] private float focusDuration = 0.3f;
    [SerializeField] private Ease focusEase = Ease.OutCubic;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool rotateToChestTarget = true;

    [Header("Restore")]
    [SerializeField] private bool restoreOnExit = true;
    [SerializeField, Min(0.01f)] private float restoreDuration = 0.25f;
    [SerializeField] private Ease restoreEase = Ease.OutCubic;

    private Tween activeTween;
    private Vector3 cachedGameplayPos;
    private Quaternion cachedGameplayRot;
    private bool hasCachedGameplayPose;

    public bool CanFocus => ResolveCameraTransform() != null && (chestViewAnchor != null || chestLookTarget != null);
    public bool RestoreOnExit => restoreOnExit;

    private void OnDisable()
    {
        if (activeTween != null)
        {
            activeTween.Kill(false);
            activeTween = null;
        }
    }

    public void PlayFocusThen(Action onComplete)
    {
        Transform cam = ResolveCameraTransform();
        if (cam == null || (chestViewAnchor == null && chestLookTarget == null))
        {
            onComplete?.Invoke();
            return;
        }

        CacheGameplayPose(cam);
        KillActiveTween();

        Sequence sequence = DOTween.Sequence().SetUpdate(useUnscaledTime);
        if (chestViewAnchor != null)
        {
            sequence.Join(cam.DOMove(chestViewAnchor.position, focusDuration).SetEase(focusEase));
            sequence.Join(cam.DORotateQuaternion(chestViewAnchor.rotation, focusDuration).SetEase(focusEase));
        }
        else if (rotateToChestTarget && chestLookTarget != null)
        {
            sequence.Join(cam.DOLookAt(chestLookTarget.position, focusDuration).SetEase(focusEase));
        }

        if (rotateToChestTarget && chestLookTarget != null && chestViewAnchor != null)
            sequence.Join(cam.DOLookAt(chestLookTarget.position, focusDuration).SetEase(focusEase));

        activeTween = sequence.OnComplete(() =>
        {
            activeTween = null;
            onComplete?.Invoke();
        });
    }

    public void RestoreGameplayView()
    {
        if (!restoreOnExit || !hasCachedGameplayPose)
            return;

        Transform cam = ResolveCameraTransform();
        if (cam == null)
            return;

        KillActiveTween();

        Sequence sequence = DOTween.Sequence().SetUpdate(useUnscaledTime);
        sequence.Join(cam.DOMove(cachedGameplayPos, restoreDuration).SetEase(restoreEase));
        sequence.Join(cam.DORotateQuaternion(cachedGameplayRot, restoreDuration).SetEase(restoreEase));
        activeTween = sequence.OnComplete(() => activeTween = null);
    }

    private Transform ResolveCameraTransform()
    {
        if (cameraTransform != null && cameraTransform.gameObject.activeInHierarchy)
            return cameraTransform;

        Camera cam = Camera.main;
        if (cam != null)
            cameraTransform = cam.transform;

        return cameraTransform;
    }

    private void CacheGameplayPose(Transform cam)
    {
        cachedGameplayPos = cam.position;
        cachedGameplayRot = cam.rotation;
        hasCachedGameplayPose = true;
    }

    private void KillActiveTween()
    {
        if (activeTween == null)
            return;

        activeTween.Kill(false);
        activeTween = null;
    }
}

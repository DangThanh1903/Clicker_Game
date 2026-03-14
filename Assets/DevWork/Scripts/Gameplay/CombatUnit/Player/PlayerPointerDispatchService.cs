using System;
using UnityEngine;

public sealed class PlayerPointerDispatchService
{
    private bool pointerHoldActive;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private int debugDispatchFrame = -1;
    private int debugClickDispatchCount;
    private int debugHoldDispatchCount;
#endif

    public void Tick(
        bool gameplayInputAllowed,
        IPointerDamageTargetResolver pointerTargetResolver,
        IDamageTargetSelectionService targetSelectionService,
        bool allowClickDispatch,
        bool allowHoldDispatch,
        Action<IDamageReceiver> clickDispatch,
        Action<IDamageReceiver, Vector3> holdDispatch)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        BeginPointerDispatchDiagnosticsFrame(Time.frameCount);
#endif

        if (Input.GetMouseButtonUp(0))
        {
            if (pointerHoldActive && StatsManager.Ins != null)
                StatsManager.Ins.Set(StatType.HoldedTime, 0f);

            pointerHoldActive = false;
        }

        if (!gameplayInputAllowed)
            return;

        if (pointerTargetResolver == null || targetSelectionService == null)
            return;

        bool shouldClickDispatch = allowClickDispatch && Input.GetMouseButtonDown(0);
        bool shouldHoldDispatch = allowHoldDispatch && Input.GetMouseButton(0);
        if (!shouldClickDispatch && !shouldHoldDispatch)
            return;

        if (!pointerTargetResolver.TryResolvePointerTarget(out IDamageReceiver target, out Vector3 hitPoint))
            return;
        if (!targetSelectionService.CanReceiveDamage(target))
            return;

        pointerTargetResolver.ApplyPointerHitContext(target, hitPoint);

        if (shouldClickDispatch)
        {
            clickDispatch?.Invoke(target);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RegisterPointerClickDispatch();
#endif
        }

        if (shouldHoldDispatch)
        {
            holdDispatch?.Invoke(target, hitPoint);
            pointerHoldActive = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RegisterPointerHoldDispatch();
#endif
        }
    }

    public void CancelHold()
    {
        pointerHoldActive = false;
    }

    public void ResetRuntime()
    {
        pointerHoldActive = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        debugDispatchFrame = -1;
        debugClickDispatchCount = 0;
        debugHoldDispatchCount = 0;
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void BeginPointerDispatchDiagnosticsFrame(int frame)
    {
        if (debugDispatchFrame == frame)
            return;

        if (debugDispatchFrame >= 0)
        {
            if (debugClickDispatchCount > 1)
                Debug.LogWarning($"[PlayerPointerDispatchService] Multiple click dispatches in one frame: {debugClickDispatchCount} (frame {debugDispatchFrame}).");

            if (debugHoldDispatchCount > 1)
                Debug.LogWarning($"[PlayerPointerDispatchService] Multiple hold dispatches in one frame: {debugHoldDispatchCount} (frame {debugDispatchFrame}).");
        }

        debugDispatchFrame = frame;
        debugClickDispatchCount = 0;
        debugHoldDispatchCount = 0;
    }

    private void RegisterPointerClickDispatch()
    {
        debugClickDispatchCount++;
    }

    private void RegisterPointerHoldDispatch()
    {
        debugHoldDispatchCount++;
    }
#endif
}

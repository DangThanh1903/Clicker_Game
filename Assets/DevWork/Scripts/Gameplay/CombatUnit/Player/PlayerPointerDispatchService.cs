using System;
using UnityEngine;

public sealed class PlayerPointerDispatchService
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private int debugDispatchFrame = -1;
    private int debugClickDispatchCount;
#endif

    public void Tick(
        bool gameplayInputAllowed,
        IPointerDamageTargetResolver pointerTargetResolver,
        IDamageTargetSelectionService targetSelectionService,
        bool allowClickDispatch,
        Action<IDamageReceiver> clickDispatch)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        BeginPointerDispatchDiagnosticsFrame(Time.frameCount);
#endif

        if (!gameplayInputAllowed)
            return;

        if (pointerTargetResolver == null || targetSelectionService == null)
            return;

        bool shouldClickDispatch = allowClickDispatch && Input.GetMouseButtonDown(0);
        if (!shouldClickDispatch)
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
    }

    public void ResetRuntime()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        debugDispatchFrame = -1;
        debugClickDispatchCount = 0;
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
        }

        debugDispatchFrame = frame;
        debugClickDispatchCount = 0;
    }

    private void RegisterPointerClickDispatch()
    {
        debugClickDispatchCount++;
    }
#endif
}

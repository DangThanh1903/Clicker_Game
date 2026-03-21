using System;
using UnityEngine;

public sealed class PlayerDragRotateService
{
    private static Camera cachedMainCamera;
    private static int cachedMainCameraFrame = -1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static bool hasLoggedMissingDependency;
    private static bool hasLoggedMissingCurrentBlock;
    private static bool hasLoggedMissingSpinDriver;
#endif

    private bool pointerDragActive;
    private ClickableObject activeDragBlock;
    private Vector2 lastPointerPosition;

    public void Tick(
        bool gameplayInputAllowed,
        bool enableDragRotate,
        IPointerDamageTargetResolver pointerTargetResolver,
        Func<ClickableObject> resolveCurrentBlock,
        Func<bool> isPointerOverUi,
        float rotateImpulsePerPixel,
        float minDeltaPixels,
        float inputScale,
        float maxImpulsePerFrame,
        float spinDamping,
        float spinMaxAngularSpeed,
        bool useUnscaledTime,
        float spinStopSpeedThreshold)
    {
        bool pointerDown = Input.GetMouseButtonDown(0);
        bool pointerHeld = Input.GetMouseButton(0);

        if (Input.GetMouseButtonUp(0))
        {
            StopDragSession();
            return;
        }

        if (!gameplayInputAllowed || !enableDragRotate)
        {
            StopDragSession();
            return;
        }

        if (!pointerDown && !pointerHeld)
            return;

        if (pointerTargetResolver == null || resolveCurrentBlock == null || isPointerOverUi == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogMissingDependencyOnce(
                "Missing resolver/delegate dependency (pointerTargetResolver / resolveCurrentBlock / isPointerOverUi). Drag rotate is disabled.");
#endif
            StopDragSession();
            return;
        }

        if (isPointerOverUi())
        {
            StopDragSession();
            return;
        }

        if (pointerDown)
        {
            if (pointerTargetResolver.TryResolvePointerTarget(out _, out _))
            {
                StopDragSession();
                return;
            }

            if (!TryResolveUsableBlock(resolveCurrentBlock, out var pointerDownBlock))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogMissingCurrentBlockOnceIfNoMonsterEncounter();
#endif
                StopDragSession();
                return;
            }

            StartDragSession(
                pointerDownBlock,
                spinDamping,
                spinMaxAngularSpeed,
                useUnscaledTime,
                spinStopSpeedThreshold);
            return;
        }

        if (!pointerDragActive && pointerHeld)
        {
            if (pointerTargetResolver.TryResolvePointerTarget(out _, out _))
                return;

            if (!TryResolveUsableBlock(resolveCurrentBlock, out var heldBlock))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogMissingCurrentBlockOnceIfNoMonsterEncounter();
#endif
                return;
            }

            StartDragSession(
                heldBlock,
                spinDamping,
                spinMaxAngularSpeed,
                useUnscaledTime,
                spinStopSpeedThreshold);
            return;
        }

        if (!pointerDragActive || !pointerHeld)
            return;

        if (!TryResolveUsableBlock(resolveCurrentBlock, out var block))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogMissingCurrentBlockOnceIfNoMonsterEncounter();
#endif
            StopDragSession();
            return;
        }

        if (pointerTargetResolver.TryResolvePointerTarget(out _, out _))
        {
            StopDragSession();
            return;
        }

        Vector2 current = Input.mousePosition;
        Vector2 delta = current - lastPointerPosition;
        lastPointerPosition = current;

        float threshold = Mathf.Max(0f, minDeltaPixels);
        if (delta.sqrMagnitude < threshold * threshold)
            return;

        ApplyDragRotation(
            block,
            delta,
            Mathf.Max(0f, rotateImpulsePerPixel),
            Mathf.Clamp(inputScale, 0.01f, 10f),
            Mathf.Max(0f, maxImpulsePerFrame));
    }

    public void ResetRuntime()
    {
        StopDragSession();
    }

    private static bool TryResolveUsableBlock(Func<ClickableObject> resolveCurrentBlock, out ClickableObject block)
    {
        block = resolveCurrentBlock();
        return block != null &&
               block.isActiveAndEnabled &&
               block.gameObject != null &&
               block.gameObject.activeInHierarchy;
    }

    private void StartDragSession(
        ClickableObject block,
        float spinDamping,
        float spinMaxAngularSpeed,
        bool useUnscaledTime,
        float spinStopSpeedThreshold)
    {
        if (block == null)
            return;

        ConfigureDragSpinDriver(
            block,
            spinDamping,
            spinMaxAngularSpeed,
            useUnscaledTime,
            spinStopSpeedThreshold);

        if (!ReferenceEquals(activeDragBlock, block))
        {
            activeDragBlock?.SetIdleAnimationSuppressed(false);
            activeDragBlock = block;
            activeDragBlock.SetIdleAnimationSuppressed(true);
        }

        pointerDragActive = true;
        lastPointerPosition = Input.mousePosition;
    }

    private void StopDragSession()
    {
        pointerDragActive = false;

        if (activeDragBlock == null)
            return;

        activeDragBlock.SetIdleAnimationSuppressed(false);
        activeDragBlock = null;
    }

    private static void ApplyDragRotation(
        ClickableObject block,
        Vector2 delta,
        float rotateImpulsePerPixel,
        float inputScale,
        float maxImpulsePerFrame)
    {
        if (block == null || rotateImpulsePerPixel <= 0f)
            return;

        Camera cam = ResolveMainCamera();
        if (cam == null)
            return;

        Vector3 axis =
            (-cam.transform.up * delta.x) +
            (cam.transform.right * delta.y);

        if (axis.sqrMagnitude <= 0.000001f)
            return;

        float impulse = (delta.magnitude * inputScale) * rotateImpulsePerPixel;
        if (maxImpulsePerFrame > 0f)
            impulse = Mathf.Min(impulse, maxImpulsePerFrame);

        if (impulse <= 0.0001f)
            return;

        var spinDriver = block.MomentumSpinDriver;
        if (spinDriver != null)
        {
            spinDriver.AddAngularVelocity(axis.normalized, impulse);
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LogMissingSpinDriverOnce(block);
#endif
    }

    private static Camera ResolveMainCamera()
    {
        if (cachedMainCamera != null && cachedMainCamera.isActiveAndEnabled)
            return cachedMainCamera;

        if (cachedMainCameraFrame == Time.frameCount)
            return cachedMainCamera;

        cachedMainCameraFrame = Time.frameCount;
        cachedMainCamera = Camera.main;
        return cachedMainCamera;
    }

    private static void ConfigureDragSpinDriver(
        ClickableObject block,
        float spinDamping,
        float spinMaxAngularSpeed,
        bool useUnscaledTime,
        float spinStopSpeedThreshold)
    {
        if (block == null)
            return;

        var spinDriver = block.MomentumSpinDriver;
        if (spinDriver == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogMissingSpinDriverOnce(block);
#endif
            return;
        }

        spinDriver.Configure(
            Mathf.Max(0.1f, spinDamping),
            Mathf.Max(0f, spinMaxAngularSpeed),
            useUnscaledTime,
            Mathf.Max(0.001f, spinStopSpeedThreshold));
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static void LogMissingDependencyOnce(string message)
    {
        if (hasLoggedMissingDependency)
            return;

        hasLoggedMissingDependency = true;
        Debug.LogError($"[PlayerDragRotateService] {message}");
    }

    private static void LogMissingCurrentBlockOnceIfNoMonsterEncounter()
    {
        if (BlockManager.Ins != null &&
            BlockManager.Ins.MonsterSpawner != null &&
            BlockManager.Ins.MonsterSpawner.HasActiveEncounter)
            return;

        if (hasLoggedMissingCurrentBlock)
            return;

        hasLoggedMissingCurrentBlock = true;
        Debug.LogWarning("[PlayerDragRotateService] Current block is null/inactive, drag rotate is skipped.");
    }

    private static void LogMissingSpinDriverOnce(ClickableObject block)
    {
        if (hasLoggedMissingSpinDriver)
            return;

        hasLoggedMissingSpinDriver = true;
        Debug.LogError("[PlayerDragRotateService] Missing BlockMomentumSpinDriver on current block. Drag rotate is disabled until prefab has the driver.", block);
    }
#endif
}

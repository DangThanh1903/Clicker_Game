using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Lean.Pool;
using UnityEngine;

public class BlockDropCollectAnimator : MonoBehaviour
{
    public static BlockDropCollectAnimator Ins { get; private set; }

    [Header("Visual")]
    [SerializeField] private ItemDropCollectView itemViewPrefab;
    [SerializeField] private Transform viewRoot;
    [SerializeField, Min(0.01f)] private float visualScale = 0.85f;

    [Header("Drop Arc")]
    [SerializeField, Min(0f)] private float scatterRadius = 2.35f;
    [SerializeField, Range(0f, 1.5f)] private float depthScatterMultiplier = 1.05f;
    [SerializeField, Min(0f)] private float launchStartHeight = 0.25f;
    [SerializeField, Min(0f)] private float apexHeight = 1.45f;
    [SerializeField] private float groundY = 0.05f;
    [SerializeField, Min(1)] private int maxVisualsPerDropEntry = 6;
    [SerializeField, Min(1)] private int maxTotalVisuals = 18;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float dropDuration = 0.75f;
    [SerializeField, Min(0f)] private float settleSpinDuration = 0.4f;
    [SerializeField, Min(0.01f)] private float flyDuration = 0.45f;
    [SerializeField, Min(0f)] private float dropStagger = 0.08f;
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Target")]
    [SerializeField] private Transform collectTarget;
    [SerializeField, Min(0f)] private float collectArcHeight = 0.75f;
    [SerializeField, Range(0f, 1f)] private float collectViewportX = 0.5f;
    [SerializeField, Range(0f, 1f)] private float collectViewportY = 0.08f;
    [SerializeField, Min(0.1f)] private float fallbackTargetDepth = 8f;
    [SerializeField] private Ease dropEase = Ease.Linear;
    [SerializeField] private Ease flyEase = Ease.InCubic;

    private static Camera cachedMainCamera;
    private static int cachedMainCameraFrame = -1;

    public bool CanPlayVisual => itemViewPrefab != null;

    private void Awake()
    {
        if (Ins != null && Ins != this)
        {
            Destroy(gameObject);
            return;
        }

        Ins = this;
    }

    private void OnDestroy()
    {
        if (Ins == this)
            Ins = null;
    }

    public IEnumerator PlayThenGrantDrops_Co(
        IReadOnlyList<DropGrantEntry> drops,
        Vector3 dropOrigin,
        Action<Item, int> onItemGranted,
        string logContext,
        Action<bool, string> onCompleted)
    {
        if (drops == null || drops.Count == 0)
        {
            onCompleted?.Invoke(false, string.Empty);
            yield break;
        }

        if (!CanPlayVisual)
        {
            bool granted = DropGrantService.TryGrantDrops(drops, out string immediateSummary, onItemGranted, logContext);
            onCompleted?.Invoke(granted, immediateSummary);
            yield break;
        }

        var validDrops = new List<DropGrantEntry>(drops.Count);
        var visualItems = new List<Item>(Mathf.Max(1, maxTotalVisuals));
        int remainingVisualBudget = Mathf.Max(1, maxTotalVisuals);

        for (int i = 0; i < drops.Count; i++)
        {
            DropGrantEntry entry = drops[i];
            if (entry.Item == null || entry.Item.Type == ItemType.None || entry.Amount <= 0)
                continue;

            validDrops.Add(entry);

            int visualCount = remainingVisualBudget > 0
                ? Mathf.Min(
                    Mathf.Max(1, entry.Amount),
                    Mathf.Max(1, maxVisualsPerDropEntry),
                    remainingVisualBudget)
                : 0;

            for (int visualIndex = 0; visualIndex < visualCount; visualIndex++)
                visualItems.Add(entry.Item);

            remainingVisualBudget = Mathf.Max(0, remainingVisualBudget - visualCount);
        }

        if (visualItems.Count > 0)
            yield return PlayDropVisuals_Co(visualItems, dropOrigin);

        bool grantedAny = DropGrantService.TryGrantDrops(
            validDrops,
            out string dropSummary,
            onItemGranted,
            logContext);

        onCompleted?.Invoke(grantedAny, dropSummary);
    }

    private IEnumerator PlayDropVisuals_Co(IReadOnlyList<Item> visualItems, Vector3 dropOrigin)
    {
        Camera cam = ResolveMainCamera();
        if (cam == null || visualItems == null || visualItems.Count == 0)
            yield break;

        int completed = 0;
        int spawned = 0;
        int visualCount = visualItems.Count;

        for (int i = 0; i < visualCount; i++)
        {
            if (TrySpawnDropVisual(visualItems[i], dropOrigin, cam, i, visualCount, () => completed++))
                spawned++;

            if (dropStagger > 0f && i < visualCount - 1)
                yield return WaitForDelay(dropStagger);
        }

        while (completed < spawned)
            yield return null;
    }

    private bool TrySpawnDropVisual(Item item, Vector3 dropOrigin, Camera cam, int visualIndex, int visualCount, Action onCompleted)
    {
        if (item == null || cam == null)
            return false;

        Vector3 startPosition = ResolveStartPosition(dropOrigin);
        Vector3 groundPosition = ResolveGroundPosition(dropOrigin, cam, visualIndex, visualCount);
        Vector3 dropApexPosition = ResolveDropApexPosition(startPosition, groundPosition);
        Vector3 flyTarget = ResolveFlyTarget(cam, groundPosition);
        Vector3 collectApexPosition = ResolveCollectApexPosition(groundPosition, flyTarget);
        Quaternion displayRotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);

        ItemDropCollectView view = LeanPool.Spawn(itemViewPrefab, startPosition, displayRotation, viewRoot != null ? viewRoot : transform);
        view.ResetVisual();
        view.Bind(item);

        Tween tween = view.PlayCollect(
            startPosition,
            dropApexPosition,
            groundPosition,
            collectApexPosition,
            flyTarget,
            displayRotation,
            visualScale,
            dropDuration,
            settleSpinDuration,
            flyDuration,
            dropEase,
            flyEase,
            useUnscaledTime,
            onCompleted);

        if (tween == null)
        {
            onCompleted?.Invoke();
            LeanPool.Despawn(view);
        }

        return true;
    }

    private Vector3 ResolveStartPosition(Vector3 dropOrigin)
    {
        return dropOrigin + Vector3.up * Mathf.Max(0f, launchStartHeight);
    }

    private Vector3 ResolveGroundPosition(Vector3 dropOrigin, Camera cam, int visualIndex, int visualCount)
    {
        Vector3 screenRight = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up);
        if (screenRight.sqrMagnitude < 0.0001f)
            screenRight = Vector3.right;
        else
            screenRight.Normalize();

        Vector3 screenDepth = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (screenDepth.sqrMagnitude < 0.0001f)
            screenDepth = Vector3.forward;
        else
            screenDepth.Normalize();

        const float GoldenAngle = 137.50776f;
        float angle;
        if (visualCount <= 1)
        {
            angle = UnityEngine.Random.Range(0f, 360f);
        }
        else
        {
            angle = (visualIndex + 1) * GoldenAngle + UnityEngine.Random.Range(-28f, 28f);
        }

        float radians = angle * Mathf.Deg2Rad;
        float distance = UnityEngine.Random.Range(scatterRadius * 0.55f, scatterRadius);
        Vector3 scatterOffset =
            screenRight * (Mathf.Cos(radians) * distance) +
            screenDepth * (Mathf.Sin(radians) * distance * depthScatterMultiplier);

        Vector3 groundPosition = dropOrigin + scatterOffset;
        groundPosition.y = groundY;
        return groundPosition;
    }

    private Vector3 ResolveDropApexPosition(Vector3 startPosition, Vector3 groundPosition)
    {
        Vector3 apexPosition = Vector3.Lerp(startPosition, groundPosition, 0.48f);
        apexPosition.y = Mathf.Max(startPosition.y, groundPosition.y) + Mathf.Max(0.01f, apexHeight);
        return apexPosition;
    }

    private Vector3 ResolveCollectApexPosition(Vector3 groundPosition, Vector3 flyTargetPosition)
    {
        Vector3 apexPosition = Vector3.Lerp(groundPosition, flyTargetPosition, 0.5f);
        apexPosition.y = Mathf.Max(groundPosition.y, flyTargetPosition.y) + Mathf.Max(0f, collectArcHeight);
        return apexPosition;
    }

    private Vector3 ResolveFlyTarget(Camera cam, Vector3 fromPosition)
    {
        if (collectTarget != null)
            return collectTarget.position;

        Vector3 viewport = cam.WorldToViewportPoint(fromPosition);
        float depth = viewport.z > cam.nearClipPlane ? viewport.z : fallbackTargetDepth;
        return cam.ViewportToWorldPoint(new Vector3(collectViewportX, collectViewportY, depth));
    }

    private IEnumerator WaitForDelay(float seconds)
    {
        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(seconds);
        else
            yield return new WaitForSeconds(seconds);
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
}

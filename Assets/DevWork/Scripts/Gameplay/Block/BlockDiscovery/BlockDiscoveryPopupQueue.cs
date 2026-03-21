using System.Collections;
using System.Collections.Generic;
using Game.Discovery;
using UnityEngine;
using UnityEngine.Serialization;

public class BlockDiscoveryPopupQueue : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private BlockUVDatabase blockDb;

    [Header("Banner")]
    [SerializeField] private TopNotificationType notificationType = TopNotificationType.Generic;
    [SerializeField] private string discoveredPrefix = "Discovered block";
    [SerializeField, Min(0.2f)] private float bannerDuration = 1.4f;
    [SerializeField, FormerlySerializedAs("popupPrefab")] private BlockDiscoveryPopupView legacyPopupPrefab;

    private readonly Queue<string> queue = new Queue<string>();
    private Coroutine runRoutine;
    private Coroutine subscribeRoutine;
    private bool running;
    private bool subscribed;
    private bool warnedMissingBannerManager;

    private void OnEnable()
    {
        if (subscribeRoutine == null)
            subscribeRoutine = StartCoroutine(WaitAndSubscribe());
    }

    private void OnDisable()
    {
        if (subscribeRoutine != null)
        {
            StopCoroutine(subscribeRoutine);
            subscribeRoutine = null;
        }

        if (runRoutine != null)
        {
            StopCoroutine(runRoutine);
            runRoutine = null;
            running = false;
        }

        if (subscribed && BlockDiscoveryService.Ins != null)
            BlockDiscoveryService.Ins.OnBlockDiscovered -= Enqueue;
        subscribed = false;
    }

    private IEnumerator WaitAndSubscribe()
    {
        while (BlockDiscoveryService.Ins == null)
            yield return null;

        if (!subscribed)
        {
            BlockDiscoveryService.Ins.OnBlockDiscovered += Enqueue;
            subscribed = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevLog.Log("[DiscoveryPopupQueue] Subscribed to OnBlockDiscovered");
#endif
        }

        subscribeRoutine = null;
    }

    private void Enqueue(string blockName)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DevLog.Log($"[DiscoveryPopupQueue] Enqueue {blockName}");
#endif
        queue.Enqueue(blockName);
        if (runRoutine == null)
            runRoutine = StartCoroutine(RunQueue());
    }

    private IEnumerator RunQueue()
    {
        if (running)
            yield break;

        running = true;

        while (queue.Count > 0)
        {
            string blockName = queue.Dequeue();
            var entry = blockDb != null ? blockDb.GetByName(blockName) : null;

            if (entry == null)
            {
                Debug.LogWarning($"[DiscoveryPopupQueue] Block not found in DB: {blockName}");
                continue;
            }

            if (TopNotificationManager.Ins == null)
            {
                if (!warnedMissingBannerManager)
                {
                    warnedMissingBannerManager = true;
                    Debug.LogWarning("[DiscoveryPopupQueue] TopNotificationManager is missing. Discovery banner will be skipped.");
                }

                yield return null;
                continue;
            }

            string displayName = string.IsNullOrWhiteSpace(entry.blockName) ? blockName : entry.blockName;
            TopNotificationManager.Notify(notificationType, $"{discoveredPrefix}: {displayName}", bannerDuration);
            yield return null;
        }

        running = false;
        runRoutine = null;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Discovery;

public class BlockDiscoveryPopupQueue : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private BlockUVDatabase blockDb;

    [Header("Popup")]
    [SerializeField] private BlockDiscoveryPopupView popupPrefab;

    private readonly Queue<string> _queue = new Queue<string>();
    private Coroutine _runRoutine;
    private Coroutine _subscribeRoutine;
    private bool _running;
    private bool _subscribed;

    private void OnEnable()
    {
        if (_subscribeRoutine == null)
            _subscribeRoutine = StartCoroutine(WaitAndSubscribe());
    }

    private void OnDisable()
    {
        if (_subscribeRoutine != null)
        {
            StopCoroutine(_subscribeRoutine);
            _subscribeRoutine = null;
        }

        if (_runRoutine != null)
        {
            StopCoroutine(_runRoutine);
            _runRoutine = null;
            _running = false;
        }

        if (_subscribed && BlockDiscoveryService.Ins != null)
            BlockDiscoveryService.Ins.OnBlockDiscovered -= Enqueue;
        _subscribed = false;
    }

    private IEnumerator WaitAndSubscribe()
    {
        while (BlockDiscoveryService.Ins == null)
            yield return null;

        if (!_subscribed)
        {
            BlockDiscoveryService.Ins.OnBlockDiscovered += Enqueue;
            _subscribed = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevLog.Log("[DiscoveryPopupQueue] Subscribed to OnBlockDiscovered");
#endif
        }

        _subscribeRoutine = null;
    }

    private void Enqueue(string blockName)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DevLog.Log($"[DiscoveryPopupQueue] Enqueue {blockName}");
#endif
        _queue.Enqueue(blockName);
        if (_runRoutine == null)
            _runRoutine = StartCoroutine(RunQueue());
    }

    private IEnumerator RunQueue()
    {
        if (_running) yield break;
        _running = true;

        while (_queue.Count > 0)
        {
            string blockName = _queue.Dequeue();
            var entry = blockDb != null ? blockDb.GetByName(blockName) : null;

            if (entry == null)
            {
                Debug.LogWarning($"[DiscoveryPopupQueue] Block not found in DB: {blockName}");
                continue;
            }

            var popupController = PopupController.Instance;
            if (popupController == null)
            {
                Debug.LogWarning("[DiscoveryPopupQueue] PopupController is missing.");
                yield return null;
                continue;
            }

            var showTask = popupController.Show(popupPrefab, popup =>
            {
                if (popup is BlockDiscoveryPopupView view)
                    view.Bind(entry);
            });
            while (!showTask.IsCompleted)
                yield return null;

            if (showTask.IsFaulted)
            {
                Debug.LogWarning("[DiscoveryPopupQueue] Failed to show popup.");
                continue;
            }

            // Wait until the popup stack is empty again
            while (popupController != null && popupController.IsAnyPopupOpen())
                yield return null;
        }

        _running = false;
        _runRoutine = null;
    }
}


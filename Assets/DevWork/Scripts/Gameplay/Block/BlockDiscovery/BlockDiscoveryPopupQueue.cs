using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Game.Discovery;

public class BlockDiscoveryPopupQueue : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private BlockUVDatabase blockDb;

    [Header("Popup")]
    [SerializeField] private BlockDiscoveryPopupView popupPrefab;

    private readonly Queue<string> _queue = new Queue<string>();
    private bool _running;
    private bool _subscribed;

    private async void Start()
    {
        // Wait until the discovery service exists
        while (BlockDiscoveryService.Ins == null)
            await Task.Yield();

        // Subscribe once
        if (!_subscribed)
        {
            BlockDiscoveryService.Ins.OnBlockDiscovered += Enqueue;
            _subscribed = true;
            Debug.Log("[DiscoveryPopupQueue] Subscribed to OnBlockDiscovered");
        }
    }

    private void OnDestroy()
    {
        if (_subscribed && BlockDiscoveryService.Ins != null)
            BlockDiscoveryService.Ins.OnBlockDiscovered -= Enqueue;
    }

    private void Enqueue(string blockName)
    {
        Debug.Log($"[DiscoveryPopupQueue] Enqueue {blockName}");
        _queue.Enqueue(blockName);
        _ = RunQueue();
    }

    private async Task RunQueue()
    {
        if (_running) return;
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

            await PopupController.Instance.Show(popupPrefab, popup =>
            {
                if (popup is BlockDiscoveryPopupView view)
                    view.Bind(entry);
            });

            // Wait until the popup stack is empty again
            while (PopupController.Instance.IsAnyPopupOpen())
                await Task.Yield();
        }

        _running = false;
    }
}

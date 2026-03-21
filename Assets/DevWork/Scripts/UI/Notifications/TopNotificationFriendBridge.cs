using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public sealed class TopNotificationFriendBridge : MonoBehaviour
{
    [Header("Realtime")]
    [SerializeField] private int realtimeLimit = 100;

    [Header("Enable Types")]
    [SerializeField] private bool notifyIncomingRequest = true;
    [SerializeField] private bool notifyNewFriend = true;

    [Header("Message")]
    [SerializeField] private string incomingRequestPrefix = "New friend request from";
    [SerializeField] private string newFriendPrefix = "You are now friends with";
    [SerializeField, Min(0.2f)] private float duration = 1.6f;

    private readonly HashSet<string> knownIncomingRequestUids = new HashSet<string>();
    private readonly HashSet<string> knownFriendUids = new HashSet<string>();

    private bool incomingPrimed;
    private bool friendsPrimed;
    private bool subscribed;
    private bool startedRealtimeByThisBridge;
    private int lifecycleVersion;

    private void OnEnable()
    {
        lifecycleVersion++;
        SubscribeRealtime();
        _ = EnsureRealtimeStartedAsync(lifecycleVersion);
    }

    private void OnDisable()
    {
        lifecycleVersion++;
        UnsubscribeRealtime();

        if (startedRealtimeByThisBridge)
            FriendService.StopRealtimeSync();

        startedRealtimeByThisBridge = false;
        incomingPrimed = false;
        friendsPrimed = false;
        knownIncomingRequestUids.Clear();
        knownFriendUids.Clear();
    }

    private async Task EnsureRealtimeStartedAsync(int version)
    {
        bool wasListening = FriendService.IsRealtimeListening;
        var result = await FriendService.StartRealtimeSyncAsync(realtimeLimit);

        if (version != lifecycleVersion)
        {
            if (!wasListening && result.status == FriendOpStatus.Success)
                FriendService.StopRealtimeSync();
            return;
        }

        startedRealtimeByThisBridge = !wasListening && result.status == FriendOpStatus.Success;
    }

    private void SubscribeRealtime()
    {
        if (subscribed)
            return;

        subscribed = true;
        FriendService.RealtimeIncomingRequestsChanged += OnIncomingRequestsChanged;
        FriendService.RealtimeFriendsChanged += OnFriendsChanged;
    }

    private void UnsubscribeRealtime()
    {
        if (!subscribed)
            return;

        subscribed = false;
        FriendService.RealtimeIncomingRequestsChanged -= OnIncomingRequestsChanged;
        FriendService.RealtimeFriendsChanged -= OnFriendsChanged;
    }

    private void OnIncomingRequestsChanged(IReadOnlyList<FriendRequestData> rows)
    {
        var next = new HashSet<string>();
        if (rows != null)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || string.IsNullOrWhiteSpace(row.uid))
                    continue;

                next.Add(row.uid);

                if (incomingPrimed && notifyIncomingRequest && !knownIncomingRequestUids.Contains(row.uid))
                {
                    string name = string.IsNullOrWhiteSpace(row.displayName) ? FriendUiFormat.ShortUid(row.uid) : row.displayName;
                    TopNotificationManager.NotifyFriend($"{incomingRequestPrefix} {name}", duration);
                }
            }
        }

        knownIncomingRequestUids.Clear();
        foreach (var uid in next)
            knownIncomingRequestUids.Add(uid);

        incomingPrimed = true;
    }

    private void OnFriendsChanged(IReadOnlyList<FriendLinkData> rows)
    {
        var next = new HashSet<string>();
        if (rows != null)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || string.IsNullOrWhiteSpace(row.uid))
                    continue;

                next.Add(row.uid);

                if (friendsPrimed && notifyNewFriend && !knownFriendUids.Contains(row.uid))
                {
                    string name = string.IsNullOrWhiteSpace(row.displayName) ? FriendUiFormat.ShortUid(row.uid) : row.displayName;
                    TopNotificationManager.NotifyFriend($"{newFriendPrefix} {name}", duration);
                }
            }
        }

        knownFriendUids.Clear();
        foreach (var uid in next)
            knownFriendUids.Add(uid);

        friendsPrimed = true;
    }
}

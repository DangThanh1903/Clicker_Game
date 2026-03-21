using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

public static class FriendRealtimeService
{
    public static event Action<IReadOnlyList<FriendLinkData>> FriendsChanged;
    public static event Action<IReadOnlyList<FriendRequestData>> IncomingRequestsChanged;
    public static event Action<IReadOnlyList<FriendRequestData>> OutgoingRequestsChanged;
    public static event Action<string> RealtimeError;

    public static bool IsListening =>
        friendsListener != null ||
        incomingListener != null ||
        outgoingListener != null;

    private static ListenerRegistration friendsListener;
    private static ListenerRegistration incomingListener;
    private static ListenerRegistration outgoingListener;
    private static string boundUid;
    private static int boundLimit;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Stop();
        FriendsChanged = null;
        IncomingRequestsChanged = null;
        OutgoingRequestsChanged = null;
        RealtimeError = null;
    }

    public static async Task<FriendOpResult> StartAsync(int limit = FriendServiceConstants.DefaultQueryLimit)
    {
        var context = await FriendContextResolver.TryResolveAsync();
        if (context == null)
            return FriendServiceUtil.Result(FriendOpStatus.NotReady, "No connection.");

        limit = FriendServiceUtil.ClampLimit(limit);
        if (IsListening && boundUid == context.Uid && boundLimit == limit)
            return FriendServiceUtil.Result(FriendOpStatus.Success, "Realtime already running.");

        Stop();
        boundUid = context.Uid;
        boundLimit = limit;

        try
        {
            friendsListener = FriendFirestoreRefs.FriendsCol(context.Db, context.Uid)
                .Limit(limit)
                .Listen(snapshot => SafeEmitFriends(snapshot));

            incomingListener = FriendFirestoreRefs.UserDoc(context.Db, context.Uid)
                .Collection(FriendServiceConstants.RequestsInCollection)
                .Limit(limit)
                .Listen(snapshot => SafeEmitIncomingRequests(snapshot));

            outgoingListener = FriendFirestoreRefs.UserDoc(context.Db, context.Uid)
                .Collection(FriendServiceConstants.RequestsOutCollection)
                .Limit(limit)
                .Listen(snapshot => SafeEmitOutgoingRequests(snapshot));

            return FriendServiceUtil.Result(FriendOpStatus.Success, "Realtime started.");
        }
        catch (Exception ex)
        {
            Stop();
            Debug.LogWarning($"[FriendRealtimeService] StartAsync failed: {ex.Message}");
            return FriendServiceUtil.Result(FriendOpStatus.Error, "Failed to start realtime.");
        }
    }

    public static void Stop()
    {
        StopListener(ref friendsListener);
        StopListener(ref incomingListener);
        StopListener(ref outgoingListener);
        boundUid = null;
        boundLimit = 0;
    }

    private static void StopListener(ref ListenerRegistration listener)
    {
        if (listener == null)
            return;

        try
        {
            listener.Stop();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FriendRealtimeService] Stop listener failed: {ex.Message}");
        }
        finally
        {
            listener = null;
        }
    }

    private static void SafeEmitFriends(QuerySnapshot snapshot)
    {
        try
        {
            FriendsChanged?.Invoke(MapFriends(snapshot));
        }
        catch (Exception ex)
        {
            ReportRealtimeError($"Friends listener error: {ex.Message}");
        }
    }

    private static void SafeEmitIncomingRequests(QuerySnapshot snapshot)
    {
        try
        {
            IncomingRequestsChanged?.Invoke(MapIncomingRequests(snapshot));
        }
        catch (Exception ex)
        {
            ReportRealtimeError($"Incoming listener error: {ex.Message}");
        }
    }

    private static void SafeEmitOutgoingRequests(QuerySnapshot snapshot)
    {
        try
        {
            OutgoingRequestsChanged?.Invoke(MapIncomingRequests(snapshot));
        }
        catch (Exception ex)
        {
            ReportRealtimeError($"Outgoing listener error: {ex.Message}");
        }
    }

    private static void ReportRealtimeError(string message)
    {
        Debug.LogWarning($"[FriendRealtimeService] {message}");
        RealtimeError?.Invoke(message);
    }

    private static IReadOnlyList<FriendLinkData> MapFriends(QuerySnapshot snapshot)
    {
        var results = new List<FriendLinkData>();
        if (snapshot == null)
            return results;

        foreach (var doc in snapshot.Documents)
        {
            if (!doc.Exists)
                continue;

            var data = doc.ConvertTo<FriendLinkData>() ?? new FriendLinkData();
            data.uid = string.IsNullOrWhiteSpace(data.uid) ? doc.Id : data.uid;
            data.displayName = string.IsNullOrWhiteSpace(data.displayName)
                ? FriendServiceUtil.ShortUid(data.uid)
                : data.displayName;
            results.Add(data);
        }

        return results;
    }

    private static IReadOnlyList<FriendRequestData> MapIncomingRequests(QuerySnapshot snapshot)
    {
        var results = new List<FriendRequestData>();
        if (snapshot == null)
            return results;

        foreach (var doc in snapshot.Documents)
        {
            if (!doc.Exists)
                continue;

            var data = doc.ConvertTo<FriendRequestData>() ?? new FriendRequestData();
            data.uid = string.IsNullOrWhiteSpace(data.uid) ? doc.Id : data.uid;
            data.displayName = string.IsNullOrWhiteSpace(data.displayName)
                ? FriendServiceUtil.ShortUid(data.uid)
                : data.displayName;
            results.Add(data);
        }

        return results;
    }

}

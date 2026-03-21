using System.Collections.Generic;
using System.Threading.Tasks;
using System;

public static class FriendService
{
    public static event Action<IReadOnlyList<FriendLinkData>> RealtimeFriendsChanged
    {
        add => FriendRealtimeService.FriendsChanged += value;
        remove => FriendRealtimeService.FriendsChanged -= value;
    }

    public static event Action<IReadOnlyList<FriendRequestData>> RealtimeIncomingRequestsChanged
    {
        add => FriendRealtimeService.IncomingRequestsChanged += value;
        remove => FriendRealtimeService.IncomingRequestsChanged -= value;
    }

    public static event Action<IReadOnlyList<FriendRequestData>> RealtimeOutgoingRequestsChanged
    {
        add => FriendRealtimeService.OutgoingRequestsChanged += value;
        remove => FriendRealtimeService.OutgoingRequestsChanged -= value;
    }

    public static event Action<string> RealtimeError
    {
        add => FriendRealtimeService.RealtimeError += value;
        remove => FriendRealtimeService.RealtimeError -= value;
    }

    public static bool IsRealtimeListening => FriendRealtimeService.IsListening;

    public static Task<FriendOpResult> StartRealtimeSyncAsync(int limit = FriendServiceConstants.DefaultQueryLimit)
    {
        return FriendRealtimeService.StartAsync(limit);
    }

    public static void StopRealtimeSync()
    {
        FriendRealtimeService.Stop();
    }

    public static async Task<FriendOpResult> SendFriendRequestAsync(string targetUid)
    {
        var context = await FriendContextResolver.TryResolveAsync();
        if (context == null)
            return FriendServiceUtil.Result(FriendOpStatus.NotReady, "No connection.");

        return await new FriendRelationOps(context).SendFriendRequestAsync(targetUid);
    }

    public static async Task<FriendOpResult> AcceptFriendRequestAsync(string fromUid)
    {
        var context = await FriendContextResolver.TryResolveAsync();
        if (context == null)
            return FriendServiceUtil.Result(FriendOpStatus.NotReady, "No connection.");

        return await new FriendRelationOps(context).AcceptFriendRequestAsync(fromUid);
    }

    public static async Task<FriendOpResult> RejectFriendRequestAsync(string fromUid)
    {
        var context = await FriendContextResolver.TryResolveAsync();
        if (context == null)
            return FriendServiceUtil.Result(FriendOpStatus.NotReady, "No connection.");

        return await new FriendRelationOps(context).RejectFriendRequestAsync(fromUid);
    }

    public static async Task<FriendOpResult> CancelFriendRequestAsync(string toUid)
    {
        var context = await FriendContextResolver.TryResolveAsync();
        if (context == null)
            return FriendServiceUtil.Result(FriendOpStatus.NotReady, "No connection.");

        return await new FriendRelationOps(context).CancelFriendRequestAsync(toUid);
    }

    public static async Task<FriendOpResult> RemoveFriendAsync(string friendUid)
    {
        var context = await FriendContextResolver.TryResolveAsync();
        if (context == null)
            return FriendServiceUtil.Result(FriendOpStatus.NotReady, "No connection.");

        return await new FriendRelationOps(context).RemoveFriendAsync(friendUid);
    }

    public static async Task<List<FriendLinkData>> GetFriendsAsync(int limit = FriendServiceConstants.DefaultQueryLimit)
    {
        var context = await FriendContextResolver.TryResolveAsync();
        if (context == null)
            return new List<FriendLinkData>();

        return await new FriendRelationOps(context).GetFriendsAsync(limit);
    }

    public static async Task<List<FriendRequestData>> GetIncomingRequestsAsync(int limit = FriendServiceConstants.DefaultQueryLimit)
    {
        var context = await FriendContextResolver.TryResolveAsync();
        if (context == null)
            return new List<FriendRequestData>();

        return await new FriendRelationOps(context)
            .GetRequestsAsync(FriendServiceConstants.RequestsInCollection, limit);
    }

    public static async Task<List<FriendRequestData>> GetOutgoingRequestsAsync(int limit = FriendServiceConstants.DefaultQueryLimit)
    {
        var context = await FriendContextResolver.TryResolveAsync();
        if (context == null)
            return new List<FriendRequestData>();

        return await new FriendRelationOps(context)
            .GetRequestsAsync(FriendServiceConstants.RequestsOutCollection, limit);
    }

    public static async Task<FriendOpResult> SendDailyGiftAsync(
        string friendUid,
        int giftDiamonds = FriendServiceConstants.DefaultGiftDiamonds)
    {
        var context = await FriendContextResolver.TryResolveAsync();
        if (context == null)
            return FriendServiceUtil.Result(FriendOpStatus.NotReady, "No connection.");

        return await new FriendGiftOps(context).SendDailyGiftAsync(friendUid, giftDiamonds);
    }

    public static async Task<List<FriendGiftData>> GetPendingGiftsAsync(int limit = FriendServiceConstants.DefaultQueryLimit)
    {
        var context = await FriendContextResolver.TryResolveAsync();
        if (context == null)
            return new List<FriendGiftData>();

        return await new FriendGiftOps(context).GetPendingGiftsAsync(limit);
    }

    public static async Task<FriendOpResult> ClaimGiftAsync(string giftId)
    {
        var context = await FriendContextResolver.TryResolveAsync();
        if (context == null)
            return FriendServiceUtil.Result(FriendOpStatus.NotReady, "No connection.");

        return await new FriendGiftOps(context).ClaimGiftAsync(giftId);
    }

    public static async Task<FriendPublicProfile> GetPublicProfileAsync(string targetUid)
    {
        var db = await FriendContextResolver.TryResolveDbAsync();
        if (db == null)
            return null;

        return await new FriendProfileOps(db).GetPublicProfileAsync(targetUid);
    }

    public static async Task<List<FriendPublicProfile>> GetTopPublicProfilesAsync(
        int limit = FriendServiceConstants.DefaultAddFriendListLimit)
    {
        var db = await FriendContextResolver.TryResolveDbAsync();
        if (db == null)
            return new List<FriendPublicProfile>();

        return await new FriendProfileOps(db).GetTopPublicProfilesAsync(limit);
    }

    public static async Task<List<FriendPublicProfile>> SearchPublicProfilesByDisplayNamePrefixAsync(
        string keyword,
        int limit = FriendServiceConstants.DefaultAddFriendListLimit)
    {
        var db = await FriendContextResolver.TryResolveDbAsync();
        if (db == null)
            return new List<FriendPublicProfile>();

        return await new FriendProfileOps(db).SearchPublicProfilesByDisplayNamePrefixAsync(keyword, limit);
    }
}

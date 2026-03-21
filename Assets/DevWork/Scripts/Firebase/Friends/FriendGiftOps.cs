using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

internal sealed class FriendGiftOps
{
    private readonly FriendRuntimeContext context;

    private enum SendGiftState
    {
        Success,
        NotFriends,
        AlreadySent,
        Error
    }

    private enum ClaimGiftState
    {
        Success,
        GiftNotFound,
        AlreadyClaimed,
        Error
    }

    public FriendGiftOps(FriendRuntimeContext context)
    {
        this.context = context;
    }

    public async Task<FriendOpResult> SendDailyGiftAsync(string friendUid, int giftDiamonds)
    {
        friendUid = FriendServiceUtil.NormalizeUid(friendUid);
        giftDiamonds = Math.Max(0, giftDiamonds);

        if (string.IsNullOrEmpty(friendUid))
            return FriendServiceUtil.Result(FriendOpStatus.InvalidInput, "Invalid user.");
        if (giftDiamonds <= 0)
            return FriendServiceUtil.Result(FriendOpStatus.InvalidInput, "Gift amount must be positive.");
        if (friendUid == context.Uid)
            return FriendServiceUtil.Result(FriendOpStatus.InvalidInput, "Cannot gift yourself.");

        string dayKey = FriendServiceUtil.GetUtcDayKey();
        string stateId = $"{friendUid}_{dayKey}";
        string giftId = $"{context.Uid}_{dayKey}";
        var now = Timestamp.GetCurrentTimestamp();

        var friendRef = FriendFirestoreRefs.FriendDoc(context.Db, context.Uid, friendUid);
        var stateRef = FriendFirestoreRefs.GiftStateDoc(context.Db, context.Uid, stateId);
        var giftInRef = FriendFirestoreRefs.GiftInDoc(context.Db, friendUid, giftId);

        SendGiftState state = SendGiftState.Error;

        try
        {
            await FirebaseTaskTracker.Track(context.Db.RunTransactionAsync(async tx =>
            {
                var friendSnap = await tx.GetSnapshotAsync(friendRef);
                if (!friendSnap.Exists)
                {
                    state = SendGiftState.NotFriends;
                    return;
                }

                var stateSnap = await tx.GetSnapshotAsync(stateRef);
                if (stateSnap.Exists)
                {
                    state = SendGiftState.AlreadySent;
                    return;
                }

                tx.Set(stateRef, new FriendGiftStateData
                {
                    fromUid = context.Uid,
                    toUid = friendUid,
                    dayKey = dayKey,
                    createdAt = now
                });

                tx.Set(giftInRef, new FriendGiftData
                {
                    giftId = giftId,
                    fromUid = context.Uid,
                    fromDisplayName = context.DisplayName,
                    diamonds = giftDiamonds,
                    dayKey = dayKey,
                    createdAt = now,
                    status = FriendServiceConstants.GiftStatusPending
                });

                state = SendGiftState.Success;
            }));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FriendService] SendDailyGiftAsync failed: {ex.Message}");
            return FriendServiceUtil.Result(FriendOpStatus.Error, "Send gift failed.");
        }

        if (state == SendGiftState.Success)
            return FriendServiceUtil.Result(FriendOpStatus.Success, "Gift sent.");
        if (state == SendGiftState.NotFriends)
            return FriendServiceUtil.Result(FriendOpStatus.NotFriends, "You are not friends.");
        if (state == SendGiftState.AlreadySent)
            return FriendServiceUtil.Result(FriendOpStatus.GiftAlreadySentToday, "Gift already sent today.");
        return FriendServiceUtil.Result(FriendOpStatus.Error, "Send gift failed.");
    }

    public async Task<List<FriendGiftData>> GetPendingGiftsAsync(int limit)
    {
        var results = new List<FriendGiftData>();

        Query query = FriendFirestoreRefs.GiftsInCol(context.Db, context.Uid)
            .WhereEqualTo("status", FriendServiceConstants.GiftStatusPending)
            .Limit(FriendServiceUtil.ClampLimit(limit));

        QuerySnapshot snapshot;
        try
        {
            snapshot = await FirebaseTaskTracker.Track(query.GetSnapshotAsync());
        }
        catch
        {
            return results;
        }

        foreach (var doc in snapshot.Documents)
        {
            if (!doc.Exists)
                continue;

            var data = doc.ConvertTo<FriendGiftData>() ?? new FriendGiftData();
            data.giftId = string.IsNullOrWhiteSpace(data.giftId) ? doc.Id : data.giftId;
            data.fromUid = string.IsNullOrWhiteSpace(data.fromUid) ? string.Empty : data.fromUid;
            data.fromDisplayName = string.IsNullOrWhiteSpace(data.fromDisplayName)
                ? FriendServiceUtil.ShortUid(data.fromUid)
                : data.fromDisplayName;
            results.Add(data);
        }

        return results;
    }

    public async Task<FriendOpResult> ClaimGiftAsync(string giftId)
    {
        giftId = giftId != null ? giftId.Trim() : string.Empty;
        if (string.IsNullOrEmpty(giftId))
            return FriendServiceUtil.Result(FriendOpStatus.InvalidInput, "Invalid gift.");
        if (StatsManager.Ins == null)
            return FriendServiceUtil.Result(FriendOpStatus.MissingRuntimeDependency, "StatsManager is missing.");

        var giftRef = FriendFirestoreRefs.GiftInDoc(context.Db, context.Uid, giftId);
        int diamonds = 0;
        ClaimGiftState state = ClaimGiftState.Error;

        try
        {
            await FirebaseTaskTracker.Track(context.Db.RunTransactionAsync(async tx =>
            {
                var giftSnap = await tx.GetSnapshotAsync(giftRef);
                if (!giftSnap.Exists)
                {
                    state = ClaimGiftState.GiftNotFound;
                    return;
                }

                string status = FriendServiceUtil.ReadString(giftSnap, "status", string.Empty);
                if (string.Equals(status, FriendServiceConstants.GiftStatusClaimed, StringComparison.OrdinalIgnoreCase))
                {
                    state = ClaimGiftState.AlreadyClaimed;
                    return;
                }

                diamonds = Math.Max(0, FriendServiceUtil.ReadInt(giftSnap, "diamonds", 0));

                tx.Set(giftRef, new Dictionary<string, object>
                {
                    ["status"] = FriendServiceConstants.GiftStatusClaimed,
                    ["claimedAt"] = Timestamp.GetCurrentTimestamp()
                }, SetOptions.MergeAll);

                state = ClaimGiftState.Success;
            }));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FriendService] ClaimGiftAsync failed: {ex.Message}");
            return FriendServiceUtil.Result(FriendOpStatus.Error, "Claim failed.");
        }

        if (state == ClaimGiftState.GiftNotFound)
            return FriendServiceUtil.Result(FriendOpStatus.GiftNotFound, "Gift not found.");
        if (state == ClaimGiftState.AlreadyClaimed)
            return FriendServiceUtil.Result(FriendOpStatus.GiftAlreadyClaimed, "Gift already claimed.");
        if (state != ClaimGiftState.Success)
            return FriendServiceUtil.Result(FriendOpStatus.Error, "Claim failed.");

        if (diamonds > 0)
        {
            StatsManager.Ins.Add(StatType.Diamond, diamonds);
            DataSaver.Ins?.SaveDataFn();
        }

        return FriendServiceUtil.Result(FriendOpStatus.Success, "Gift claimed.", diamonds);
    }
}

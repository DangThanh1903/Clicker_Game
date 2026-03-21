using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

internal sealed class FriendRelationOps
{
    private readonly FriendRuntimeContext context;

    private enum SendRequestState
    {
        Success,
        TargetNotFound,
        AlreadyFriends,
        AlreadyRequested,
        IncomingRequestExists,
        Error
    }

    private enum AcceptRequestState
    {
        Success,
        RequestNotFound,
        Error
    }

    public FriendRelationOps(FriendRuntimeContext context)
    {
        this.context = context;
    }

    public async Task<FriendOpResult> SendFriendRequestAsync(string targetUid)
    {
        targetUid = FriendServiceUtil.NormalizeUid(targetUid);
        if (string.IsNullOrEmpty(targetUid))
            return FriendServiceUtil.Result(FriendOpStatus.InvalidInput, "Invalid target.");
        if (targetUid == context.Uid)
            return FriendServiceUtil.Result(FriendOpStatus.InvalidInput, "Cannot add yourself.");

        string targetDisplayName = await FriendServiceUtil.ResolveDisplayNameForUidAsync(context.Db, targetUid);
        var now = Timestamp.GetCurrentTimestamp();

        var selfFriendRef = FriendFirestoreRefs.FriendDoc(context.Db, context.Uid, targetUid);
        var targetFriendRef = FriendFirestoreRefs.FriendDoc(context.Db, targetUid, context.Uid);
        var outgoingRef = FriendFirestoreRefs.OutgoingRequestDoc(context.Db, context.Uid, targetUid);
        var incomingRef = FriendFirestoreRefs.IncomingRequestDoc(context.Db, context.Uid, targetUid);
        var targetIncomingRef = FriendFirestoreRefs.IncomingRequestDoc(context.Db, targetUid, context.Uid);
        var targetUserRef = FriendFirestoreRefs.UserDoc(context.Db, targetUid);

        SendRequestState state = SendRequestState.Error;

        try
        {
            await FirebaseTaskTracker.Track(context.Db.RunTransactionAsync(async tx =>
            {
                var targetUserSnap = await tx.GetSnapshotAsync(targetUserRef);
                if (!targetUserSnap.Exists)
                {
                    state = SendRequestState.TargetNotFound;
                    return;
                }

                var selfFriendSnap = await tx.GetSnapshotAsync(selfFriendRef);
                if (selfFriendSnap.Exists)
                {
                    state = SendRequestState.AlreadyFriends;
                    return;
                }

                var targetFriendSnap = await tx.GetSnapshotAsync(targetFriendRef);
                if (targetFriendSnap.Exists)
                {
                    state = SendRequestState.AlreadyFriends;
                    return;
                }

                var outgoingSnap = await tx.GetSnapshotAsync(outgoingRef);
                if (outgoingSnap.Exists)
                {
                    state = SendRequestState.AlreadyRequested;
                    return;
                }

                var incomingSnap = await tx.GetSnapshotAsync(incomingRef);
                if (incomingSnap.Exists)
                {
                    state = SendRequestState.IncomingRequestExists;
                    return;
                }

                tx.Set(outgoingRef, new FriendRequestData
                {
                    uid = targetUid,
                    displayName = targetDisplayName,
                    createdAt = now
                });

                tx.Set(targetIncomingRef, new FriendRequestData
                {
                    uid = context.Uid,
                    displayName = context.DisplayName,
                    createdAt = now
                });

                state = SendRequestState.Success;
            }));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FriendService] SendFriendRequestAsync failed: {ex.Message}");
            return FriendServiceUtil.Result(FriendOpStatus.Error, "Request failed.");
        }

        switch (state)
        {
            case SendRequestState.Success:
                return FriendServiceUtil.Result(FriendOpStatus.Success, "Request sent.");
            case SendRequestState.TargetNotFound:
                return FriendServiceUtil.Result(FriendOpStatus.TargetNotFound, "Target not found.");
            case SendRequestState.AlreadyFriends:
                return FriendServiceUtil.Result(FriendOpStatus.AlreadyFriends, "Already friends.");
            case SendRequestState.AlreadyRequested:
                return FriendServiceUtil.Result(FriendOpStatus.AlreadyRequested, "Request already sent.");
            case SendRequestState.IncomingRequestExists:
                return FriendServiceUtil.Result(FriendOpStatus.IncomingRequestExists, "This player already sent you a request.");
            default:
                return FriendServiceUtil.Result(FriendOpStatus.Error, "Request failed.");
        }
    }

    public async Task<FriendOpResult> AcceptFriendRequestAsync(string fromUid)
    {
        fromUid = FriendServiceUtil.NormalizeUid(fromUid);
        if (string.IsNullOrEmpty(fromUid) || fromUid == context.Uid)
            return FriendServiceUtil.Result(FriendOpStatus.InvalidInput, "Invalid user.");

        var requestInRef = FriendFirestoreRefs.IncomingRequestDoc(context.Db, context.Uid, fromUid);
        var requestOutRef = FriendFirestoreRefs.OutgoingRequestDoc(context.Db, fromUid, context.Uid);
        var now = Timestamp.GetCurrentTimestamp();

        AcceptRequestState state = AcceptRequestState.Error;

        try
        {
            await FirebaseTaskTracker.Track(context.Db.RunTransactionAsync(async tx =>
            {
                var incomingSnap = await tx.GetSnapshotAsync(requestInRef);
                if (!incomingSnap.Exists)
                {
                    state = AcceptRequestState.RequestNotFound;
                    return;
                }

                string fromDisplayName = FriendServiceUtil.ReadString(
                    incomingSnap,
                    "displayName",
                    FriendServiceUtil.ShortUid(fromUid));

                tx.Set(FriendFirestoreRefs.FriendDoc(context.Db, context.Uid, fromUid), new FriendLinkData
                {
                    uid = fromUid,
                    displayName = fromDisplayName,
                    sinceAt = now,
                    updatedAt = now
                });

                tx.Set(FriendFirestoreRefs.FriendDoc(context.Db, fromUid, context.Uid), new FriendLinkData
                {
                    uid = context.Uid,
                    displayName = context.DisplayName,
                    sinceAt = now,
                    updatedAt = now
                });

                tx.Delete(requestInRef);
                tx.Delete(requestOutRef);
                tx.Delete(FriendFirestoreRefs.OutgoingRequestDoc(context.Db, context.Uid, fromUid));
                tx.Delete(FriendFirestoreRefs.IncomingRequestDoc(context.Db, fromUid, context.Uid));
                state = AcceptRequestState.Success;
            }));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FriendService] AcceptFriendRequestAsync failed: {ex.Message}");
            return FriendServiceUtil.Result(FriendOpStatus.Error, "Accept failed.");
        }

        if (state == AcceptRequestState.Success)
            return FriendServiceUtil.Result(FriendOpStatus.Success, "Friend added.");
        if (state == AcceptRequestState.RequestNotFound)
            return FriendServiceUtil.Result(FriendOpStatus.RequestNotFound, "Request not found.");
        return FriendServiceUtil.Result(FriendOpStatus.Error, "Accept failed.");
    }

    public async Task<FriendOpResult> RejectFriendRequestAsync(string fromUid)
    {
        fromUid = FriendServiceUtil.NormalizeUid(fromUid);
        if (string.IsNullOrEmpty(fromUid))
            return FriendServiceUtil.Result(FriendOpStatus.InvalidInput, "Invalid user.");

        try
        {
            var batch = context.Db.StartBatch();
            batch.Delete(FriendFirestoreRefs.IncomingRequestDoc(context.Db, context.Uid, fromUid));
            batch.Delete(FriendFirestoreRefs.OutgoingRequestDoc(context.Db, fromUid, context.Uid));
            await FirebaseTaskTracker.Track(batch.CommitAsync());
            return FriendServiceUtil.Result(FriendOpStatus.Success, "Request rejected.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FriendService] RejectFriendRequestAsync failed: {ex.Message}");
            return FriendServiceUtil.Result(FriendOpStatus.Error, "Reject failed.");
        }
    }

    public async Task<FriendOpResult> CancelFriendRequestAsync(string toUid)
    {
        toUid = FriendServiceUtil.NormalizeUid(toUid);
        if (string.IsNullOrEmpty(toUid))
            return FriendServiceUtil.Result(FriendOpStatus.InvalidInput, "Invalid user.");

        try
        {
            var batch = context.Db.StartBatch();
            batch.Delete(FriendFirestoreRefs.OutgoingRequestDoc(context.Db, context.Uid, toUid));
            batch.Delete(FriendFirestoreRefs.IncomingRequestDoc(context.Db, toUid, context.Uid));
            await FirebaseTaskTracker.Track(batch.CommitAsync());
            return FriendServiceUtil.Result(FriendOpStatus.Success, "Request canceled.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FriendService] CancelFriendRequestAsync failed: {ex.Message}");
            return FriendServiceUtil.Result(FriendOpStatus.Error, "Cancel failed.");
        }
    }

    public async Task<FriendOpResult> RemoveFriendAsync(string friendUid)
    {
        friendUid = FriendServiceUtil.NormalizeUid(friendUid);
        if (string.IsNullOrEmpty(friendUid))
            return FriendServiceUtil.Result(FriendOpStatus.InvalidInput, "Invalid user.");

        try
        {
            var batch = context.Db.StartBatch();
            batch.Delete(FriendFirestoreRefs.FriendDoc(context.Db, context.Uid, friendUid));
            batch.Delete(FriendFirestoreRefs.FriendDoc(context.Db, friendUid, context.Uid));
            batch.Delete(FriendFirestoreRefs.OutgoingRequestDoc(context.Db, context.Uid, friendUid));
            batch.Delete(FriendFirestoreRefs.IncomingRequestDoc(context.Db, context.Uid, friendUid));
            batch.Delete(FriendFirestoreRefs.OutgoingRequestDoc(context.Db, friendUid, context.Uid));
            batch.Delete(FriendFirestoreRefs.IncomingRequestDoc(context.Db, friendUid, context.Uid));
            await FirebaseTaskTracker.Track(batch.CommitAsync());
            return FriendServiceUtil.Result(FriendOpStatus.Success, "Friend removed.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FriendService] RemoveFriendAsync failed: {ex.Message}");
            return FriendServiceUtil.Result(FriendOpStatus.Error, "Remove failed.");
        }
    }

    public async Task<List<FriendLinkData>> GetFriendsAsync(int limit)
    {
        var results = new List<FriendLinkData>();
        QuerySnapshot snapshot;

        try
        {
            snapshot = await FirebaseTaskTracker.Track(
                FriendFirestoreRefs.FriendsCol(context.Db, context.Uid)
                    .Limit(FriendServiceUtil.ClampLimit(limit))
                    .GetSnapshotAsync());
        }
        catch
        {
            return results;
        }

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

    public async Task<List<FriendRequestData>> GetRequestsAsync(string requestCollection, int limit)
    {
        var results = new List<FriendRequestData>();
        Query query = FriendFirestoreRefs.UserDoc(context.Db, context.Uid)
            .Collection(requestCollection)
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

using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;

internal sealed class FriendProfileOps
{
    private readonly FirebaseFirestore db;

    public FriendProfileOps(FirebaseFirestore db)
    {
        this.db = db;
    }

    public async Task<FriendPublicProfile> GetPublicProfileAsync(string targetUid)
    {
        targetUid = FriendServiceUtil.NormalizeUid(targetUid);
        if (string.IsNullOrEmpty(targetUid))
            return null;

        var leaderboardTask = FirebaseTaskTracker.Track(
            db.Collection(FriendServiceConstants.LeaderboardsCollection).Document(targetUid).GetSnapshotAsync());
        var userTask = FirebaseTaskTracker.Track(FriendFirestoreRefs.UserDoc(db, targetUid).GetSnapshotAsync());

        try
        {
            await Task.WhenAll(leaderboardTask, userTask);
        }
        catch
        {
            return null;
        }

        string displayName = FriendServiceUtil.ShortUid(targetUid);
        float clicks = 0f;
        float totalPlaytime = 0f;
        string avatarId = string.Empty;
        string currentBlock = string.Empty;
        string currentLocation = string.Empty;

        var leaderboardSnap = leaderboardTask.Result;
        if (leaderboardSnap.Exists)
        {
            if (leaderboardSnap.TryGetValue("displayName", out string leaderboardName) &&
                !string.IsNullOrWhiteSpace(leaderboardName))
            {
                displayName = leaderboardName;
            }

            if (leaderboardSnap.TryGetValue("avatarId", out string leaderboardAvatarId) &&
                !string.IsNullOrWhiteSpace(leaderboardAvatarId))
            {
                avatarId = leaderboardAvatarId;
            }

            if (leaderboardSnap.TryGetValue("clicks", out double clicksDouble))
                clicks = (float)clicksDouble;
            else if (leaderboardSnap.TryGetValue("clicks", out long clicksLong))
                clicks = clicksLong;

            if (leaderboardSnap.TryGetValue("totalPlaytime", out double playtimeDouble))
                totalPlaytime = (float)playtimeDouble;
            else if (leaderboardSnap.TryGetValue("totalPlaytime", out long playtimeLong))
                totalPlaytime = playtimeLong;
        }

        var userSnap = userTask.Result;
        if (userSnap.Exists)
        {
            if (userSnap.TryGetValue("profile", out UserProfileData profile) &&
                profile != null)
            {
                if (!string.IsNullOrWhiteSpace(profile.displayName))
                    displayName = profile.displayName;
                if (!string.IsNullOrWhiteSpace(profile.avatarId))
                    avatarId = profile.avatarId;
            }

            if (userSnap.TryGetValue("gameplay", out GameplaySaveData gameplay) && gameplay != null)
            {
                currentBlock = gameplay.currentBlock ?? string.Empty;
                currentLocation = gameplay.currentLocation ?? string.Empty;

                if (clicks <= 0f)
                    clicks = gameplay.clicks;
                if (totalPlaytime <= 0f)
                    totalPlaytime = gameplay.totalPlaytime;
            }
        }

        return new FriendPublicProfile
        {
            uid = targetUid,
            displayName = string.IsNullOrWhiteSpace(displayName) ? FriendServiceUtil.ShortUid(targetUid) : displayName,
            avatarId = avatarId ?? string.Empty,
            clicks = clicks,
            totalPlaytime = totalPlaytime,
            currentBlock = currentBlock,
            currentLocation = currentLocation
        };
    }

    public async Task<List<FriendPublicProfile>> GetTopPublicProfilesAsync(int limit)
    {
        var results = new List<FriendPublicProfile>();
        Query query = db.Collection(FriendServiceConstants.LeaderboardsCollection)
            .OrderByDescending("clicks")
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

            var data = doc.ConvertTo<LeaderboardPublicData>();
            if (data == null)
                continue;

            results.Add(MapLeaderboardEntry(doc.Id, data));
        }

        return results;
    }

    public async Task<List<FriendPublicProfile>> SearchPublicProfilesByDisplayNamePrefixAsync(string keyword, int limit)
    {
        var results = new List<FriendPublicProfile>();
        keyword = keyword != null ? keyword.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(keyword))
            return results;

        Query query = db.Collection(FriendServiceConstants.LeaderboardsCollection)
            .OrderBy("displayName")
            .StartAt(keyword)
            .EndAt(keyword + "\uf8ff")
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

            var data = doc.ConvertTo<LeaderboardPublicData>();
            if (data == null)
                continue;

            results.Add(MapLeaderboardEntry(doc.Id, data));
        }

        return results;
    }

    private static FriendPublicProfile MapLeaderboardEntry(string uid, LeaderboardPublicData data)
    {
        return new FriendPublicProfile
        {
            uid = uid,
            displayName = string.IsNullOrWhiteSpace(data.displayName)
                ? FriendServiceUtil.ShortUid(uid)
                : data.displayName,
            avatarId = data.avatarId ?? string.Empty,
            clicks = data.clicks,
            totalPlaytime = data.totalPlaytime,
            currentBlock = string.Empty,
            currentLocation = string.Empty
        };
    }
}

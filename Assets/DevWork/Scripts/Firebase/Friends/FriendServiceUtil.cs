using System;
using System.Threading.Tasks;
using Firebase.Firestore;

internal static class FriendServiceUtil
{
    public static FriendOpResult Result(FriendOpStatus status, string message, int diamondsGranted = 0)
    {
        return new FriendOpResult
        {
            status = status,
            message = message,
            diamondsGranted = diamondsGranted
        };
    }

    public static int ClampLimit(int limit)
    {
        if (limit <= 0)
            return 1;
        return Math.Min(limit, FriendServiceConstants.MaxQueryLimit);
    }

    public static string NormalizeUid(string uid)
    {
        return uid != null ? uid.Trim() : string.Empty;
    }

    public static string GetUtcDayKey()
    {
        return DateTime.UtcNow.ToString("yyyyMMdd");
    }

    public static string ShortUid(string uid)
    {
        if (string.IsNullOrEmpty(uid))
            return "Player";
        if (uid.Length <= 8)
            return uid;
        return $"{uid.Substring(0, 4)}...{uid.Substring(uid.Length - 4)}";
    }

    public static string ReadString(DocumentSnapshot snapshot, string field, string fallback)
    {
        if (snapshot != null && snapshot.Exists &&
            snapshot.TryGetValue(field, out string value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return fallback;
    }

    public static int ReadInt(DocumentSnapshot snapshot, string field, int fallback)
    {
        if (snapshot == null || !snapshot.Exists)
            return fallback;

        if (snapshot.TryGetValue(field, out int intValue))
            return intValue;
        if (snapshot.TryGetValue(field, out long longValue))
            return (int)longValue;
        if (snapshot.TryGetValue(field, out double doubleValue))
            return (int)doubleValue;

        return fallback;
    }

    public static async Task<string> ResolveDisplayNameForUidAsync(FirebaseFirestore db, string uid)
    {
        uid = NormalizeUid(uid);
        if (string.IsNullOrEmpty(uid))
            return "Player";

        string fallback = ShortUid(uid);
        if (db == null)
            return fallback;

        try
        {
            var leaderboardRef = db.Collection(FriendServiceConstants.LeaderboardsCollection).Document(uid);
            var leaderboardSnap = await FirebaseTaskTracker.Track(leaderboardRef.GetSnapshotAsync());
            if (leaderboardSnap.Exists &&
                leaderboardSnap.TryGetValue("displayName", out string leaderboardName) &&
                !string.IsNullOrWhiteSpace(leaderboardName))
            {
                return leaderboardName.Trim();
            }

            var userSnap = await FirebaseTaskTracker.Track(FriendFirestoreRefs.UserDoc(db, uid).GetSnapshotAsync());
            if (userSnap.Exists &&
                userSnap.TryGetValue("profile", out UserProfileData profile) &&
                profile != null &&
                !string.IsNullOrWhiteSpace(profile.displayName))
            {
                return profile.displayName.Trim();
            }
        }
        catch
        {
        }

        return fallback;
    }
}

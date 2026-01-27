using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;

public enum LeaderboardMetric
{
    Clicks,
    TotalPlaytime
}

public class LeaderboardEntry
{
    public string uid;
    public string displayName;
    public float value;
}

public static class LeaderboardService
{
    private const int MaxLimit = 200;

    public static Task<List<LeaderboardEntry>> GetTopClicks(int limit = 50)
    {
        return GetTop(LeaderboardMetric.Clicks, limit);
    }

    public static Task<List<LeaderboardEntry>> GetTopTotalPlaytime(int limit = 50)
    {
        return GetTop(LeaderboardMetric.TotalPlaytime, limit);
    }

    public static async Task<List<LeaderboardEntry>> GetTop(LeaderboardMetric metric, int limit = 50)
    {
        var results = new List<LeaderboardEntry>();
        var bootstrap = FirebaseBootstrap.Ins;
        if (bootstrap == null) return results;

        try
        {
            await bootstrap.ReadyTask;
        }
        catch
        {
            return results;
        }

        if (!bootstrap.IsReady || bootstrap.Db == null) return results;

        if (limit <= 0) limit = 1;
        if (limit > MaxLimit) limit = MaxLimit;

        string field = metric == LeaderboardMetric.Clicks
            ? "clicks"
            : "totalPlaytime";

        Query query = bootstrap.Db
            .Collection("leaderboards")
            .OrderByDescending(field)
            .Limit(limit);

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
            if (!doc.Exists) continue;

            var data = doc.ConvertTo<LeaderboardPublicData>();
            if (data == null)
                continue;

            float value = metric == LeaderboardMetric.Clicks
                ? data.clicks
                : data.totalPlaytime;

            string displayName = string.IsNullOrWhiteSpace(data.displayName)
                ? ShortUid(doc.Id)
                : data.displayName;

            results.Add(new LeaderboardEntry
            {
                uid = doc.Id,
                displayName = displayName,
                value = value
            });
        }

        return results;
    }

    private static string ShortUid(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return "Player";
        if (uid.Length <= 8) return uid;
        return $"{uid.Substring(0, 4)}...{uid.Substring(uid.Length - 4)}";
    }
}

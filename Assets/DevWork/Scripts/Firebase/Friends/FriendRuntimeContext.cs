using System.Threading.Tasks;
using Firebase.Firestore;

internal sealed class FriendRuntimeContext
{
    public FirebaseFirestore Db { get; }
    public string Uid { get; }
    public string DisplayName { get; }

    public FriendRuntimeContext(FirebaseFirestore db, string uid, string displayName)
    {
        Db = db;
        Uid = uid;
        DisplayName = displayName;
    }
}

internal static class FriendContextResolver
{
    public static async Task<FriendRuntimeContext> TryResolveAsync()
    {
        var bootstrap = FirebaseBootstrap.Ins;
        if (bootstrap == null)
            return null;

        try
        {
            await bootstrap.ReadyTask;
        }
        catch
        {
            return null;
        }

        if (!bootstrap.IsReady || bootstrap.Db == null || string.IsNullOrWhiteSpace(bootstrap.Uid))
            return null;

        return new FriendRuntimeContext(
            bootstrap.Db,
            bootstrap.Uid,
            ResolveSelfDisplayName(bootstrap.Uid));
    }

    public static async Task<FirebaseFirestore> TryResolveDbAsync()
    {
        var bootstrap = FirebaseBootstrap.Ins;
        if (bootstrap == null)
            return null;

        try
        {
            await bootstrap.ReadyTask;
        }
        catch
        {
            return null;
        }

        if (!bootstrap.IsReady || bootstrap.Db == null)
            return null;

        return bootstrap.Db;
    }

    private static string ResolveSelfDisplayName(string fallbackUid)
    {
        string displayName = DataSaver.Ins != null ? DataSaver.Ins.DisplayName : string.Empty;
        if (string.IsNullOrWhiteSpace(displayName))
            return FriendServiceUtil.ShortUid(fallbackUid);
        return displayName.Trim();
    }
}

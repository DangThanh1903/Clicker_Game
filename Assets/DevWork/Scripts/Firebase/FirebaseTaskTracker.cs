using System.Threading;
using System.Threading.Tasks;

public static class FirebaseTaskTracker
{
    private static int pendingCount;

    public static bool HasPending => pendingCount > 0;

    public static Task Track(Task task)
    {
        if (task == null) return task;
        Interlocked.Increment(ref pendingCount);
        task.ContinueWith(_ => Interlocked.Decrement(ref pendingCount), TaskScheduler.Default);
        return task;
    }

    public static Task<T> Track<T>(Task<T> task)
    {
        if (task == null) return task;
        Interlocked.Increment(ref pendingCount);
        task.ContinueWith(_ => Interlocked.Decrement(ref pendingCount), TaskScheduler.Default);
        return task;
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;

public class FirebaseBootstrap : MonoBehaviour
{
    public static FirebaseBootstrap Ins { get; private set; }

    public FirebaseAuth Auth { get; private set; }
    public FirebaseFirestore Db { get; private set; }

    public string Uid => Auth?.CurrentUser?.UserId;

    public enum FirebaseInitState
    {
        Uninitialized,
        Initializing,
        Ready,
        Failed
    }

    public FirebaseInitState State { get; private set; } = FirebaseInitState.Uninitialized;
    public bool IsReady => State == FirebaseInitState.Ready;
    public bool IsFailed => State == FirebaseInitState.Failed;
    public DependencyStatus DependencyStatus { get; private set; } = DependencyStatus.UnavailableOther;
    public Exception InitError { get; private set; }
    public Task ReadyTask => readyTcs?.Task ?? Task.CompletedTask;

    private TaskCompletionSource<bool> readyTcs;
    private bool initStarted;

    [Header("Quit Handling")]
    [SerializeField] private float quitWaitTimeout = 2f;
    private bool quitRequested;
    private Coroutine quitRoutine;

    [Header("Diagnostics")]
    [SerializeField] private bool runDiagnosticsOnReady = false;
    [SerializeField] private bool logFirebaseConfig = true;
    [SerializeField] private bool pingFirestoreWrite = true;
    [SerializeField] private float pingTimeoutSeconds = 8f;

    void Awake()
    {
        if (Ins && Ins != this) { Destroy(gameObject); return; }
        Ins = this;
        DontDestroyOnLoad(gameObject);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 120;
        AnalyticsManager.EnsureExists();
        StartInitIfNeeded();
    }

    void OnEnable()
    {
        Application.wantsToQuit += OnWantsToQuit;
    }

    void OnDisable()
    {
        Application.wantsToQuit -= OnWantsToQuit;
    }

    bool OnWantsToQuit()
    {
        if (quitRequested) return true;

        quitRequested = true;
        if (DataSaver.Ins != null)
            DataSaver.Ins.SaveDataFn(true);

        if (!FirebaseTaskTracker.HasPending)
            return true;

        if (quitRoutine == null)
            quitRoutine = StartCoroutine(CoWaitAndQuit());

        return false;
    }

    IEnumerator CoWaitAndQuit()
    {
        float t = 0f;
        while (FirebaseTaskTracker.HasPending && t < quitWaitTimeout)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void StartInitIfNeeded()
    {
        if (initStarted) return;
        initStarted = true;
        State = FirebaseInitState.Initializing;
        readyTcs = new TaskCompletionSource<bool>();
        StartCoroutine(CoInitialize());
    }

    IEnumerator CoInitialize()
    {
        var depTask = FirebaseTaskTracker.Track(FirebaseApp.CheckAndFixDependenciesAsync());
        yield return new WaitUntil(() => depTask.IsCompleted);

        if (depTask.Exception != null)
        {
            FailInit(depTask.Exception);
            yield break;
        }

        DependencyStatus = depTask.Result;
        if (DependencyStatus != DependencyStatus.Available)
        {
            FailInit(new Exception($"Firebase deps not available: {DependencyStatus}"));
            yield break;
        }

        Auth = FirebaseAuth.DefaultInstance;
        Db = FirebaseFirestore.DefaultInstance;

        if (Auth.CurrentUser == null)
        {
            var signTask = FirebaseTaskTracker.Track(Auth.SignInAnonymouslyAsync());
            yield return new WaitUntil(() => signTask.IsCompleted);

            if (signTask.Exception != null)
            {
                FailInit(signTask.Exception);
                yield break;
            }
        }

        State = FirebaseInitState.Ready;
        readyTcs.TrySetResult(true);
        DevLog.Log($"[OK] Firebase ready. uid={Uid}");

        if (runDiagnosticsOnReady)
            StartCoroutine(CoRunDiagnostics());
    }

    void FailInit(Exception ex)
    {
        InitError = ex;
        State = FirebaseInitState.Failed;
        readyTcs.TrySetException(ex);
        Debug.LogError($"[Error] Firebase init failed: {ex}");
    }

    IEnumerator CoRunDiagnostics()
    {
        if (logFirebaseConfig)
            LogFirebaseConfig();

        if (pingFirestoreWrite)
            yield return StartCoroutine(CoPingFirestore());
    }

    void LogFirebaseConfig()
    {
        try
        {
            var app = FirebaseApp.DefaultInstance;
            var opt = app.Options;
            string dbUrl = opt.DatabaseUrl != null ? opt.DatabaseUrl.ToString() : "null";
            DevLog.Log($"Firebase config: ProjectId={opt.ProjectId}, AppId={opt.AppId}, StorageBucket={opt.StorageBucket}, DatabaseUrl={dbUrl}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Warn] Firebase config log failed: {ex}");
        }
    }

    IEnumerator CoPingFirestore()
    {
        if (Db == null)
        {
            Debug.LogWarning("[Warn] Firestore ping skipped: Db not ready.");
            yield break;
        }

        string uid = Auth?.CurrentUser?.UserId;
        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogWarning("[Warn] Firestore ping skipped: uid missing.");
            yield break;
        }

        var doc = Db.Collection("users").Document(uid).Collection("debug").Document("ping");
        var payload = new Dictionary<string, object>
        {
            ["ts"] = Timestamp.GetCurrentTimestamp(),
            ["device"] = SystemInfo.deviceModel
        };

        var task = FirebaseTaskTracker.Track(doc.SetAsync(payload, SetOptions.MergeAll));

        float timeout = Mathf.Max(0f, pingTimeoutSeconds);
        if (timeout <= 0f)
        {
            yield return new WaitUntil(() => task.IsCompleted);
        }
        else
        {
            float t = 0f;
            while (!task.IsCompleted && t < timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!task.IsCompleted)
            {
                Debug.LogWarning($"[Warn] Firestore ping timed out after {timeout:F1}s.");
                yield break;
            }
        }

        if (task.Exception != null)
            Debug.LogError($"[Error] Firestore ping failed: {task.Exception}");
        else
            DevLog.Log("[OK] Firestore ping write OK.");
    }
}


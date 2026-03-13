using System;
using System.Collections;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Login : MonoBehaviour
{
    public static Login Ins { get; private set; }

    [Header("Scene to load after data")]
    [SerializeField] private string gameSceneName = "SampleScene";

    [Header("Access Gate")]
    [SerializeField] private bool requireInternetToPlay = true;
    [SerializeField] private bool requireVersionGate = true;
    [SerializeField, Min(0.1f)] private float internetPollIntervalSeconds = 1f;
    [SerializeField, Min(1f)] private float versionGateRetryDelaySeconds = 5f;
    [SerializeField, Min(1f)] private float versionGateRequestTimeout = 8f;

    [Header("Version Gate (Firestore)")]
    [SerializeField] private string versionConfigCollection = "configs";
    [SerializeField] private string versionConfigDocument = "app";
    [SerializeField] private string minSupportedVersionField = "min_supported_version";
    [SerializeField] private string storeUrlField = "store_url";
    [SerializeField] private string fallbackStoreUrl = "";
    [SerializeField] private bool autoOpenStoreWhenUnsupported = false;

    [Header("Timeouts (seconds)")]
    [SerializeField] private float waitBootstrapTimeout = 5f;
    [SerializeField] private float waitFirebaseTimeout = 10f;
    [SerializeField] private float waitFirebaseHardTimeout = 30f;
    [SerializeField] private float loadDataTimeout = 10f;
    [SerializeField] private float retryLoadDelaySeconds = 3f;
    [SerializeField] private int maxCloudLoadAttempts = 3;

    [Header("Quest Load")]
    [SerializeField] private bool requireQuestReady = true;
    [SerializeField] private float waitQuestTimeout = 10f;

    [Header("Startup Recovery")]
    [SerializeField] private bool allowOfflineFallbackOnStartupFailure = true;
    [SerializeField] private bool allowVersionGateFailOpenOnRequestError = true;
    [SerializeField] private bool allowSkipQuestGateOnTimeout = true;
    [SerializeField, Min(1f)] private float maxVersionGateWaitSeconds = 20f;

    [Header("Loading UI")]
    [SerializeField] private Image progressFillImage;
    [SerializeField] private Text progressText;

    private int totalSteps;
    private int completedSteps;
    private bool stepBootstrap;
    private bool stepFirebase;
    private bool stepInternetGate;
    private bool stepVersionGate;
    private bool stepDataSaver;
    private bool stepGameplay;
    private bool stepInventory;
    private bool stepQuest;

    private sealed class CoroutineRunState
    {
        public bool IsComplete;
        public Exception Exception;
    }

    void Awake()
    {
        if (Ins != null && Ins != this)
        {
            Destroy(gameObject);
            return;
        }

        Ins = this;
    }

    void Start()
    {
        DevLog.Log("Login.Start");
        totalSteps = 5 +
                     (requireInternetToPlay ? 1 : 0) +
                     (requireVersionGate ? 1 : 0) +
                     (requireQuestReady ? 1 : 0);

        UpdateProgress(0f, "Loading");
        StartCoroutine(Flow());
    }

    IEnumerator Flow()
    {
        if (requireInternetToPlay)
        {
            yield return WaitForInternet();
            MarkStep(ref stepInternetGate, "Internet");
        }

        // 1) Wait FirebaseBootstrap instance
        DevLog.Log("Waiting FirebaseBootstrap...");
        yield return WaitUntilOrTimeout(
            () => FirebaseBootstrap.Ins != null,
            waitBootstrapTimeout,
            "FirebaseBootstrap missing (timeout)");

        if (FirebaseBootstrap.Ins == null)
        {
            yield return RecoverToLocalAndLoadScene("FirebaseBootstrap missing.");
            yield break;
        }

        MarkStep(ref stepBootstrap, "Firebase bootstrap");

        bool firebaseReady = false;
        yield return WaitForFirebaseReadyOrTimeout(ok => firebaseReady = ok);
        if (!firebaseReady)
        {
            yield return RecoverToLocalAndLoadScene("Firebase not ready.");
            yield break;
        }

        MarkStep(ref stepFirebase, "Firebase ready");

        string uid = FirebaseBootstrap.Ins.Auth.CurrentUser.UserId;
        DevLog.Log($"Firebase ready. uid={uid}");

        if (requireVersionGate)
        {
            bool versionOk = false;
            yield return CheckVersionGateBlocking(ok => versionOk = ok);
            if (!versionOk)
            {
                Debug.LogError("Unsupported app version. Stay on splash.");
                yield break;
            }

            MarkStep(ref stepVersionGate, "Version");
        }

        // 2) Wait DataSaver
        DevLog.Log("Waiting DataSaver...");
        yield return WaitUntilOrTimeout(
            () => DataSaver.Ins != null,
            5f,
            "DataSaver not found (timeout)");

        if (DataSaver.Ins == null)
        {
            Debug.LogError("DataSaver missing. Cannot load cloud data.");
            yield break;
        }

        MarkStep(ref stepDataSaver, "Data saver");

        // 3) Load gameplay + inventories (prefer cloud, fallback local if needed)
        int attempts = Mathf.Max(1, maxCloudLoadAttempts);
        for (int i = 1; i <= attempts; i++)
        {
            DevLog.Log("Loading gameplay...");
            bool gameplayOk = false;
            yield return RunCoroutineWithTimeout(
                DataSaver.Ins.LoadGameplay(uid, ok => gameplayOk = ok),
                loadDataTimeout,
                "LoadGameplay timeout");

            DevLog.Log("Loading inventories...");
            bool inventoryOk = false;
            yield return RunCoroutineWithTimeout(
                DataSaver.Ins.LoadAllInventories(uid, ok => inventoryOk = ok),
                loadDataTimeout,
                "LoadAllInventories timeout");

            if (gameplayOk && inventoryOk)
            {
                DataSaver.Ins.MarkInitialLoadComplete(true);
                MarkStep(ref stepGameplay, "Gameplay");
                MarkStep(ref stepInventory, "Inventory");
                yield return FinishStartupAfterDataLoad();
                yield break;
            }

            Debug.LogWarning($"Cloud load incomplete (attempt {i}/{attempts}).");
            if (i < attempts)
            {
                float wait = Mathf.Max(0.1f, retryLoadDelaySeconds);
                yield return new WaitForSecondsRealtime(wait);
            }
        }

        yield return RecoverToLocalAndLoadScene("Cloud load failed.");
    }

    IEnumerator WaitForInternet()
    {
        float poll = Mathf.Max(0.1f, internetPollIntervalSeconds);

        while (Application.internetReachability == NetworkReachability.NotReachable)
        {
            UpdateProgress(GetCurrentProgress(), "Internet required");
            yield return new WaitForSecondsRealtime(poll);
        }
    }

    IEnumerator CheckVersionGateBlocking(System.Action<bool> onDone)
    {
        bool storeOpened = false;
        float retryDelay = Mathf.Max(1f, versionGateRetryDelaySeconds);
        float timeout = Mathf.Max(1f, versionGateRequestTimeout);
        float maxWait = Mathf.Max(timeout, maxVersionGateWaitSeconds);
        float totalWait = 0f;

        while (true)
        {
            if (requireInternetToPlay &&
                Application.internetReachability == NetworkReachability.NotReachable)
            {
                UpdateProgress(GetCurrentProgress(), "Internet required");
                float wait = Mathf.Max(0.1f, internetPollIntervalSeconds);
                totalWait += wait;
                if (TryResolveVersionGateRequestFailure(totalWait, "No internet reachability", onDone))
                    yield break;
                yield return new WaitForSecondsRealtime(wait);
                continue;
            }

            var bootstrap = FirebaseBootstrap.Ins;
            if (bootstrap == null || bootstrap.Db == null)
            {
                UpdateProgress(GetCurrentProgress(), "Server unavailable");
                totalWait += retryDelay;
                if (TryResolveVersionGateRequestFailure(totalWait, "Firebase DB unavailable", onDone))
                    yield break;
                yield return new WaitForSecondsRealtime(retryDelay);
                continue;
            }

            var docRef = bootstrap.Db.Collection(versionConfigCollection).Document(versionConfigDocument);
            var getTask = FirebaseTaskTracker.Track(docRef.GetSnapshotAsync());

            float t = 0f;
            while (!getTask.IsCompleted)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                totalWait += dt;
                if (t >= timeout || totalWait >= maxWait)
                    break;
                yield return null;
            }

            if (!getTask.IsCompleted)
            {
                Debug.LogWarning("Version gate request timed out.");
                UpdateProgress(GetCurrentProgress(), "Checking version");
                if (TryResolveVersionGateRequestFailure(totalWait, "Version gate request timed out", onDone))
                    yield break;
                totalWait += retryDelay;
                if (TryResolveVersionGateRequestFailure(totalWait, "Version gate request timed out", onDone))
                    yield break;
                yield return new WaitForSecondsRealtime(retryDelay);
                continue;
            }

            if (getTask.Exception != null)
            {
                Debug.LogWarning($"Version gate request failed: {getTask.Exception.Message}");
                UpdateProgress(GetCurrentProgress(), "Checking version");
                totalWait += retryDelay;
                if (TryResolveVersionGateRequestFailure(totalWait, "Version gate request failed", onDone))
                    yield break;
                yield return new WaitForSecondsRealtime(retryDelay);
                continue;
            }

            DocumentSnapshot snap = getTask.Result;
            if (snap == null || !snap.Exists)
            {
                Debug.LogWarning("Version gate config document is missing.");
                UpdateProgress(GetCurrentProgress(), "Checking version");
                totalWait += retryDelay;
                if (TryResolveVersionGateRequestFailure(totalWait, "Version gate config missing", onDone))
                    yield break;
                yield return new WaitForSecondsRealtime(retryDelay);
                continue;
            }

            if (!snap.TryGetValue(minSupportedVersionField, out string minSupported) ||
                string.IsNullOrWhiteSpace(minSupported))
            {
                Debug.LogWarning($"Version gate field '{minSupportedVersionField}' is missing.");
                UpdateProgress(GetCurrentProgress(), "Checking version");
                totalWait += retryDelay;
                if (TryResolveVersionGateRequestFailure(totalWait, "Version gate field missing", onDone))
                    yield break;
                yield return new WaitForSecondsRealtime(retryDelay);
                continue;
            }

            string current = Application.version;
            if (CompareVersions(current, minSupported) < 0)
            {
                string storeUrl = fallbackStoreUrl;
                if (snap.TryGetValue(storeUrlField, out string remoteStoreUrl) &&
                    !string.IsNullOrWhiteSpace(remoteStoreUrl))
                {
                    storeUrl = remoteStoreUrl;
                }

                UpdateProgress(GetCurrentProgress(), "Update required");
                Debug.LogError($"App version '{current}' is below min supported '{minSupported}'.");

                if (autoOpenStoreWhenUnsupported &&
                    !storeOpened &&
                    !string.IsNullOrWhiteSpace(storeUrl))
                {
                    storeOpened = true;
                    Application.OpenURL(storeUrl);
                }

                onDone?.Invoke(false);
                yield break;
            }

            onDone?.Invoke(true);
            yield break;
        }
    }

    void LoadGameScene()
    {
        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError($"Scene '{gameSceneName}' is not available in Build Settings.");
            UpdateProgress(GetCurrentProgress(), "Scene missing");
            return;
        }

        DevLog.Log($"Loading scene: {gameSceneName}");
        SceneManager.LoadScene(gameSceneName);
    }

    IEnumerator WaitUntilOrTimeout(System.Func<bool> predicate, float timeout, string timeoutMsg)
    {
        float t = 0f;
        while (!predicate())
        {
            t += Time.unscaledDeltaTime;
            if (t >= timeout)
            {
                Debug.LogWarning(timeoutMsg);
                yield break;
            }

            yield return null;
        }
    }

    IEnumerator WaitForFirebaseReadyOrTimeout(System.Action<bool> onDone)
    {
        float softTimeout = Mathf.Max(0f, waitFirebaseTimeout);
        float hardTimeout = Mathf.Max(softTimeout, waitFirebaseHardTimeout);
        bool warned = false;
        float t = 0f;

        while (!FirebaseBootstrap.Ins.IsReady)
        {
            if (FirebaseBootstrap.Ins.IsFailed)
            {
                Debug.LogError($"Firebase init failed: {FirebaseBootstrap.Ins.InitError}");
                onDone?.Invoke(false);
                yield break;
            }

            t += Time.unscaledDeltaTime;
            if (!warned && softTimeout > 0f && t >= softTimeout)
            {
                warned = true;
                Debug.LogWarning("FirebaseBootstrap not ready (still waiting)");
            }

            if (hardTimeout > 0f && t >= hardTimeout)
            {
                Debug.LogWarning("FirebaseBootstrap not ready (hard timeout)");
                onDone?.Invoke(false);
                yield break;
            }

            yield return null;
        }

        onDone?.Invoke(true);
    }

    IEnumerator WaitForQuestReadyOrTimeout(System.Action<bool> onDone)
    {
        float timeout = Mathf.Max(0f, waitQuestTimeout);
        float t = 0f;

        while (true)
        {
            if (QuestManager.Ins != null && QuestManager.Ins.IsReady)
            {
                onDone?.Invoke(true);
                yield break;
            }

            t += Time.unscaledDeltaTime;
            if (timeout > 0f && t >= timeout)
            {
                Debug.LogWarning("QuestManager not ready (timeout)");
                onDone?.Invoke(false);
                yield break;
            }

            yield return null;
        }
    }

    IEnumerator LoadLocalFallback()
    {
        yield return WaitUntilOrTimeout(
            () => DataSaver.Ins != null,
            5f,
            "DataSaver not found (timeout)");

        if (DataSaver.Ins == null)
        {
            Debug.LogError("DataSaver missing. Cannot load local cache.");
            yield break;
        }

        bool localOk = false;
        yield return RunCoroutineWithTimeout(
            DataSaver.Ins.LoadFromLocalCache(ok => localOk = ok),
            loadDataTimeout,
            "LoadLocalCache timeout");

        if (!localOk)
            Debug.LogWarning("Local cache unavailable. Continue with default startup state.");

        DataSaver.Ins.MarkInitialLoadComplete(true);
        if (localOk)
        {
            MarkStep(ref stepGameplay, "Gameplay (local)");
            MarkStep(ref stepInventory, "Inventory (local)");
        }
        else
        {
            MarkStep(ref stepGameplay, "Gameplay (default)");
            MarkStep(ref stepInventory, "Inventory (default)");
        }
    }

    IEnumerator RunCoroutineWithTimeout(IEnumerator routine, float timeout, string timeoutMsg)
    {
        if (routine == null)
        {
            Debug.LogWarning($"{timeoutMsg} (routine missing)");
            yield break;
        }

        var state = new CoroutineRunState();
        Coroutine nested = StartCoroutine(RunNestedCoroutine(routine, state));
        float t = 0f;
        while (!state.IsComplete)
        {
            if (timeout > 0f)
            {
                t += Time.unscaledDeltaTime;
                if (t >= timeout)
                {
                    if (nested != null)
                        StopCoroutine(nested);
                    Debug.LogWarning(timeoutMsg);
                    yield break;
                }
            }

            yield return null;
        }

        if (state.Exception != null)
            Debug.LogError(state.Exception);
    }

    IEnumerator RunNestedCoroutine(IEnumerator routine, CoroutineRunState state)
    {
        while (true)
        {
            object current;
            try
            {
                if (!routine.MoveNext())
                    break;

                current = routine.Current;
            }
            catch (Exception ex)
            {
                state.Exception = ex;
                break;
            }

            yield return current;
        }

        state.IsComplete = true;
    }

    IEnumerator RecoverToLocalAndLoadScene(string reason)
    {
        if (!allowOfflineFallbackOnStartupFailure)
        {
            Debug.LogError($"{reason} Startup blocked.");
            UpdateProgress(GetCurrentProgress(), "Startup failed");
            yield break;
        }

        Debug.LogWarning($"{reason} Falling back to local/default data.");
        UpdateProgress(GetCurrentProgress(), "Loading local");
        yield return LoadLocalFallback();
        yield return FinishStartupAfterDataLoad();
    }

    IEnumerator FinishStartupAfterDataLoad()
    {
        if (requireQuestReady)
        {
            bool questOk = false;
            yield return WaitForQuestReadyOrTimeout(ok => questOk = ok);
            if (!questOk)
            {
                if (!allowSkipQuestGateOnTimeout)
                {
                    Debug.LogError("QuestManager not ready. Startup blocked.");
                    UpdateProgress(GetCurrentProgress(), "Quest unavailable");
                    yield break;
                }

                Debug.LogWarning("QuestManager not ready. Continue without blocking startup.");
            }
            else
            {
                MarkStep(ref stepQuest, "Quests");
            }
        }

        DevLog.Log("Load flow done -> Load scene");
        UpdateProgress(1f, "Done");
        LoadGameScene();
    }

    private bool TryResolveVersionGateRequestFailure(float elapsedSeconds, string reason, Action<bool> onDone)
    {
        if (elapsedSeconds < Mathf.Max(versionGateRequestTimeout, maxVersionGateWaitSeconds))
            return false;

        if (allowVersionGateFailOpenOnRequestError)
        {
            Debug.LogWarning($"Version gate bypassed after {elapsedSeconds:F1}s: {reason}.");
            onDone?.Invoke(true);
        }
        else
        {
            Debug.LogError($"Version gate failed after {elapsedSeconds:F1}s: {reason}.");
            onDone?.Invoke(false);
        }

        return true;
    }

    private void MarkStep(ref bool flag, string label)
    {
        if (flag) return;

        flag = true;
        completedSteps = Mathf.Min(completedSteps + 1, totalSteps);
        float progress = totalSteps > 0 ? (float)completedSteps / totalSteps : 1f;
        UpdateProgress(progress, label);
    }

    private void UpdateProgress(float progress, string label)
    {
        float clamped = Mathf.Clamp01(progress);

        if (progressFillImage != null)
            progressFillImage.fillAmount = clamped;

        if (progressText != null)
        {
            int pct = Mathf.RoundToInt(clamped * 100f);
            progressText.text = string.IsNullOrEmpty(label) ? $"{pct}%" : $"{label} {pct}%";
        }
    }

    private float GetCurrentProgress()
    {
        return totalSteps > 0 ? (float)completedSteps / totalSteps : 0f;
    }

    private static int CompareVersions(string current, string minimum)
    {
        if (string.IsNullOrWhiteSpace(current)) current = "0";
        if (string.IsNullOrWhiteSpace(minimum)) minimum = "0";

        string[] a = current.Split('.');
        string[] b = minimum.Split('.');
        int len = Mathf.Max(a.Length, b.Length);

        for (int i = 0; i < len; i++)
        {
            int ai = ParseVersionPart(a, i);
            int bi = ParseVersionPart(b, i);
            if (ai < bi) return -1;
            if (ai > bi) return 1;
        }

        return 0;
    }

    private static int ParseVersionPart(string[] parts, int index)
    {
        if (parts == null || index < 0 || index >= parts.Length)
            return 0;

        if (int.TryParse(parts[index], out int parsed))
            return parsed;

        return 0;
    }
}


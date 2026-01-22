using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Login : MonoBehaviour
{
    public static Login Ins { get; private set; }

    [Header("Scene to load after data")]
    [SerializeField] private string gameSceneName = "SampleScene";

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

    [Header("Loading UI")]
    [SerializeField] private Image progressFillImage;
    [SerializeField] private Text progressText;

    private int totalSteps;
    private int completedSteps;
    private bool stepBootstrap;
    private bool stepFirebase;
    private bool stepDataSaver;
    private bool stepGameplay;
    private bool stepInventory;
    private bool stepQuest;

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
        Debug.Log("✅ Login.Start()");
        totalSteps = 5 + (requireQuestReady ? 1 : 0);
        UpdateProgress(0f, "Loading");
        StartCoroutine(Flow());
    }

    IEnumerator Flow()
    {
        // 1) Wait FirebaseBootstrap instance
        Debug.Log("⏳ Waiting FirebaseBootstrap...");
        yield return WaitUntilOrTimeout(
            () => FirebaseBootstrap.Ins != null,
            waitBootstrapTimeout,
            "FirebaseBootstrap missing (timeout)"
        );

        if (FirebaseBootstrap.Ins == null)
        {
            Debug.LogError("❌ FirebaseBootstrap missing. Stay on splash.");
            yield break;
        }
        MarkStep(ref stepBootstrap, "Firebase bootstrap");

        bool firebaseReady = false;
        yield return WaitForFirebaseReadyOrTimeout(ok => firebaseReady = ok);
        if (!firebaseReady)
        {
            Debug.LogWarning("⚠️ Firebase not ready. Fallback to local cache.");
            yield return LoadLocalFallback();
            UpdateProgress(1f, "Done");
            LoadGameScene();
            yield break;
        }
        MarkStep(ref stepFirebase, "Firebase ready");

        string uid = FirebaseBootstrap.Ins.Auth.CurrentUser.UserId;
        Debug.Log($"✅ Firebase ready. uid={uid}");

        // 2) Wait DataSaver
        Debug.Log("⏳ Waiting DataSaver...");
        yield return WaitUntilOrTimeout(
            () => DataSaver.Ins != null,
            5f,
            "DataSaver not found (timeout)"
        );

        if (DataSaver.Ins == null)
        {
            Debug.LogError("❌ DataSaver missing. Cannot load cloud data.");
            yield break;
        }
        MarkStep(ref stepDataSaver, "Data saver");

        // 3) Load gameplay + inventories (prefer cloud, fallback local if needed)
        int attempts = Mathf.Max(1, maxCloudLoadAttempts);
        for (int i = 1; i <= attempts; i++)
        {
            Debug.Log("⏳ Loading gameplay...");
            bool gameplayOk = false;
            yield return RunCoroutineWithTimeout(
                DataSaver.Ins.LoadGameplay(uid, ok => gameplayOk = ok),
                loadDataTimeout,
                "LoadGameplay timeout"
            );

            Debug.Log("⏳ Loading inventories...");
            bool inventoryOk = false;
            yield return RunCoroutineWithTimeout(
                DataSaver.Ins.LoadAllInventories(uid, ok => inventoryOk = ok),
                loadDataTimeout,
                "LoadAllInventories timeout"
            );

            if (gameplayOk && inventoryOk)
            {
                DataSaver.Ins.MarkInitialLoadComplete(true);
                MarkStep(ref stepGameplay, "Gameplay");
                MarkStep(ref stepInventory, "Inventory");
                if (requireQuestReady)
                {
                    bool questOk = false;
                    yield return WaitForQuestReadyOrTimeout(ok => questOk = ok);
                    if (!questOk)
                    {
                        Debug.LogError("❌ QuestManager not ready. Stay on splash.");
                        yield break;
                    }
                    MarkStep(ref stepQuest, "Quests");
                }
                Debug.Log("✅ Cloud load ok -> Load scene");
                UpdateProgress(1f, "Done");
                LoadGameScene();
                yield break;
            }

            Debug.LogWarning($"⚠️ Cloud load incomplete (attempt {i}/{attempts}).");
            if (i < attempts)
            {
                float wait = Mathf.Max(0.1f, retryLoadDelaySeconds);
                yield return new WaitForSecondsRealtime(wait);
            }
        }

        Debug.LogWarning("⚠️ Cloud load failed. Fallback to local cache.");
        yield return LoadLocalFallback();
        if (requireQuestReady)
        {
            bool questOk = false;
            yield return WaitForQuestReadyOrTimeout(ok => questOk = ok);
            if (!questOk)
            {
                Debug.LogError("❌ QuestManager not ready. Stay on splash.");
                yield break;
            }
            MarkStep(ref stepQuest, "Quests");
        }
        Debug.Log("✅ Load flow done -> Load scene");
        UpdateProgress(1f, "Done");
        LoadGameScene();
    }

    void LoadGameScene()
    {
        // Check scene exists in build settings
        int idx = SceneManager.GetSceneByName(gameSceneName).buildIndex;
        Debug.Log($"➡️ Loading scene: {gameSceneName}");

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
                Debug.LogWarning($"⚠️ {timeoutMsg}");
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
                Debug.LogError($"❌ Firebase init failed: {FirebaseBootstrap.Ins.InitError}");
                yield break;
            }

            t += Time.unscaledDeltaTime;
            if (!warned && softTimeout > 0f && t >= softTimeout)
            {
                warned = true;
                Debug.LogWarning("⚠️ FirebaseBootstrap not ready (still waiting)");
            }
            if (hardTimeout > 0f && t >= hardTimeout)
            {
                Debug.LogWarning("⚠️ FirebaseBootstrap not ready (hard timeout)");
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
                Debug.LogWarning("⚠️ QuestManager not ready (timeout)");
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
            "DataSaver not found (timeout)"
        );

        if (DataSaver.Ins == null)
        {
            Debug.LogError("❌ DataSaver missing. Cannot load local cache.");
            yield break;
        }

        bool localOk = false;
        yield return RunCoroutineWithTimeout(
            DataSaver.Ins.LoadFromLocalCache(ok => localOk = ok),
            loadDataTimeout,
            "LoadLocalCache timeout"
        );
        DataSaver.Ins.MarkInitialLoadComplete(localOk);
        if (localOk)
        {
            MarkStep(ref stepGameplay, "Gameplay (local)");
            MarkStep(ref stepInventory, "Inventory (local)");
        }
    }

    IEnumerator RunCoroutineWithTimeout(IEnumerator routine, float timeout, string timeoutMsg)
    {
        float t = 0f;
        while (true)
        {
            bool moved = routine.MoveNext();
            if (!moved) yield break;

            t += Time.unscaledDeltaTime;
            if (t >= timeout)
            {
                Debug.LogWarning($"⚠️ {timeoutMsg}");
                yield break;
            }

            yield return routine.Current;
        }
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
}

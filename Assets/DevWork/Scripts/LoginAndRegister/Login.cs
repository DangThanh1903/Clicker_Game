using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        if (FirebaseBootstrap.Ins != null)
        {
            float softTimeout = Mathf.Max(0f, waitFirebaseTimeout);
            float hardTimeout = Mathf.Max(softTimeout, waitFirebaseHardTimeout);
            bool warned = false;
            float t = 0f;

            while (!FirebaseBootstrap.Ins.IsReady && !FirebaseBootstrap.Ins.IsFailed)
            {
                t += Time.unscaledDeltaTime;
                if (!warned && softTimeout > 0f && t >= softTimeout)
                {
                    warned = true;
                    Debug.LogWarning("⚠️ FirebaseBootstrap not ready (soft timeout)");
                }
                if (hardTimeout > 0f && t >= hardTimeout)
                {
                    Debug.LogWarning("⚠️ FirebaseBootstrap not ready (hard timeout)");
                    break;
                }
                yield return null;
            }
        }

        if (FirebaseBootstrap.Ins == null || !FirebaseBootstrap.Ins.IsReady)
        {
            if (FirebaseBootstrap.Ins != null && FirebaseBootstrap.Ins.IsFailed)
                Debug.LogError($"❌ Firebase init failed: {FirebaseBootstrap.Ins.InitError}");
            else
                Debug.LogError("❌ Firebase not ready. Still continue to scene (offline/local fallback).");

            yield return WaitUntilOrTimeout(
                () => DataSaver.Ins != null,
                5f,
                "DataSaver not found (timeout)"
            );

            if (DataSaver.Ins != null)
            {
                bool localOk = false;
                yield return RunCoroutineWithTimeout(
                    DataSaver.Ins.LoadFromLocalCache(ok => localOk = ok),
                    loadDataTimeout,
                    "LoadLocalCache timeout"
                );
            }

            LoadGameScene();
            yield break;
        }

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
            LoadGameScene();
            yield break;
        }

        // 3) Load gameplay + inventories with timeout
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

        if (!gameplayOk || !inventoryOk)
        {
            Debug.LogWarning("⚠️ Cloud load incomplete. Try local cache fallback.");
            bool localOk = false;
            yield return RunCoroutineWithTimeout(
                DataSaver.Ins.LoadFromLocalCache(ok => localOk = ok),
                loadDataTimeout,
                "LoadLocalCache timeout"
            );
        }

        Debug.Log("✅ Load flow done -> Load scene");
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
}

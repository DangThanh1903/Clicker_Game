using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Login : MonoBehaviour
{
    [Header("Scene to load after data")]
    [SerializeField] private string gameSceneName = "SampleScene";

    [Header("Timeouts (seconds)")]
    [SerializeField] private float waitFirebaseTimeout = 10f;
    [SerializeField] private float loadDataTimeout = 10f;

    void Start()
    {
        Debug.Log("✅ Login.Start()");
        StartCoroutine(Flow());
    }

    IEnumerator Flow()
    {
        // 1) Wait FirebaseBootstrap
        Debug.Log("⏳ Waiting FirebaseBootstrap...");
        yield return WaitUntilOrTimeout(
            () => FirebaseBootstrap.Ins != null &&
                  FirebaseBootstrap.Ins.Auth != null &&
                  FirebaseBootstrap.Ins.Auth.CurrentUser != null &&
                  FirebaseBootstrap.Ins.Db != null &&
                  !string.IsNullOrEmpty(FirebaseBootstrap.Ins.Auth.CurrentUser.UserId),
            waitFirebaseTimeout,
            "FirebaseBootstrap not ready (timeout)"
        );

        if (FirebaseBootstrap.Ins == null || FirebaseBootstrap.Ins.Auth?.CurrentUser == null)
        {
            Debug.LogError("❌ Firebase not ready. Still continue to scene (offline/local fallback).");
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
        yield return RunCoroutineWithTimeout(DataSaver.Ins.LoadGameplay(uid), loadDataTimeout, "LoadGameplay timeout");

        Debug.Log("⏳ Loading inventories...");
        yield return RunCoroutineWithTimeout(DataSaver.Ins.LoadAllInventories(uid), loadDataTimeout, "LoadAllInventories timeout");

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

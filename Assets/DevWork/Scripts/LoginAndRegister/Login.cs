using System.Collections;
using UnityEngine;
using Firebase.Auth;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

public class Login : MonoBehaviour
{
    private FirebaseAuth auth;

    void Awake()
    {
        auth = FirebaseAuth.DefaultInstance;
    }

    void Start()
    {
        _ = CheckOrGuestLoginAsync();
    }

    private async Task CheckOrGuestLoginAsync()
    {
        // 1. Already signed in? (Firebase persists sessions)
        if (auth.CurrentUser != null)
        {
            string userId = auth.CurrentUser.UserId;
            Debug.Log($"Found current Firebase user: {userId}");

            PlayerPrefs.SetString("UserID", userId);
            PlayerPrefs.Save();

            StartCoroutine(LoadSceneAfterData(userId));
            return;
        }

        // 2. If we stored an ID locally, use it (only makes sense if still valid)
        if (PlayerPrefs.HasKey("UserID"))
        {
            string userId = PlayerPrefs.GetString("UserID");
            Debug.Log($"Found UserID in PlayerPrefs: {userId}");

            StartCoroutine(LoadSceneAfterData(userId));
            return;
        }

        // 3. Otherwise → guest login immediately
        Debug.Log("No account found. Logging in as Guest...");
        await GuestLoginAsync();
    }

    private async Task GuestLoginAsync()
    {
        var loginResult = await auth.SignInAnonymouslyAsync();

        if (loginResult == null || loginResult.User == null)
        {
            Debug.LogError("Anonymous login failed.");
            return;
        }

        string userId = loginResult.User.UserId;
        Debug.Log($"Guest login success: {userId}");

        PlayerPrefs.SetString("UserID", userId);
        PlayerPrefs.Save();

        StartCoroutine(LoadSceneAfterData(userId));
    }

    private IEnumerator LoadSceneAfterData(string userId)
    {
        // Load your data
        yield return StartCoroutine(DataSaver.Ins.LoadAllInventories(userId));
        yield return StartCoroutine(DataSaver.Ins.LoadCurrentBlock(userId));
        yield return StartCoroutine(DataSaver.Ins.LoadCurrentLocation(userId));
        yield return StartCoroutine(DataSaver.Ins.LoadSomeStat(userId));
        yield return StartCoroutine(DataSaver.Ins.LoadTime(userId));

        // Optional delay
        yield return new WaitForSeconds(1f);

        SceneManager.LoadScene("SampleScene");
    }
}

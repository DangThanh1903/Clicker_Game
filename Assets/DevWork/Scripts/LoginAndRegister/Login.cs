using System.Collections;
using UnityEngine;
using Firebase.Auth;
using UnityEngine.UI;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

public class Login : MonoBehaviour
{
    [SerializeField] private Button guestLogin;
    [SerializeField] private GameObject loginPanel;

    void Awake()
    {
        guestLogin.onClick.AddListener(() => _ = GuestLoginAsync());
    }
    void Start()
    {
        RememberLogin();
    }

    private async Task GuestLoginAsync()
    {
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;

        var loginResult = await auth.SignInAnonymouslyAsync();

        if (loginResult == null || loginResult.User == null)
        {
            Debug.LogError("Anonymous login failed.");
            return;
        }

        string userId = loginResult.User.UserId;

        PlayerPrefs.SetString("UserID", userId);
        PlayerPrefs.Save();

        // After async login, move back to Unity main thread
        StartCoroutine(LoadSceneAfterData(userId));
    }

    private IEnumerator LoadSceneAfterData(string userId)
    {

        // Coroutine to load data
        yield return StartCoroutine(DataSaver.Ins.LoadAllInventories(userId));
        yield return StartCoroutine(DataSaver.Ins.LoadCurrentBlock(userId));
        yield return StartCoroutine(DataSaver.Ins.LoadCurrentLocation(userId));
        yield return StartCoroutine(DataSaver.Ins.LoadSomeStat(userId));


        yield return new WaitForSeconds(2f); // optional delay

        SceneManager.LoadScene("SampleScene");
    }
    
    void RememberLogin()
    {
        if (PlayerPrefs.HasKey("UserID"))
        {
            string userId = PlayerPrefs.GetString("UserID");
            Debug.Log("UserID found: " + userId);
            StartCoroutine(LoadSceneAfterData(userId));
        }
        else
        {
            Debug.Log("No UserID found.");
            loginPanel.SetActive(true);
        }
    }
}

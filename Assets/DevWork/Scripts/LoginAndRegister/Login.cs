using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Firebase.Auth;
using UnityEngine.SceneManagement;

public class Login : MonoBehaviour
{
    private FirebaseAuth auth;

    void Awake()
    {
        auth = FirebaseAuth.DefaultInstance;
    }

    void Start()
    {
        _ = EnsureSignedInThenLoadAsync();
    }

    private async Task EnsureSignedInThenLoadAsync()
    {
        // If Firebase previously persisted a session, CurrentUser will be non-null.
        // Reload to ensure token/session is still valid (optional but safer).
        if (auth.CurrentUser != null)
        {
            try
            {
                await auth.CurrentUser.ReloadAsync();
            }
            catch { /* ignore reload errors; we'll re-auth if needed */ }
        }

        if (auth.CurrentUser == null)
        {
            Debug.Log("No Firebase session. Signing in anonymously...");
            var user = await GuestLoginAsync();
            if (user == null)
            {
                Debug.LogError("Failed to sign in anonymously.");
                return;
            }
            Debug.Log($"Anonymous sign-in success: {user.UserId}");
        }
        else
        {
            Debug.Log($"Found Firebase user: {auth.CurrentUser.UserId}");
        }

        // Proceed to load game data
        StartCoroutine(LoadSceneAfterData(auth.CurrentUser.UserId));
    }

    private async Task<FirebaseUser> GuestLoginAsync()
    {
        try
        {
            var result = await auth.SignInAnonymouslyAsync();
            return result?.User;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Anonymous login failed: {e}");
            return null;
        }
    }

    private IEnumerator LoadSceneAfterData(string userId)
    {
        // Ensure DataSaver exists before using it
        yield return new WaitUntil(() => DataSaver.Ins != null);

        DataSaver.Ins.currentUserID = userId;

        // Load your data
        yield return StartCoroutine(DataSaver.Ins.LoadAllInventories(userId));
        yield return StartCoroutine(DataSaver.Ins.LoadCurrentBlock(userId));
        yield return StartCoroutine(DataSaver.Ins.LoadCurrentLocation(userId));
        yield return StartCoroutine(DataSaver.Ins.LoadSomeStat(userId));
        yield return StartCoroutine(DataSaver.Ins.LoadTime(userId));

        // Optional delay for UX
        yield return new WaitForSeconds(0.5f);

        SceneManager.LoadScene("SampleScene");
    }
}

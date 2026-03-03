using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using UnityEngine;

#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

#if FACEBOOK_SDK
using Facebook.Unity;
#endif

public class AuthLinkManager : MonoBehaviour
{
    [Header("Behavior")]
    [SerializeField] private bool signInIfCredentialAlreadyInUse = true;
    [SerializeField, Min(1f)] private float firebaseReadyTimeoutSeconds = 15f;
    [SerializeField] private bool verboseLogs = true;

    private bool isLinking;

    public bool IsLinking => isLinking;

    public void LinkWithGooglePlay()
    {
        if (isLinking)
            return;
        StartCoroutine(CoLinkWithGooglePlay());
    }

    public void LinkWithFacebook()
    {
        if (isLinking)
            return;
        StartCoroutine(CoLinkWithFacebook());
    }

    private IEnumerator CoLinkWithGooglePlay()
    {
#if UNITY_ANDROID
        isLinking = true;
        yield return WaitForFirebaseReady();

        var auth = FirebaseBootstrap.Ins != null ? FirebaseBootstrap.Ins.Auth : null;
        if (auth == null || auth.CurrentUser == null)
        {
            isLinking = false;
            Debug.LogError("Google Play link failed: Firebase user is not ready.");
            yield break;
        }

        PlayGamesPlatform.Activate();
        var playGames = PlayGamesPlatform.Instance;

        bool authDone = false;
        bool authOk = false;
        playGames.Authenticate(status =>
        {
            authOk = status == SignInStatus.Success;
            authDone = true;
            if (!authOk)
                Debug.LogWarning($"Google Play sign-in failed: {status}");
        });
        yield return new WaitUntil(() => authDone);

        if (!authOk)
        {
            isLinking = false;
            yield break;
        }

        bool codeDone = false;
        string authCode = null;
        playGames.RequestServerSideAccess(true, code =>
        {
            authCode = code;
            codeDone = true;
        });
        yield return new WaitUntil(() => codeDone);

        if (string.IsNullOrEmpty(authCode))
        {
            isLinking = false;
            Debug.LogError("Google Play link failed: server auth code is empty.");
            yield break;
        }

        var credential = PlayGamesAuthProvider.GetCredential(authCode);
        Task<bool> linkTask = LinkOrSignInWithCredentialAsync(
            credential,
            "playgames.google.com",
            "Google Play Games");
        yield return new WaitUntil(() => linkTask.IsCompleted);

        if (linkTask.Exception != null || !linkTask.Result)
            Debug.LogError($"Google Play link failed: {linkTask.Exception}");
        else
            LogVerbose("Google Play account linked successfully.");

        isLinking = false;
#else
        Debug.LogWarning("Google Play link is only supported on Android build.");
        yield break;
#endif
    }

    private IEnumerator CoLinkWithFacebook()
    {
#if FACEBOOK_SDK
        isLinking = true;
        yield return WaitForFirebaseReady();

        var auth = FirebaseBootstrap.Ins != null ? FirebaseBootstrap.Ins.Auth : null;
        if (auth == null || auth.CurrentUser == null)
        {
            isLinking = false;
            Debug.LogError("Facebook link failed: Firebase user is not ready.");
            yield break;
        }

        bool initDone = false;
        if (!FB.IsInitialized)
        {
            FB.Init(() => { initDone = true; }, null);
            yield return new WaitUntil(() => initDone);
        }

        if (!FB.IsInitialized)
        {
            isLinking = false;
            Debug.LogError("Facebook SDK failed to initialize.");
            yield break;
        }

        bool loginDone = false;
        bool loginOk = false;
        FB.LogInWithReadPermissions(new List<string> { "public_profile", "email" }, result =>
        {
            loginOk = result != null && string.IsNullOrEmpty(result.Error) && FB.IsLoggedIn;
            loginDone = true;
            if (!loginOk)
            {
                string err = result != null ? result.Error : "unknown error";
                Debug.LogWarning($"Facebook login failed: {err}");
            }
        });
        yield return new WaitUntil(() => loginDone);

        if (!loginOk)
        {
            isLinking = false;
            yield break;
        }

        string token = AccessToken.CurrentAccessToken != null ? AccessToken.CurrentAccessToken.TokenString : null;
        if (string.IsNullOrEmpty(token))
        {
            isLinking = false;
            Debug.LogError("Facebook link failed: access token is missing.");
            yield break;
        }

        var credential = FacebookAuthProvider.GetCredential(token);
        Task<bool> linkTask = LinkOrSignInWithCredentialAsync(
            credential,
            "facebook.com",
            "Facebook");
        yield return new WaitUntil(() => linkTask.IsCompleted);

        if (linkTask.Exception != null || !linkTask.Result)
            Debug.LogError($"Facebook link failed: {linkTask.Exception}");
        else
            LogVerbose("Facebook account linked successfully.");

        isLinking = false;
#else
        Debug.LogWarning("Facebook SDK is not installed. Install Facebook SDK and define FACEBOOK_SDK.");
        yield break;
#endif
    }

    private IEnumerator WaitForFirebaseReady()
    {
        float timeout = Mathf.Max(1f, firebaseReadyTimeoutSeconds);
        float elapsed = 0f;

        while (FirebaseBootstrap.Ins == null || !FirebaseBootstrap.Ins.IsReady)
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= timeout)
            {
                Debug.LogError("Timed out waiting for FirebaseBootstrap ready state.");
                yield break;
            }
            yield return null;
        }
    }

    private async Task<bool> LinkOrSignInWithCredentialAsync(Credential credential, string providerId, string providerLabel)
    {
        var auth = FirebaseBootstrap.Ins != null ? FirebaseBootstrap.Ins.Auth : null;
        if (auth == null)
            return false;

        var currentUser = auth.CurrentUser;
        if (currentUser == null)
            return false;

        try
        {
            if (IsProviderAlreadyLinked(currentUser, providerId))
            {
                LogVerbose($"{providerLabel} is already linked.");
                return true;
            }

            await FirebaseTaskTracker.Track(currentUser.LinkWithCredentialAsync(credential));
            return true;
        }
        catch (FirebaseException ex) when (IsCredentialAlreadyInUse(ex) && signInIfCredentialAlreadyInUse)
        {
            LogVerbose($"{providerLabel} credential already in use. Signing in with provider account.");
            try
            {
                await FirebaseTaskTracker.Track(auth.SignInWithCredentialAsync(credential));
                return true;
            }
            catch (Exception signInEx)
            {
                Debug.LogError($"{providerLabel} sign-in failed after link conflict: {signInEx}");
                return false;
            }
        }
        catch (FirebaseException ex) when (IsProviderAlreadyLinkedError(ex))
        {
            LogVerbose($"{providerLabel} already linked by Firebase.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"{providerLabel} link failed: {ex}");
            return false;
        }
    }

    private static bool IsProviderAlreadyLinked(FirebaseUser user, string providerId)
    {
        if (user == null || string.IsNullOrEmpty(providerId))
            return false;

        foreach (var info in user.ProviderData)
        {
            if (info != null && string.Equals(info.ProviderId, providerId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool IsCredentialAlreadyInUse(FirebaseException ex)
    {
        if (ex == null)
            return false;
        string codeName = ((AuthError)ex.ErrorCode).ToString();
        return string.Equals(codeName, "CredentialAlreadyInUse", StringComparison.Ordinal) ||
               string.Equals(codeName, "AccountExistsWithDifferentCredentials", StringComparison.Ordinal);
    }

    private static bool IsProviderAlreadyLinkedError(FirebaseException ex)
    {
        if (ex == null)
            return false;
        string codeName = ((AuthError)ex.ErrorCode).ToString();
        return string.Equals(codeName, "ProviderAlreadyLinked", StringComparison.Ordinal);
    }

    private void LogVerbose(string message)
    {
        if (verboseLogs)
            Debug.Log(message);
    }
}

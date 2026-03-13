using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;

public class GooglePlayGamesLinker : MonoBehaviour
{
    [SerializeField] private bool enableGooglePlayLink = false;

    public async Task<bool> LinkWithPlayGamesAuthCodeAsync(string authCode)
    {
        if (!enableGooglePlayLink)
        {
            Debug.LogWarning("Google Play link is temporarily disabled.");
            return false;
        }

        var auth = FirebaseBootstrap.Ins != null ? FirebaseBootstrap.Ins.Auth : null;
        if (auth == null)
        {
            Debug.LogError("Firebase not ready. Missing FirebaseBootstrap.");
            return false;
        }
        if (auth.CurrentUser == null) return false;

        try
        {
            Credential credential = PlayGamesAuthProvider.GetCredential(authCode);
            var res = await FirebaseTaskTracker.Track(auth.CurrentUser.LinkWithCredentialAsync(credential));
            DevLog.Log($"âœ… Linked Google Play Games. uid still = {res.User.UserId}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"âŒ Link Google failed: {e}");
            return false;
        }
    }
}


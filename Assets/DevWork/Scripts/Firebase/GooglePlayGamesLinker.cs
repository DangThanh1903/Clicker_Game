using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;

public class GooglePlayGamesLinker : MonoBehaviour
{
    public async Task<bool> LinkWithPlayGamesAuthCodeAsync(string authCode)
    {
        var auth = AuthManager.Ins.Auth;
        if (auth.CurrentUser == null) return false;

        try
        {
            Credential credential = PlayGamesAuthProvider.GetCredential(authCode);
            var res = await auth.CurrentUser.LinkWithCredentialAsync(credential);
            Debug.Log($"✅ Linked Google Play Games. uid still = {res.User.UserId}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Link Google failed: {e}");
            return false;
        }
    }
}

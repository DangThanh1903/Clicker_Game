using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using UnityEngine;

public class AuthManager : MonoBehaviour
{
    public static AuthManager Ins { get; private set; }

    public FirebaseAuth Auth { get; private set; }
    public string Uid => Auth?.CurrentUser?.UserId;
    public bool IsReady { get; private set; }

    async void Awake()
    {
        if (Ins && Ins != this) { Destroy(gameObject); return; }
        Ins = this;
        DontDestroyOnLoad(gameObject);

        var dep = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (dep != DependencyStatus.Available)
        {
            Debug.LogError($"Firebase deps not available: {dep}");
            return;
        }

        Auth = FirebaseAuth.DefaultInstance;

        // Auto anonymous
        if (Auth.CurrentUser == null)
        {
            var res = await Auth.SignInAnonymouslyAsync();
            Debug.Log($"✅ Anonymous signed in: {res.User.UserId}");
        }
        else
        {
            Debug.Log($"✅ Found existing user: {Auth.CurrentUser.UserId} (anon={Auth.CurrentUser.IsAnonymous})");
        }

        IsReady = true;
    }

    public bool IsAnonymous() => Auth?.CurrentUser != null && Auth.CurrentUser.IsAnonymous;
}

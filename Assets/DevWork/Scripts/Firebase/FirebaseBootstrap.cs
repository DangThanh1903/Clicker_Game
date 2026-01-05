using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;

public class FirebaseBootstrap : MonoBehaviour
{
    public static FirebaseBootstrap Ins { get; private set; }

    public FirebaseAuth Auth { get; private set; }
    public FirebaseFirestore Db { get; private set; }

    public string Uid => Auth?.CurrentUser?.UserId;

    async void Awake()
    {
        if (Ins && Ins != this) { Destroy(gameObject); return; }
        Ins = this;
        DontDestroyOnLoad(gameObject);

        var dep = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (dep != DependencyStatus.Available)
            throw new Exception($"Firebase deps not available: {dep}");

        Auth = FirebaseAuth.DefaultInstance;
        Db = FirebaseFirestore.DefaultInstance;

        if (Auth.CurrentUser == null)
            await Auth.SignInAnonymouslyAsync();

        Debug.Log($"✅ Firebase ready. uid={Uid}");
    }
}

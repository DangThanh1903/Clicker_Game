using UnityEngine;
using UnityEngine.SceneManagement;

public class SplashSceneController : MonoBehaviour
{
    public static SplashSceneController Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}

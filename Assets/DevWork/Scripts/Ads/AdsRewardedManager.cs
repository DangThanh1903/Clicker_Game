using System;
using GoogleMobileAds.Api;
using UnityEngine;

public class AdsRewardedManager : MonoBehaviour
{
    public static AdsRewardedManager Instance { get; private set; }

    [Header("AdMob IDs (local asset)")]
    [SerializeField] private AdsConfig adsConfig;

    [Header("Behavior")]
    [SerializeField] private bool autoInitialize = true;
    [SerializeField] private float showCooldownSeconds = 0f;

    private RewardedAd rewardedAd;
    private bool isInitialized;
    private bool isShowing;
    private float nextAllowedShowTime;
    private Action onRewardGranted;
    private Action onClosed;
    private Action<string> onFailed;

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

    void Start()
    {
        if (autoInitialize)
            Initialize();
    }

    public void Initialize()
    {
        if (isInitialized) return;

        MobileAds.Initialize(initStatus =>
        {
            isInitialized = true;
            Debug.Log("[Ads] MobileAds initialized.");
            LoadRewarded();
        });
    }

    public void LoadRewarded()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("[Ads] LoadRewarded called before Initialize.");
            return;
        }

        string adUnitId = GetRewardedUnitId();
        if (string.IsNullOrEmpty(adUnitId))
        {
            Debug.LogWarning("[Ads] Rewarded unit id is empty.");
            return;
        }

        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
            rewardedAd = null;
        }

        AdRequest request = new AdRequest();
        RewardedAd.Load(adUnitId, request, (RewardedAd ad, LoadAdError error) =>
        {
            if (error != null)
            {
                Debug.LogWarning($"[Ads] Rewarded failed to load: {error.GetMessage()}");
                return;
            }

            rewardedAd = ad;
            Debug.Log("[Ads] Rewarded loaded.");
            RegisterRewardedHandlers(rewardedAd);
        });
    }

    public bool IsRewardedReady()
    {
        return rewardedAd != null && rewardedAd.CanShowAd();
    }

    public void ShowRewarded(Action onRewardGranted, Action onClosed = null, Action<string> onFailed = null)
    {
        if (!isInitialized)
        {
            onFailed?.Invoke("Ads not initialized.");
            Debug.LogWarning("[Ads] ShowRewarded blocked: not initialized.");
            return;
        }

        if (isShowing)
        {
            onFailed?.Invoke("Ad already showing.");
            Debug.LogWarning("[Ads] ShowRewarded blocked: already showing.");
            return;
        }

        if (Time.unscaledTime < nextAllowedShowTime)
        {
            onFailed?.Invoke("Cooldown active.");
            Debug.LogWarning("[Ads] ShowRewarded blocked: cooldown.");
            return;
        }

        if (!IsRewardedReady())
        {
            onFailed?.Invoke("Ad not ready.");
            Debug.LogWarning("[Ads] ShowRewarded blocked: ad not ready.");
            LoadRewarded();
            return;
        }

        this.onRewardGranted = onRewardGranted;
        this.onClosed = onClosed;
        this.onFailed = onFailed;

        isShowing = true;
        rewardedAd.Show(reward =>
        {
            Debug.Log("[Ads] Reward granted.");
            this.onRewardGranted?.Invoke();
        });
    }

    private void RegisterRewardedHandlers(RewardedAd ad)
    {
        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("[Ads] Rewarded opened.");
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("[Ads] Rewarded closed.");
            isShowing = false;
            nextAllowedShowTime = Time.unscaledTime + Mathf.Max(0f, showCooldownSeconds);
            onClosed?.Invoke();
            LoadRewarded();
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            Debug.LogWarning($"[Ads] Rewarded failed to show: {error.GetMessage()}");
            isShowing = false;
            nextAllowedShowTime = Time.unscaledTime + Mathf.Max(0f, showCooldownSeconds);
            onFailed?.Invoke(error.GetMessage());
            LoadRewarded();
        };

        ad.OnAdPaid += adValue =>
        {
            Debug.Log($"[Ads] Paid event: {adValue.Value} {adValue.CurrencyCode} ({adValue.Precision})");
        };
    }

    private string GetRewardedUnitId()
    {
#if UNITY_ANDROID
        if (adsConfig == null)
        {
            Debug.LogWarning("[Ads] AdsConfig is missing. Assign a local AdsConfig asset.");
            return string.Empty;
        }
        return adsConfig.RewardedUnitIdAndroid;
#else
        return string.Empty;
#endif
    }

    public void OnClickRewarded20Diamonds()
    {
        ShowRewarded(
            onRewardGranted: () =>
            {
                if (StatsManager.Ins != null)
                    StatsManager.Ins.Add(StatType.Diamond, 20);
                else
                    Debug.LogWarning("[Ads] StatsManager missing, cannot grant diamonds.");
            },
            onClosed: () => Debug.Log("[Ads] Rewarded closed."),
            onFailed: err => Debug.LogWarning($"[Ads] Rewarded failed: {err}")
        );
    }
}

using UnityEngine;

[CreateAssetMenu(menuName = "Ads/AdsConfig", fileName = "AdsConfig")]
public class AdsConfig : ScriptableObject
{
    [Header("AdMob IDs (placeholders)")]
    [SerializeField] private string appIdAndroid = "ADMOB_APP_ID_ANDROID";
    [SerializeField] private string rewardedUnitIdAndroid = "ADMOB_REWARDED_UNIT_ID_ANDROID";

    public string AppIdAndroid => appIdAndroid;
    public string RewardedUnitIdAndroid => rewardedUnitIdAndroid;
}

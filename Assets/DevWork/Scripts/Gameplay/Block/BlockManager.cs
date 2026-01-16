using Lean.Pool;
using UnityEngine;

public class BlockManager : MonoBehaviour
{
    public static BlockManager Ins;
    [SerializeField] private ClickableObject currentBlock;
    [SerializeField] private LocationLoader locationLoader;
    [SerializeField] BossSO bossSO;
    [SerializeField] Transform  spawnPos;
    public float rareWeightCap = 10;
    private GameObject activeBoss;
    private BossEntry activeBossInfo;
    Boss activeBossComp;
    void Awake()
    {
        if (Ins && Ins != this) { Destroy(gameObject); return; }
        Ins = this;
        DontDestroyOnLoad(gameObject);

        currentBlock.SetClickableBlock(DataSaver.Ins.currentBlock ?? "Dirt");


        int startIndex = (int?)DataSaver.Ins.currentLocation ?? 1;
        locationLoader.SetLocation(startIndex, isInitiate: true);
    }


    public GameObject Summon(BlockSpawnLocation bossLocation, BossType bossType)
    {
        if (activeBoss)
        {
            Debug.Log("[BossSpawner] Boss already active.");
            return activeBoss;
        }

        Vector3 pos = spawnPos ? spawnPos.position : Vector3.zero;
        Quaternion rot = spawnPos ? spawnPos.rotation : Quaternion.identity;

        activeBossInfo = bossSO.FindOne(bossLocation, bossType);

        if (activeBossInfo == null)
        {
            Debug.LogWarning($"[BossSpawner] No boss found for location {bossLocation} and type {bossType}");
            return null;
        }

        if (activeBossInfo.bossPrefab == null)
        {
            Debug.LogWarning($"[BossSpawner] Boss prefab is null for boss {activeBossInfo.bossName}");
            return null;
        }

        if (IsBossOutOfCondition())
        {
            Debug.Log($"[BossSpawner] Boss {activeBossInfo.bossName} cannot be summoned due to time/weather conditions.");
            // Game log
            return null;
        }

        // Spawn
        var bossPrefab = activeBossInfo.bossPrefab;
        var go = LeanPool.Spawn(bossPrefab, pos, rot);
        activeBoss = go;
        var stats = go.GetComponent<EnemyStatsManager>();

        UIManager.Ins.SetButtonsInteractable(false);

        activeBossComp = go.GetComponent<Boss>();
        if (activeBossComp != null)
            activeBossComp.Died += OnBossDied;
        PlayerController.Instance.OnDied += () =>
        {
            LeanPool.Despawn(go);
            activeBoss = null;
            activeBossComp = null;
            if (currentBlock) currentBlock.gameObject.SetActive(true);
            UIManager.Ins.SetButtonsInteractable(true);
        };
        if (currentBlock) currentBlock.gameObject.SetActive(false);
        return go;
    }
    public void OnBossDied(Boss boss)
    {
        if (activeBossComp != null) activeBossComp.Died -= OnBossDied;

        if (boss && boss.gameObject && boss.gameObject.activeInHierarchy)
            LeanPool.Despawn(boss.gameObject);

        activeBoss = null;
        activeBossComp = null;

        if (currentBlock) currentBlock.gameObject.SetActive(true);
        UIManager.Ins.SetButtonsInteractable(true);
    }
    public void OnBlockBroken()
    {
        string blockId = currentBlock.BlockName;

        var biome = locationLoader.currentLocation;
        string biomeName = biome.ToString();

        string targetId = $"{blockId}@{biomeName}";

        QuestSignals.BreakBlock(targetId, 1);

        NormalWeatherName normalName =
            (WeatherManager.Instance.CurrentNormalWeather.Value as NormalWeatherData)?.weatherName
            ?? NormalWeatherName.Any;

        SpecialWeatherName specialName =
            (WeatherManager.Instance.CurrentSpecialWeather.Value as SpecialWeatherData)?.weatherName
            ?? SpecialWeatherName.Any;
        DataSaver.Ins.SaveDataFn();
        currentBlock.SetClickableBlockByCondition(
            locationLoader.currentLocation,
            TimeSystem.Instance.CurrentTimeState.Value,
            normalName,
            specialName
        );
    }


    // Boss
    public bool IsBossOutOfCondition()
    {
        return activeBossInfo.Matches(TimeSystem.Instance.CurrentTimeState.Value, WeatherManager.Instance.CurrentNormalWeather.Value, WeatherManager.Instance.CurrentSpecialWeather.Value) == false;
    }
}

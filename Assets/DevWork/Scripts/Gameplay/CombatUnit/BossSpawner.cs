using Lean.Pool;
using UnityEngine;

public class BossSpawner : MonoBehaviour
{
    public static BossSpawner Ins;
    [SerializeField] GameObject bossPrefab;
    [SerializeField] Transform  spawnPos;
    private GameObject activeBoss;
    void Awake()
    {
        if (Ins && Ins != this)
        {
            Destroy(gameObject); return;
        }
        Ins = this;
        DontDestroyOnLoad(gameObject);
    }

    public GameObject Summon(BaseStat bossBase)
    {
        if (activeBoss)
        {
            Debug.Log("[BossSpawner] Boss already active.");
            return activeBoss;
        }

        Vector3 pos = spawnPos ? spawnPos.position : Vector3.zero;
        Quaternion rot = spawnPos ? spawnPos.rotation : Quaternion.identity;

        var go = LeanPool.Spawn(bossPrefab, pos, rot);
        activeBoss = go;
        var stats = go.GetComponent<EnemyStatsManager>();
        if (stats)
        {
            stats.SetBaseStat(bossBase);
        }
        else
        {
            Debug.LogWarning("[BossSpawner] Spawned boss has no EnemyStatsManager.");
        }

        return go;
    }
}

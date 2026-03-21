using System.Linq;
using System;
using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

public class MonsterSpawner : MonoBehaviour
{
    [Header("Spawn Tables")]
    [SerializeField, FormerlySerializedAs("table")] private SpawnTable edgeSpawnTable;
    [SerializeField] private SpawnTable blockEncounterSpawnTable;

    [Header("Trigger")]
    [SerializeField, Min(1)] private int blocksPerSpawn = 10;
    [SerializeField] private bool ignoreSpawnRuleForBlockEncounter = true;

    [Header("Spawn")]
    [SerializeField] private Transform spawnRoot;
    [SerializeField] private bool spawnAtBlockAnchor = true;

    [Header("Limit")]
    [SerializeField] private bool allowOnlyOneAlive = true;

    public event Action<int, int> SpawnProgressChanged;
    public event Action<bool> EncounterStateChanged;

    public int BlocksPerSpawn => Mathf.Max(1, blocksPerSpawn);
    public int CurrentBreakProgress => Mathf.Min(blockBreakCounter, BlocksPerSpawn);
    public bool HasActiveEncounter => currentAlive != null;

    private MonsterClickable currentAlive;
    private int blockBreakCounter;
    private Transform blockAnchor;
    private bool warnedMissingBlockEncounterTable;
    private bool warnedNoMatchingPool;
    private bool warnedUsingEdgeTableFallback;
    private readonly List<MonsterSpawnEntry> candidateEntriesBuffer = new List<MonsterSpawnEntry>(16);

    public void NotifyBlockBroken()
    {
        int threshold = BlocksPerSpawn;
        blockBreakCounter = Mathf.Min(blockBreakCounter + 1, threshold);
        SpawnProgressChanged?.Invoke(blockBreakCounter, threshold);

        if (blockBreakCounter < threshold)
            return;

        if (TrySpawn())
        {
            blockBreakCounter = 0;
            SpawnProgressChanged?.Invoke(blockBreakCounter, threshold);
        }
    }

    bool TrySpawn()
    {
        var table = ResolveBlockEncounterTable();
        if (table == null || table.pools == null || table.pools.Count == 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!warnedMissingBlockEncounterTable)
            {
                warnedMissingBlockEncounterTable = true;
                Debug.LogWarning("[MonsterSpawner] Missing spawn table for block encounter. Spawn is disabled.", this);
            }
#endif
            return false;
        }

        if (allowOnlyOneAlive)
        {
            if (currentAlive != null)
            {
                if (currentAlive.gameObject != null && currentAlive.gameObject.activeInHierarchy)
                    return false;

                currentAlive = null;
            }
        }

        var ctx = BuildContext();
        var entries = ResolveCandidateEntries(table, ctx);
        if (entries == null || entries.Count == 0)
            return false;

        var picked = PickWeighted(entries);

        if (picked == null || picked.monster == null) return false;

        return SpawnEncounter(picked.monster);
    }

    private SpawnTable ResolveBlockEncounterTable()
    {
        if (blockEncounterSpawnTable != null && blockEncounterSpawnTable.pools != null && blockEncounterSpawnTable.pools.Count > 0)
            return blockEncounterSpawnTable;

        if (edgeSpawnTable != null && edgeSpawnTable.pools != null && edgeSpawnTable.pools.Count > 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!warnedUsingEdgeTableFallback)
            {
                warnedUsingEdgeTableFallback = true;
                Debug.LogWarning("[MonsterSpawner] blockEncounterSpawnTable is missing, fallback to edgeSpawnTable.", this);
            }
#endif
            return edgeSpawnTable;
        }

        return null;
    }

    private List<MonsterSpawnEntry> ResolveCandidateEntries(SpawnTable table, SpawnContext ctx)
    {
        if (table == null || table.pools == null || table.pools.Count == 0)
            return null;

        candidateEntriesBuffer.Clear();

        var matchedPool = table.pools
            .Where(p => p != null && p.rule != null && p.rule.Matches(ctx))
            .OrderByDescending(p => p.rule.priority)
            .FirstOrDefault();

        if (matchedPool != null && matchedPool.entries != null && matchedPool.entries.Count > 0)
        {
            AppendValidEntries(matchedPool.entries, candidateEntriesBuffer);
            if (candidateEntriesBuffer.Count > 0)
                return candidateEntriesBuffer;
        }

        if (!ignoreSpawnRuleForBlockEncounter)
            return null;

        for (int i = 0; i < table.pools.Count; i++)
        {
            var pool = table.pools[i];
            if (pool == null || pool.entries == null || pool.entries.Count == 0)
                continue;

            AppendValidEntries(pool.entries, candidateEntriesBuffer);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (candidateEntriesBuffer.Count == 0 && !warnedNoMatchingPool)
        {
            warnedNoMatchingPool = true;
            Debug.LogWarning("[MonsterSpawner] No valid spawn pool found for block encounter.", this);
        }
#endif
        return candidateEntriesBuffer;
    }

    private static void AppendValidEntries(List<MonsterSpawnEntry> source, List<MonsterSpawnEntry> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            var entry = source[i];
            if (entry == null || entry.monster == null || entry.monster.prefab == null)
                continue;

            if (entry.weight <= 0f)
                continue;

            target.Add(entry);
        }
    }

    MonsterSpawnEntry PickWeighted(System.Collections.Generic.List<MonsterSpawnEntry> list)
    {
        float total = list.Sum(e => Mathf.Max(0f, e.weight));
        if (total <= 0f) return null;

        float r = UnityEngine.Random.Range(0f, total);
        float acc = 0f;

        foreach (var e in list)
        {
            acc += Mathf.Max(0f, e.weight);
            if (r <= acc) return e;
        }
        return list[list.Count - 1];
    }

    SpawnContext BuildContext()
    {
        var loc = DataSaver.Ins != null && DataSaver.Ins.currentLocation.HasValue
            ? DataSaver.Ins.currentLocation.Value
            : default;

        var timeState = TimeSystem.Instance != null ? TimeSystem.Instance.CurrentTimeState.Value : TimeState.Any;

        var normal = NormalWeatherName.Any;
        if (WeatherManager.Instance != null && WeatherManager.Instance.CurrentNormalWeather.Value is NormalWeatherData n)
            normal = n.weatherName;

        var special = SpecialWeatherName.Any;
        if (WeatherManager.Instance != null && WeatherManager.Instance.CurrentSpecialWeather.Value is SpecialWeatherData s)
            special = s.weatherName;

        return new SpawnContext { location = loc, timeState = timeState, normalWeather = normal, specialWeather = special };
    }

    bool SpawnEncounter(MonsterDef def)
    {
        if (def.prefab == null) return false;

        var pos = GetSpawnPos();
        var rot = GetSpawnRot();
        var go = Lean.Pool.LeanPool.Spawn(def.prefab, pos, rot, spawnRoot);

        var clickable = go.GetComponent<MonsterClickable>();
        if (clickable == null)
        {
            Debug.LogError($"[MonsterSpawner] Spawned prefab '{def.prefab.name}' is missing MonsterClickable. Despawning instance.", go);
            Lean.Pool.LeanPool.Despawn(go);
            return false;
        }

        clickable.Init(def, this);
        currentAlive = clickable;
        EncounterStateChanged?.Invoke(true);

        var ctx = BuildContext();
        AnalyticsManager.Ins?.TrackMonsterSpawn(
            ResolveMonsterId(def),
            ctx.location.ToString(),
            ctx.timeState.ToString(),
            ctx.normalWeather.ToString(),
            ctx.specialWeather.ToString()
        );

        if (def.appearSfx != null) SoundEffectController.Ins?.PlaySFX(def.appearSfx);
        return true;
    }

    public void NotifyResolved(MonsterClickable who)
    {
        if (currentAlive != who)
            return;

        currentAlive = null;
        EncounterStateChanged?.Invoke(false);
    }

    Vector3 GetSpawnPos()
    {
        if (spawnAtBlockAnchor && blockAnchor != null)
            return blockAnchor.position;

        return spawnRoot != null ? spawnRoot.position : transform.position;
    }

    Quaternion GetSpawnRot()
    {
        if (spawnAtBlockAnchor && blockAnchor != null)
            return blockAnchor.rotation;

        return Quaternion.identity;
    }

    public void SetBlockAnchor(Transform anchor)
    {
        blockAnchor = anchor;
    }

    string ResolveMonsterId(MonsterDef def)
    {
        if (def == null) return "unknown";
        return string.IsNullOrEmpty(def.id) ? def.name : def.id;
    }
}

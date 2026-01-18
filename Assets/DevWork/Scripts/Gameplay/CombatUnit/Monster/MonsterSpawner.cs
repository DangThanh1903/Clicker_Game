using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniRx;
using System;

public class MonsterSpawner : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private SpawnTable table;
    [SerializeField] private float checkInterval = 10f;

    [Header("Spawn")]
    [SerializeField] private Transform spawnRoot;

    [Header("Limit")]
    [SerializeField] private bool allowOnlyOneAlive = true;

    private IDisposable tick;
    private MonsterClickable currentAlive;

    void OnEnable()
    {
        tick?.Dispose();
        tick = Observable.Interval(TimeSpan.FromSeconds(checkInterval))
            .Subscribe(_ => TrySpawn())
            .AddTo(this);
    }

    void OnDisable()
    {
        tick?.Dispose();
        tick = null;
    }

    void TrySpawn()
    {
        if (table == null || table.pools == null || table.pools.Count == 0) return;

        if (allowOnlyOneAlive && currentAlive != null) return;

        var ctx = BuildContext();

        var pool = table.pools
            .Where(p => p != null && p.rule != null && p.rule.Matches(ctx))
            .OrderByDescending(p => p.rule.priority)
            .FirstOrDefault();

        if (pool == null || pool.entries == null || pool.entries.Count == 0) return;

        var picked = PickWeighted(pool.entries);

        if (picked == null || picked.monster == null) return;

        SpawnEncounter(picked.monster);
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

    void SpawnEncounter(MonsterDef def)
    {
        if (def.prefab == null) return;

        var pos = GetSpawnPos();
        var go = Lean.Pool.LeanPool.Spawn(def.prefab, pos, Quaternion.identity, spawnRoot);

        var clickable = go.GetComponent<MonsterClickable>();
        if (clickable == null) clickable = go.AddComponent<MonsterClickable>();

        clickable.Init(def, this);
        currentAlive = clickable;

        var ctx = BuildContext();
        AnalyticsManager.Ins?.TrackMonsterSpawn(
            ResolveMonsterId(def),
            ctx.location.ToString(),
            ctx.timeState.ToString(),
            ctx.normalWeather.ToString(),
            ctx.specialWeather.ToString()
        );

        if (def.appearSfx != null) SoundEffectController.Ins?.PlaySFX(def.appearSfx);
    }

    public void NotifyResolved(MonsterClickable who)
    {
        if (currentAlive == who) currentAlive = null;
    }

    Vector3 GetSpawnPos()
    {
        return spawnRoot != null ? spawnRoot.position : transform.position;
    }

    string ResolveMonsterId(MonsterDef def)
    {
        if (def == null) return "unknown";
        return string.IsNullOrEmpty(def.id) ? def.name : def.id;
    }

    void OnEncounterResolved(MonsterClickable who)
    {
        if (currentAlive == who) currentAlive = null;
    }
}

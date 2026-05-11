using UnityEngine.UI;
using UniRx;
using UnityEngine;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(DamageTargetRegistrant))]
public class Boss : MonoBehaviour, IDamageReceiver
{
    [SerializeField] EnemyStatsManager enemyStatsManager;
    [SerializeField] Image HpUI;
    public event Action<Boss> Died;
    public int InputPriority => 2;
    public bool CanReceiveDamage => isActiveAndEnabled;

    [Header("Tick Settings")]
    [SerializeField] float tickSeconds = 1f;
    [SerializeField] bool useUnscaledTime = true;
    IDisposable sub;
    private CompositeDisposable runtimeSubs;
    private float spawnTime;
    private string bossId;
    private BossEntry rewardEntry;
    private bool hasDied;
    private float BossNow => useUnscaledTime ? Time.unscaledTime : Time.time;

    void OnEnable()
    {
        hasDied = false;
        if (spawnTime <= 0f)
            spawnTime = BossNow;

        SetUp();

        if (enemyStatsManager == null) return;

        var scheduler = useUnscaledTime ? Scheduler.MainThreadIgnoreTimeScale : Scheduler.MainThread;

        sub = Observable.Interval(TimeSpan.FromSeconds(Mathf.Max(0.01f, tickSeconds)), scheduler)
            .Subscribe(_ =>
            {
                float cur  = enemyStatsManager.Get(StatType.CurrentHP);
                float max  = enemyStatsManager.Get(StatType.HP);
                float rps  = enemyStatsManager.Get(StatType.HpRegen);
                float gain = rps * tickSeconds; 

                if (Mathf.Approximately(gain, 0f)) return;
                if (gain > 0f && cur >= max) return;
                if (gain < 0f && cur <= 0f)  return; 

                enemyStatsManager.Set(StatType.CurrentHP, Mathf.Clamp(cur + gain, 0f, max));
            });
    }

    void OnDisable()
    {
        sub?.Dispose();
        sub = null;
        runtimeSubs?.Dispose();
        runtimeSubs = null;
        hasDied = false;
        rewardEntry = null;
    }

    void SetUp()
    {
        runtimeSubs?.Dispose();
        runtimeSubs = new CompositeDisposable();

        if (enemyStatsManager != null)
        {
            enemyStatsManager.GetReactive(StatType.CurrentHP)
                .Subscribe(val =>
                {
                    float maxHP = enemyStatsManager.Get(StatType.HP);
                    float fill = (maxHP > 0f) ? (val / maxHP) : 0f;
                    HpUI.fillAmount = Mathf.Clamp01(fill);
                    if (val <= 0f)
                        OnDying();
                })
                .AddTo(runtimeSubs);
        }
    }
    public void HandleClick()
    {
        float power = DamageInputPowerResolver.GetClickPower();
        TakeDamage(power, countAsHit: true);
    }

    public void ApplyDamageInput(DamageInputKind inputKind)
    {
        switch (inputKind)
        {
            case DamageInputKind.Click:
                HandleClick();
                return;
            case DamageInputKind.AutoAttack:
                HandleAutoAttack();
                return;
            default:
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[Boss] Unknown damage input kind: {inputKind}", this);
#endif
                return;
        }
    }

    public void HandleAutoAttack()
    {
        float power = DamageInputPowerResolver.GetAutoAttackPower();
        TakeDamage(power, countAsHit: true);
        CombatFeedbackRuntime.NotifyAutoAttackDamageDealt(power, transform.position);
    }
    void TakeDamage(float power, bool countAsHit = true)
    {
        if (power <= 0f)
            return;

        enemyStatsManager.Set(StatType.CurrentHP, Mathf.Max(0, enemyStatsManager.Get(StatType.CurrentHP) - power));
        DamageStatsRecorder.RecordDamage(power, 1f);
        if (countAsHit)
            CombatFeedbackRuntime.NotifyDamageHit();
    }
    void OnDying()
    {
        if (hasDied) return;
        hasDied = true;
        StatsManager.Ins.Add(StatType.TotalBlockBreaked, 1);
        string id = string.IsNullOrEmpty(bossId) ? gameObject.name : bossId;
        AnalyticsManager.Ins?.TrackBossKill(id, Mathf.Max(0f, BossNow - spawnTime));
        HandleItemDrop();
        Died?.Invoke(this); 
    }

    void HandleItemDrop()
    {
        if (rewardEntry == null || rewardEntry.drops == null || rewardEntry.drops.Count == 0)
            return;

        float luck = StatsManager.Ins != null ? StatsManager.Ins.Get(StatType.Lucky) : 0f;
        var drops = rewardEntry.GetDroppedItems(luck);
        if (drops == null || drops.Count == 0)
            return;

        var grantEntries = new List<DropGrantEntry>(drops.Count);
        foreach (var result in drops)
            grantEntries.Add(new DropGrantEntry(result.item, result.amount));

        DropGrantService.TryGrantDrops(grantEntries, out _, logContext: "[BossDrop]");
    }

    public void SetSpawnContext(BossEntry entry)
    {
        rewardEntry = entry;
        string id = entry != null ? entry.bossName : null;
        bossId = string.IsNullOrEmpty(id) ? gameObject.name : id;
        spawnTime = BossNow;
    }
}

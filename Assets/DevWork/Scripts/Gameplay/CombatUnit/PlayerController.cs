using System;
using UniRx;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private static PlayerController _instance;
    public static PlayerController Instance => _instance;
    private float manaRegenTimer = 0f;
    private readonly float timeManaReset = 0.1f;
    public ClickerState currentState;
    public event Action OnDied;
    public bool IsDead { get; private set; }

    [Header("Tick Settings")]
    [SerializeField] float tickSeconds = 1f;
    [SerializeField] bool useUnscaledTime = true;
    IDisposable regenSub;
    IDisposable deathSub;

    // Health and state setup
    private void Awake()
    {
        if (_instance == null) _instance = this;
        else Destroy(gameObject);

        currentState = new NormalState();
    }

    private void Start()
    {
        StatsManager.Ins.Set(StatType.CurrentHP, StatsManager.Ins.Get(StatType.HP));
        StatsManager.Ins.Set(StatType.CurrentMana, StatsManager.Ins.Get(StatType.Mana));
    }
    void OnEnable()
    {
        var scheduler = useUnscaledTime ? Scheduler.MainThreadIgnoreTimeScale : Scheduler.MainThread;

        // HP regen tick (skip when dead)
        regenSub = Observable.Interval(TimeSpan.FromSeconds(Mathf.Max(0.01f, tickSeconds)), scheduler)
            .Subscribe(_ =>
            {
                if (IsDead) return;

                float cur = StatsManager.Ins.Get(StatType.CurrentHP);
                float max = StatsManager.Ins.Get(StatType.HP);
                float rps = StatsManager.Ins.Get(StatType.HpRegen);
                float gain = rps * tickSeconds;

                if (Mathf.Approximately(gain, 0f)) return;
                if (gain > 0f && cur >= max) return;
                if (gain < 0f && cur <= 0f) return;

                StatsManager.Ins.Set(StatType.CurrentHP, Mathf.Clamp(cur + gain, 0f, max));
            })
            .AddTo(this);

        // Death detection (fires once)
        deathSub = StatsManager.Ins.GetReactive(StatType.CurrentHP)
            .Where(hp => hp <= 0f)
            .Take(1)
            .Subscribe(_ => Die())
            .AddTo(this);
    }

    void OnDisable()
    {
        regenSub?.Dispose(); regenSub = null;
        deathSub?.Dispose(); deathSub = null;
    }


    public void SetState(ClickerState newState)
    {
        currentState.OnExit(this);

        currentState = newState;

        currentState.OnEnter(this);
    }
    public void OnUpdate(IDamagable clickableObject)
    {
        currentState.OnUpdate(this, clickableObject);
    }

    public void OnClick(IDamagable clickableObject)
    {
        currentState.OnClick(clickableObject);
    }

    public void OnHold(IDamagable clickableObject)
    {
        if (StatsManager.Ins.Get(StatType.CurrentMana) > 10f)
        {
            currentState.OnHold(this, clickableObject);
        }
    }

    #region COMBAT_LOGIC -----------------------------------------------------------------------------------
    public void TakeDamage(float damage)
    {
        StatsManager.Ins.Set(
            StatType.CurrentHP,
            Mathf.Max(StatsManager.Ins.Get(StatType.CurrentHP) - damage, 0));
    }

    public void Heal(float amount)
    {
        StatsManager.Ins.Set(
            StatType.CurrentHP,
            Mathf.Max(StatsManager.Ins.Get(StatType.CurrentHP) + amount, StatsManager.Ins.Get(StatType.HP)));
    }
    public void UseMana()
    {
        manaRegenTimer += Time.deltaTime;

        if (manaRegenTimer >= timeManaReset)
        {
            float manaLostPerSecond = 10f * timeManaReset;
            StatsManager.Ins.Set(
                StatType.CurrentMana,
                Mathf.Max(StatsManager.Ins.Get(StatType.CurrentMana) - manaLostPerSecond, 0));
            manaRegenTimer = 0f;
        }
    }
    public void RegenMana()
    {
        manaRegenTimer += Time.deltaTime;

        if (manaRegenTimer >= timeManaReset)
        {
            float manaRegenerationPerSecond = StatsManager.Ins.Get(StatType.ManaRegen) * timeManaReset;
            StatsManager.Ins.Set(
                StatType.CurrentMana,
                Mathf.Min(StatsManager.Ins.Get(StatType.CurrentMana) + manaRegenerationPerSecond, StatsManager.Ins.Get(StatType.Mana)));
            manaRegenTimer = 0f;
        }
    }
    void Die()
    {
        if (IsDead) return;
        IsDead = true;

        OnDied?.Invoke();
    }
    public void Respawn(float hpPercent = 1f)
    {
        float max = StatsManager.Ins.Get(StatType.HP);
        StatsManager.Ins.Set(StatType.CurrentHP, Mathf.Clamp01(hpPercent) * max);
        IsDead = false;

        deathSub ??= StatsManager.Ins.GetReactive(StatType.CurrentHP)
                .Where(hp => hp <= 0f)
                .Take(1)
                .Subscribe(_ => Die())
                .AddTo(this);
    }

    #endregion
}

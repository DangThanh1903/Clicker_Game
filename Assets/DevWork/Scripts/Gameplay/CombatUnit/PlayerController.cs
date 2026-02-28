using System;
using System.Collections;
using Sirenix.OdinInspector;
using UniRx;
using UnityEngine;
using Lean.Pool;

public class PlayerController : MonoBehaviour
{
    private static PlayerController _instance;
    public static PlayerController Instance => _instance;
    private float manaRegenTimer = 0f;
    private float manaUsageTimer = 0f;
    private readonly float timeManaReset = 0.1f;

    public ClickerState currentState = new NormalState();
    public event Action OnDied;
    public bool IsDead { get; private set; }
    private int lastProcessedFrame = -1;
    private int pendingFrame = -1;
    private int pendingPriority = int.MinValue;
    private IDamagable pendingTarget;

    [Header("Tick Settings")]
    [SerializeField] float tickSeconds = 1f;
    [SerializeField] bool useUnscaledTime = true;
    IDisposable regenSub;
    IDisposable deathSub;

    [Header("Pickaxe")]
    [SerializeField] InventoryData pickaxeData;
    [SerializeField] private Transform holdBeamOrigin;

    private Pickaxe equippedPickaxe;
    private GameObject activeHoldBeamObject;
    private HoldBeamVFX activeHoldBeam;
    private Vector3 pendingHoldPoint;
    private float lastHoldUpdateTime = -999f;

    // Health and state setup
    private void Awake()
    {
        if (_instance == null) _instance = this;
        else Destroy(gameObject);

        SetUpCurrentStateItem();  
    }

    private void Start()
    {
        StatsManager.Ins.Set(StatType.CurrentHP, StatsManager.Ins.Get(StatType.HP));
        StatsManager.Ins.Set(StatType.CurrentMana, StatsManager.Ins.Get(StatType.Mana));
    }
    void Update()
    {
        if (pendingFrame == Time.frameCount &&
            lastProcessedFrame != Time.frameCount &&
            pendingTarget != null)
        {
            lastProcessedFrame = Time.frameCount;
            currentState.OnUpdate(this, pendingTarget);
        }

        // Hold mana regen should not depend on a target dispatch path.
        if (!IsDead &&
            currentState is HoldState &&
            Time.unscaledTime - lastHoldUpdateTime > 0.08f)
        {
            RegenMana();
        }

        UpdateHoldBeamLifecycle();
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
        SubscribeDeath();
    }

    void OnDisable()
    {
        regenSub?.Dispose(); regenSub = null;
        deathSub?.Dispose(); deathSub = null;
        StopHoldBeam(immediate: true);
    }

    void SubscribeDeath()
    {
        deathSub?.Dispose();
        deathSub = StatsManager.Ins?.GetReactive(StatType.CurrentHP)
            .Where(hp => hp <= 0f)
            .Take(1)
            .Subscribe(_ => Die())
            .AddTo(this);
    }
    void SetUpCurrentStateItem()
    {
        var item = pickaxeData.GetItem(0).itemData;
        SetEquippedPickaxe(item as Pickaxe);
    }
    public void SetStateByType(PickaxeType pickaxeType)
    {
        switch (pickaxeType)
        {
            case PickaxeType.Normal:
                SetState(new NormalState());
                return;
            case PickaxeType.Hold:
                SetState(new HoldState());
                return;
            case PickaxeType.Idle:
                SetState(new IdleState());
                return;
            default:
                SetState(new NormalState());
                return;
        }
    }
    public void SetState(ClickerState newState)
    {
        currentState.OnExit(this);

        currentState = newState;

        currentState.OnEnter(this);

        if (newState is not HoldState)
            StopHoldBeam();
    }
    public void OnUpdate(IDamagable clickableObject)
    {
        if (clickableObject == null) return;

        int frame = Time.frameCount;
        if (pendingFrame != frame)
        {
            pendingFrame = frame;
            pendingPriority = int.MinValue;
            pendingTarget = null;
        }

        int priority = GetPriority(clickableObject);
        if (priority >= pendingPriority)
        {
            pendingPriority = priority;
            pendingTarget = clickableObject;
        }
    }

    public void OnClick(IDamagable clickableObject)
    {
        currentState.OnClick(clickableObject);
    }

    public void OnHold(IDamagable clickableObject)
    {
        OnHold(clickableObject, Vector3.zero);
    }

    public void OnHold(IDamagable clickableObject, Vector3 holdPoint)
    {
        if (StatsManager.Ins.Get(StatType.CurrentMana) > 10f)
        {
            if (currentState is HoldState)
            {
                pendingHoldPoint = holdPoint;
                lastHoldUpdateTime = Time.unscaledTime;
                EnsureHoldBeam();
                UpdateHoldBeamPositions(pendingHoldPoint);
            }

            currentState.OnHold(this, clickableObject);
        }
        else
        {
            StopHoldBeam();
        }
    }

    private static int GetPriority(IDamagable target)
    {
        if (target is Boss) return 2;
        if (target is MonsterClickable) return 1;
        if (target is ClickableObject) return 0;
        return 0;
    }

    #region COMBAT_LOGIC -----------------------------------------------------------------------------------
    public void UseMana()
    {
        manaUsageTimer += Time.deltaTime;

        if (manaUsageTimer >= timeManaReset)
        {
            float manaLostPerSecond = 10f * timeManaReset;
            StatsManager.Ins.Set(
                StatType.CurrentMana,
                Mathf.Max(StatsManager.Ins.Get(StatType.CurrentMana) - manaLostPerSecond, 0));
            manaUsageTimer = 0f;
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
        StopHoldBeam();

        OnDied?.Invoke();

        Respawn(1f);
    }
    public void Respawn(float hpPercent = 1f)
    {
        float percent = Mathf.Clamp01(hpPercent);
        float max = StatsManager.Ins.Get(StatType.HP);
        StatsManager.Ins.Set(StatType.CurrentHP, max * percent);
        StatsManager.Ins.ForceNotifyStatsChanged();
        IsDead = false;

        SubscribeDeath();
    }

    public void SetEquippedPickaxe(Pickaxe pickaxe)
    {
        equippedPickaxe = pickaxe;

        if (equippedPickaxe == null || equippedPickaxe.Type == ItemType.None)
        {
            SetState(new NormalState());
            StopHoldBeam();
            return;
        }

        SetStateByType(equippedPickaxe.currentState);
    }

    private void UpdateHoldBeamLifecycle()
    {
        if (activeHoldBeamObject == null) return;

        bool invalidHold =
            IsDead ||
            currentState is not HoldState ||
            !Input.GetMouseButton(0) ||
            StatsManager.Ins.Get(StatType.CurrentMana) <= 10f;

        // Small tolerance avoids despawn/spawn jitter when one raycast frame is missed.
        if (!invalidHold && Time.unscaledTime - lastHoldUpdateTime > 0.15f)
            invalidHold = true;

        if (invalidHold)
            StopHoldBeam();
    }

    private void EnsureHoldBeam()
    {
        if (activeHoldBeamObject != null) return;
        if (equippedPickaxe == null) return;
        if (equippedPickaxe.HoldBeamVfxPrefab == null) return;

        activeHoldBeamObject = LeanPool.Spawn(equippedPickaxe.HoldBeamVfxPrefab);
        activeHoldBeam = activeHoldBeamObject.GetComponent<HoldBeamVFX>();
        Vector3 start = GetHoldBeamStartPosition();

        if (activeHoldBeam != null)
        {
            activeHoldBeam.Begin(start);
        }
        else
        {
            Debug.LogWarning("[PlayerController] Hold beam prefab is missing HoldBeamVFX component.");
        }
    }

    private void UpdateHoldBeamPositions(Vector3 endPoint)
    {
        if (activeHoldBeamObject == null) return;

        if (activeHoldBeam != null)
        {
            activeHoldBeam.SetEndPoint(endPoint);
        }
    }

    private Vector3 GetHoldBeamStartPosition()
    {
        Transform origin = holdBeamOrigin;

        if (origin == null && Camera.main != null)
            origin = Camera.main.transform;

        if (origin == null)
            return Vector3.zero;

        Vector3 offset = equippedPickaxe != null ? equippedPickaxe.HoldBeamStartOffset : Vector3.zero;
        return origin.position + origin.TransformDirection(offset);
    }

    private void StopHoldBeam(bool immediate = false)
    {
        if (activeHoldBeamObject == null)
        {
            activeHoldBeam = null;
            lastHoldUpdateTime = -999f;
            return;
        }

        GameObject beamToStop = activeHoldBeamObject;
        HoldBeamVFX beamVfx = activeHoldBeam;

        activeHoldBeamObject = null;
        activeHoldBeam = null;
        lastHoldUpdateTime = -999f;

        if (immediate || beamVfx == null)
        {
            LeanPool.Despawn(beamToStop);
            return;
        }

        beamVfx.EndBeam();
        float delay = beamVfx.EndDespawnDelay;
        if (delay <= 0f)
        {
            LeanPool.Despawn(beamToStop);
            return;
        }

        StartCoroutine(DespawnHoldBeamAfterDelay(beamToStop, delay));
    }

    private IEnumerator DespawnHoldBeamAfterDelay(GameObject beamObject, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        if (beamObject != null)
            LeanPool.Despawn(beamObject);
    }

    #endregion
}

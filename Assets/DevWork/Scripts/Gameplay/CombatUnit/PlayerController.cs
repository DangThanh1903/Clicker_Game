using System;
using System.Collections;
using UniRx;
using UnityEngine;
using Lean.Pool;

public class PlayerController : MonoBehaviour
{
    private static PlayerController _instance;
    public static PlayerController Instance => _instance;
    private const float HoldManaThreshold = 10f;
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
    [SerializeField, Min(0.05f)] private float idleAttackInterval = 1f;
    IDisposable regenSub;
    IDisposable deathSub;

    [Header("Pickaxe")]
    [SerializeField] InventoryData pickaxeData;
    [SerializeField] private Transform holdBeamOrigin;
    [SerializeField] private Transform idlePetVisualAnchor;

    private Pickaxe equippedPickaxe;
    private GameObject activeHoldBeamObject;
    private HoldBeamVFX activeHoldBeam;
    private GameObject activeIdlePetObject;
    private GameObject activeIdlePetPrefab;
    private IIdlePetAttackFeedback activeIdlePetFeedback;
    private Animator activeIdlePetAnimatorFallback;
    private Vector3 pendingHoldPoint;
    private float lastHoldUpdateTime = -999f;
    private float idleAttackTimer;
    private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");

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
        // Hold mana regen should not depend on a target dispatch path.
        if (!IsDead &&
            currentState is HoldState &&
            Time.unscaledTime - lastHoldUpdateTime > 0.08f)
        {
            RegenMana();
        }

        UpdateHoldBeamLifecycle();
    }

    void LateUpdate()
    {
        int frame = Time.frameCount;
        bool hasFreshTargetFrame = pendingFrame == frame || pendingFrame == frame - 1;
        bool isPendingTargetDamageable = IsTargetDamageable(pendingTarget);
        if (pendingTarget == null ||
            !isPendingTargetDamageable ||
            !hasFreshTargetFrame ||
            lastProcessedFrame == frame)
        {
            if (pendingTarget != null && !isPendingTargetDamageable)
                pendingTarget = null;
            return;
        }

        lastProcessedFrame = frame;
        currentState.OnUpdate(this, pendingTarget);
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
        pendingTarget = null;
        pendingFrame = -1;
        pendingPriority = int.MinValue;
        lastProcessedFrame = -1;
        StopHoldBeam(immediate: true);
        StopIdlePetVisual(immediate: true);
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
        if (pickaxeData == null || pickaxeData.GetSize() <= 0)
        {
            SetEquippedPickaxe(null);
            return;
        }

        var slot = pickaxeData.GetItem(0);
        var item = slot != null ? slot.itemData : null;
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

        if (newState is IdleState)
            idleAttackTimer = Mathf.Max(0.05f, idleAttackInterval); // first idle hit can happen immediately
        else
            idleAttackTimer = 0f;

        if (newState is not HoldState)
            StopHoldBeam();

        RefreshIdlePetVisual();
    }
    public void OnUpdate(IDamagable clickableObject)
    {
        if (clickableObject == null) return;
        if (!IsTargetDamageable(clickableObject)) return;

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
        if (StatsManager.Ins.Get(StatType.CurrentMana) > HoldManaThreshold)
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

    private static bool IsTargetDamageable(IDamagable target)
    {
        if (target == null)
            return false;

        if (target is MonsterClickable monster)
            return monster.isActiveAndEnabled &&
                   monster.MaxHealth > 0f &&
                   monster.CurrentHealth != null &&
                   monster.CurrentHealth.Value > 0f;

        if (target is ClickableObject block)
            return block.isActiveAndEnabled &&
                   block.MaxHealth > 0f &&
                   block.CurrentHealth != null &&
                   block.CurrentHealth.Value > 0f;

        if (target is Boss boss)
            return boss.isActiveAndEnabled;

        if (target is MonoBehaviour mb)
            return mb.isActiveAndEnabled;

        return true;
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
        StopIdlePetVisual();

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
            StopIdlePetVisual();
            return;
        }

        SetStateByType(equippedPickaxe.currentState);
        RefreshIdlePetVisual();
    }

    public void NotifyIdleDamageDealt(float damage, Vector3 targetWorldPosition)
    {
        if (IsDead || currentState is not IdleState)
            return;
        if (activeIdlePetObject == null)
            return;

        if (activeIdlePetFeedback == null && activeIdlePetAnimatorFallback == null)
            CacheIdlePetFeedbackRefs();

        if (activeIdlePetFeedback != null)
        {
            activeIdlePetFeedback.PlayIdleAttack(Mathf.Max(0f, damage), targetWorldPosition);
            return;
        }

        if (activeIdlePetAnimatorFallback != null)
            activeIdlePetAnimatorFallback.SetTrigger(AttackTriggerHash);
    }

    public void ProcessIdleAttack(IDamagable target)
    {
        if (target == null || currentState is not IdleState || IsDead)
            return;

        float interval = Mathf.Max(0.05f, idleAttackInterval);
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        idleAttackTimer += dt;
        if (idleAttackTimer < interval)
            return;

        int ticks = Mathf.Min(3, Mathf.FloorToInt(idleAttackTimer / interval));
        idleAttackTimer -= ticks * interval;

        for (int i = 0; i < ticks; i++)
            target.HandleIdle();
    }

    private void RefreshIdlePetVisual()
    {
        bool shouldShow =
            !IsDead &&
            equippedPickaxe != null &&
            equippedPickaxe.Type != ItemType.None &&
            currentState is IdleState &&
            equippedPickaxe.IdlePetVisualPrefab != null;

        if (!shouldShow)
        {
            StopIdlePetVisual();
            return;
        }

        GameObject prefab = equippedPickaxe.IdlePetVisualPrefab;
        Transform anchor = idlePetVisualAnchor != null ? idlePetVisualAnchor : transform;

        if (activeIdlePetObject == null || activeIdlePetPrefab != prefab)
        {
            StopIdlePetVisual(immediate: true);
            activeIdlePetObject = LeanPool.Spawn(
                prefab,
                anchor.position,
                anchor.rotation,
                anchor);

            // Ensure pooled instance starts exactly at anchor local origin.
            Transform spawnedPetTransform = activeIdlePetObject.transform;
            if (spawnedPetTransform.parent != anchor)
                spawnedPetTransform.SetParent(anchor, false);
            spawnedPetTransform.localPosition = Vector3.zero;

            activeIdlePetPrefab = prefab;
            CacheIdlePetFeedbackRefs();
        }

        if (activeIdlePetObject == null)
            return;

        Transform petTransform = activeIdlePetObject.transform;
        if (petTransform.parent != anchor)
            petTransform.SetParent(anchor, false);
    }

    private void StopIdlePetVisual(bool immediate = false)
    {
        if (activeIdlePetObject == null)
        {
            activeIdlePetPrefab = null;
            activeIdlePetFeedback = null;
            activeIdlePetAnimatorFallback = null;
            return;
        }

        GameObject petToDespawn = activeIdlePetObject;
        activeIdlePetObject = null;
        activeIdlePetPrefab = null;
        activeIdlePetFeedback = null;
        activeIdlePetAnimatorFallback = null;

        // No custom fade-out contract yet, keep lifecycle simple.
        if (immediate)
            LeanPool.Despawn(petToDespawn);
        else
            LeanPool.Despawn(petToDespawn);
    }

    private void CacheIdlePetFeedbackRefs()
    {
        activeIdlePetFeedback = null;
        activeIdlePetAnimatorFallback = null;

        if (activeIdlePetObject == null)
            return;

        var petBehaviours = activeIdlePetObject.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < petBehaviours.Length; i++)
        {
            if (activeIdlePetFeedback == null && petBehaviours[i] is IIdlePetAttackFeedback feedback)
                activeIdlePetFeedback = feedback;
        }

        if (activeIdlePetFeedback == null)
            activeIdlePetAnimatorFallback = activeIdlePetObject.GetComponentInChildren<Animator>(true);
    }

    private void UpdateHoldBeamLifecycle()
    {
        if (activeHoldBeamObject == null) return;

        bool invalidHold =
            IsDead ||
            currentState is not HoldState ||
            !Input.GetMouseButton(0) ||
            StatsManager.Ins.Get(StatType.CurrentMana) <= HoldManaThreshold;

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

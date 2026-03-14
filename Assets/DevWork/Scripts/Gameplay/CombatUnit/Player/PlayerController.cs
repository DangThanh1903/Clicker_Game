using UnityEngine;

public class PlayerController : MonoBehaviour, ICombatResourceReadModel, ICombatFeedbackSink, IRunFailNotifier
{
    private static PlayerController _instance;
    public static PlayerController Instance => _instance;
    private readonly GameplayInputGatePolicyService inputGatePolicyService = new GameplayInputGatePolicyService();
    private readonly PlayerCombatResourceService combatResourceService = new PlayerCombatResourceService();
    private readonly PlayerRunLifecycleService runLifecycleService = new PlayerRunLifecycleService();
    private readonly PlayerPointerDispatchService pointerDispatchService = new PlayerPointerDispatchService();
    private readonly PlayerIdleAttackTickService idleAttackTickService = new PlayerIdleAttackTickService();
    private readonly ClickPerTickService clickPerTickService = new ClickPerTickService();
    private PlayerCombatVfxService combatVfxService;

    public ClickerState currentState = new NormalState();
    public event System.Action<PlayerRunFailReason> OnRunFailed;
    public bool IsDead { get; private set; } // Reserved for future fail-state wiring.
    private IDamageReceiver pendingTarget;
    private ITargetRegistry targetRegistry;
    private RuntimeDamageTargetRegistry ownedTargetRegistry;
    private IDamageTargetSelectionService targetSelectionService = PriorityDamageTargetSelectionService.Instance;
    private IPointerDamageTargetResolver pointerTargetResolver = PhysicsPointerDamageTargetResolver.Instance;

    [SerializeField] bool useUnscaledTime = true;

    [Header("Pointer Raycast")]
    [SerializeField] private LayerMask pointerRaycastLayers = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0.1f)] private float pointerRaycastDistance = 200f;
    [SerializeField] private QueryTriggerInteraction pointerRaycastTriggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Pickaxe")]
    [SerializeField] InventoryData pickaxeData;
    [SerializeField] private Transform holdBeamOrigin;
    [SerializeField] private Transform idlePetVisualAnchor;

    private Pickaxe equippedPickaxe;
    private const float TimeUnset = float.NegativeInfinity;
    private float lastHoldUpdateTime = TimeUnset;
    public int IdleStackCount => combatResourceService.IdleStackCount;
    public bool UseUnscaledTime => useUnscaledTime;
    private float CombatNow => useUnscaledTime ? Time.unscaledTime : Time.time;
    private float CombatDelta => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    // Health and state setup
    private void Awake()
    {
        if (_instance == null)
            _instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        if (ownedTargetRegistry == null)
            ownedTargetRegistry = new RuntimeDamageTargetRegistry();

        BindCombatRuntimeBootstrap();
        ConfigurePointerResolver();
        combatVfxService = new PlayerCombatVfxService(this);
        runLifecycleService.RunFailed += HandleRunFailed;

        SetUpCurrentStateItem();  
    }

    private void Start()
    {
        if (StatsManager.Ins == null)
            return;

        IsDead = false;
        combatResourceService.InitializeResources();
        clickPerTickService.ResetRuntime();
        StatsManager.Ins.ForceNotifyStatsChanged();
    }
    void Update()
    {
        pointerDispatchService.Tick(
            IsGameplayInputAllowed(),
            pointerTargetResolver,
            targetSelectionService,
            currentState is not HoldState,
            currentState is HoldState,
            OnClick,
            OnHold);

        clickPerTickService.Tick(CombatNow);
        combatResourceService.Tick(
            false,
            currentState is HoldState,
            CombatNow,
            CombatDelta,
            lastHoldUpdateTime);

        if (combatVfxService != null &&
            combatVfxService.UpdateHoldBeamLifecycle(
                IsDead,
                currentState is HoldState,
                Input.GetMouseButton(0),
                CombatNow,
                lastHoldUpdateTime))
        {
            lastHoldUpdateTime = TimeUnset;
        }
    }

    void LateUpdate()
    {
        RefreshPendingTargetFromRegistry();
        if (!targetSelectionService.CanReceiveDamage(pendingTarget))
        {
            pendingTarget = null;
            return;
        }

        currentState.OnUpdate(this, pendingTarget);
    }
    void OnDisable()
    {
        runLifecycleService.RunFailed -= HandleRunFailed;
        combatResourceService.ResetRuntime();
        runLifecycleService.ResetRuntime();
        clickPerTickService.ResetRuntime();
        pointerDispatchService.ResetRuntime();
        idleAttackTickService.ResetRuntime();
        CombatRuntimeBootstrap.UnbindOwner(this);
        IsDead = false;
        pendingTarget = null;
        lastHoldUpdateTime = TimeUnset;
        combatVfxService?.ResetImmediate();
    }

    private void OnEnable()
    {
        if (ownedTargetRegistry == null)
            ownedTargetRegistry = new RuntimeDamageTargetRegistry();

        BindCombatRuntimeBootstrap();
        ConfigurePointerResolver();
        runLifecycleService.RunFailed -= HandleRunFailed;
        runLifecycleService.RunFailed += HandleRunFailed;
        clickPerTickService.ResetRuntime();
        pointerDispatchService.ResetRuntime();
        IsDead = false;
    }

    private void BindCombatRuntimeBootstrap()
    {
        CombatRuntimeBootstrap.BindAll(
            this,
            this,
            this,
            this,
            ownedTargetRegistry);

        targetRegistry = ownedTargetRegistry;
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
        combatResourceService.OnStateChanged();

        currentState.OnEnter(this);

        idleAttackTickService.OnStateChanged(
            newState is IdleState,
            GetSummonAttackInterval()); // first idle hit can happen immediately

        if (newState is not HoldState)
            lastHoldUpdateTime = TimeUnset;

        combatVfxService?.HandleStateChanged(
            equippedPickaxe,
            newState,
            IsDead,
            idlePetVisualAnchor,
            transform);
    }
    public void OnClick(IDamageReceiver clickableObject)
    {
        combatResourceService.OnClickDispatched(currentState is IdleState);
        currentState.OnClick(clickableObject);
    }

    public void SetTargetRegistry(ITargetRegistry registry)
    {
        if (registry == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[PlayerController] SetTargetRegistry requires a non-null registry.", this);
#endif
            return;
        }

        targetRegistry = registry;
    }

    public void SetTargetSelectionService(IDamageTargetSelectionService selectionService)
    {
        if (selectionService == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[PlayerController] SetTargetSelectionService requires a non-null selection service.", this);
#endif
            return;
        }

        targetSelectionService = selectionService;
    }

    public void SetPointerTargetResolver(IPointerDamageTargetResolver resolver)
    {
        if (resolver == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[PlayerController] SetPointerTargetResolver requires a non-null resolver.", this);
#endif
            return;
        }

        pointerTargetResolver = resolver;
        ConfigurePointerResolver();
    }

    private void RefreshPendingTargetFromRegistry()
    {
        pendingTarget = targetSelectionService.SelectBestTarget(targetRegistry);
    }

    private void ConfigurePointerResolver()
    {
        if (pointerTargetResolver is PhysicsPointerDamageTargetResolver physicsResolver)
        {
            physicsResolver.ConfigureRaycast(
                pointerRaycastLayers.value,
                Mathf.Max(0.1f, pointerRaycastDistance),
                pointerRaycastTriggerInteraction);
        }
    }

    public float ApplyStaminaToFinalDamage(float finalClickDamage)
    {
        return combatResourceService.ApplyStaminaToFinalDamage(
            finalClickDamage,
            currentState is NormalState);
    }

    public float GetStaminaPercent()
    {
        return combatResourceService.GetStaminaPercent();
    }

    public float GetIdleDamageMultiplier()
    {
        return combatResourceService.GetIdleDamageMultiplier();
    }

    public float GetIdleStackPercent()
    {
        return combatResourceService.GetIdleStackPercent();
    }

    public void OnHold(IDamageReceiver clickableObject, Vector3 holdPoint)
    {
        if (currentState is HoldState)
        {
            lastHoldUpdateTime = CombatNow;
            combatVfxService?.OnHold(equippedPickaxe, holdBeamOrigin, holdPoint);
        }

        currentState.OnHold(this, clickableObject);
    }

    public float GetHoldDamageMultiplier()
    {
        return combatResourceService.GetHoldDamageMultiplier();
    }

    public void NotifyRunFailed(PlayerRunFailReason reason)
    {
        runLifecycleService.NotifyRunFailed(reason);
    }

    public void NotifyDamageHit()
    {
        clickPerTickService.RecordHit();
    }

    public int GetRecentHitCount(float windowSeconds = 1f)
    {
        return clickPerTickService.GetRecentHitCount(windowSeconds);
    }

    private void HandleRunFailed(PlayerRunFailReason reason)
    {
        pointerDispatchService.CancelHold();
        pendingTarget = null;
        lastHoldUpdateTime = TimeUnset;
        combatVfxService?.ResetImmediate();
        OnRunFailed?.Invoke(reason);
    }

    private bool IsGameplayInputAllowed()
    {
        return inputGatePolicyService.IsCombatInputAllowed();
    }

    #region COMBAT_LOGIC -----------------------------------------------------------------------------------
    public void UseMana()
    {
        combatResourceService.ConsumeMana(CombatDelta);
    }

    public void SetEquippedPickaxe(Pickaxe pickaxe)
    {
        equippedPickaxe = pickaxe;

        if (equippedPickaxe == null || equippedPickaxe.Type == ItemType.None)
        {
            SetState(new NormalState());
            lastHoldUpdateTime = TimeUnset;
            combatVfxService?.HandleEquippedPickaxeCleared();
            return;
        }

        SetStateByType(equippedPickaxe.currentState);
    }

    public void NotifyIdleDamageDealt(float damage, Vector3 targetWorldPosition)
    {
        combatVfxService?.NotifyIdleDamageDealt(
            IsDead,
            currentState is IdleState,
            damage,
            targetWorldPosition);
    }

    public void ProcessIdleAttack(IDamageReceiver target)
    {
        idleAttackTickService.TickAndDispatch(
            target,
            currentState is IdleState,
            IsDead,
            CombatDelta,
            GetSummonAttackInterval());
    }

    private float GetSummonAttackInterval()
    {
        float summonAttackSpeed = StatsManager.Ins != null
            ? StatsManager.Ins.Get(StatType.SummonAttackSpeed)
            : 0f;
        summonAttackSpeed = Mathf.Max(0.05f, summonAttackSpeed);
        return 1f / summonAttackSpeed;
    }

    #endregion
}

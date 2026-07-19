using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UniRx;
using UnityEngine;

public sealed class JournalManager : MonoBehaviour
{
    public static JournalManager Ins { get; private set; }

    private const string JournalResourcePath = "Journal/JournalDatabase";

    [SerializeField] private JournalDatabaseSO database;
    [SerializeField] private RecipeDatabase recipeDatabase;
    [SerializeField, Min(0.05f)] private float saveThrottleSeconds = 0.2f;
    [SerializeField] private bool verboseLogs;

    public event Action StateChanged;
    public event Action<JournalHudViewModel> HudChanged;
    public event Action<IReadOnlyList<JournalStepViewModel>> MenuChanged;
    public event Action<JournalStepData> StepCompleted;

    public string CurrentBiomeId { get; private set; }
    public JournalStepData CurrentStep { get; private set; }
    public bool IsReady { get; private set; }

    private readonly List<JournalStepViewModel> menuBuffer = new();
    private readonly List<JournalIngredientProgressView> lineBuffer = new();
    private readonly CompositeDisposable inventoryDisposables = new();
    private readonly StringComparer comparer = StringComparer.OrdinalIgnoreCase;

    private IJournalStorage storage;
    private JournalProgressSave save;
    private JournalUnlockService unlocks;
    private IDisposable locationSubscription;
    private LocationLoader boundLocationLoader;
    private InventoryData boundMainInventory;
    private bool pendingSave;
    private float nextSaveTime;
    private bool initializing;
    private Coroutine initializeCo;

    public static JournalManager GetOrCreate()
    {
        if (Ins != null)
            return Ins;

        JournalManager existing = FindObjectsByType<JournalManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault();
        if (existing != null)
            return existing;

        var go = new GameObject(nameof(JournalManager));
        return go.AddComponent<JournalManager>();
    }

    private void Awake()
    {
        if (Ins != null && Ins != this)
        {
            Destroy(gameObject);
            return;
        }

        Ins = this;
        DontDestroyOnLoad(gameObject);
        storage = new JsonJournalStorage();
        unlocks = new JournalUnlockService();
        InitializeUnlockState();
    }

    private void OnEnable()
    {
        BindSignals();
        if (initializeCo == null)
            initializeCo = StartCoroutine(InitializeWhenReady());
    }

    private void OnDisable()
    {
        UnbindSignals();
        UnbindLocationLoader();
        UnbindInventory();

        if (initializeCo != null)
        {
            StopCoroutine(initializeCo);
            initializeCo = null;
        }
    }

    private IEnumerator InitializeWhenReady()
    {
        initializing = true;

        if (DataSaver.Ins != null)
            yield return new WaitUntil(() => DataSaver.Ins.IsReady);

        yield return new WaitUntil(() => LocationLoader.Ins != null && LocationLoader.Ins.IsInitialized);

        initializeCo = null;
        initializing = false;
        InitializeRuntime();
    }

    private void Update()
    {
        if (pendingSave && Time.unscaledTime >= nextSaveTime)
            SaveNow();

        if (boundLocationLoader == null && LocationLoader.Ins != null && LocationLoader.Ins.IsInitialized)
            BindLocationLoader(LocationLoader.Ins);

        if (boundMainInventory == null)
            BindInventory(ResolveMainInventory());
    }

    private void InitializeRuntime()
    {
        if (IsReady)
            return;

        if (database == null)
        {
            Debug.LogWarning("[Journal] Missing JournalDatabaseSO. Journal will stay disabled.");
            return;
        }

        BindLocationLoader(LocationLoader.Ins);
        BindInventory(ResolveMainInventory());
        ReapplyBiomeUnlocks();
        ReapplyRecipeUnlocks();
        RefreshCurrentStep();

        IsReady = true;
        PublishAll();
    }

    private void ResolveRefs()
    {
        if (database == null)
            database = Resources.Load<JournalDatabaseSO>(JournalResourcePath);

        if (recipeDatabase == null)
        {
            CraftRecipePanel[] panels = FindObjectsByType<CraftRecipePanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] != null && panels[i].recipeDB != null)
                {
                    recipeDatabase = panels[i].recipeDB;
                    break;
                }
            }

            if (recipeDatabase == null)
                recipeDatabase = Resources.FindObjectsOfTypeAll<RecipeDatabase>().FirstOrDefault();
        }
    }

    private void InitializeUnlockState()
    {
        ResolveRefs();
        if (database == null)
            return;

        save ??= storage.Load();
        MergeSaveWithDatabase(save);
        unlocks.Initialize(database, save);
        CurrentBiomeId = string.IsNullOrWhiteSpace(save.currentBiomeId)
            ? BlockSpawnLocation.Plain.ToString()
            : save.currentBiomeId;
        CurrentStep = GetActiveStep(CurrentBiomeId);
        UpdateSavePointers();
    }

    private void BindSignals()
    {
        GameplayProgressSignals.BlockBroken += HandleBlockBroken;
        GameplayProgressSignals.ItemCollected += HandleItemCollected;
        GameplayProgressSignals.ItemCrafted += HandleItemCrafted;
        GameplayProgressSignals.BossKilled += HandleBossKilled;
        GameplayProgressSignals.BlockDiscovered += HandleBlockDiscovered;
    }

    private void UnbindSignals()
    {
        GameplayProgressSignals.BlockBroken -= HandleBlockBroken;
        GameplayProgressSignals.ItemCollected -= HandleItemCollected;
        GameplayProgressSignals.ItemCrafted -= HandleItemCrafted;
        GameplayProgressSignals.BossKilled -= HandleBossKilled;
        GameplayProgressSignals.BlockDiscovered -= HandleBlockDiscovered;
    }

    private void BindLocationLoader(LocationLoader loader)
    {
        if (boundLocationLoader == loader)
            return;

        UnbindLocationLoader();
        boundLocationLoader = loader;
        if (boundLocationLoader == null)
            return;

        boundLocationLoader.CurrentCraftNodeManagerChanged += HandleCraftNodeManagerChanged;
        if (boundLocationLoader.ReactiveLocation != null)
        {
            locationSubscription = boundLocationLoader.ReactiveLocation
                .DistinctUntilChanged()
                .Subscribe(HandleLocationChanged);
        }

        HandleLocationChanged(boundLocationLoader.currentLocation);
    }

    private void UnbindLocationLoader()
    {
        if (boundLocationLoader != null)
            boundLocationLoader.CurrentCraftNodeManagerChanged -= HandleCraftNodeManagerChanged;

        locationSubscription?.Dispose();
        locationSubscription = null;
        boundLocationLoader = null;
    }

    private void BindInventory(InventoryData inventory)
    {
        if (boundMainInventory == inventory)
            return;

        UnbindInventory();
        boundMainInventory = inventory;
        if (boundMainInventory == null)
            return;

        boundMainInventory.InventoryChanged
            .ThrottleFrame(1)
            .Subscribe(_ => HandleInventoryStateChanged())
            .AddTo(inventoryDisposables);

        boundMainInventory.Items.ObserveReplace()
            .ThrottleFrame(1)
            .Subscribe(_ => HandleInventoryStateChanged())
            .AddTo(inventoryDisposables);

        boundMainInventory.Items.ObserveReset()
            .ThrottleFrame(1)
            .Subscribe(_ => HandleInventoryStateChanged())
            .AddTo(inventoryDisposables);
    }

    private void UnbindInventory()
    {
        inventoryDisposables.Clear();
        boundMainInventory = null;
    }

    private InventoryData ResolveMainInventory()
    {
        return InventoryController.Instance != null && InventoryController.Instance.InventoryUIManager != null
            ? InventoryController.Instance.InventoryUIManager.GetInventoryData(InventoryType.Inventory)
            : null;
    }

    private void HandleLocationChanged(BlockSpawnLocation biome)
    {
        CurrentBiomeId = biome.ToString();
        RefreshCurrentStep();
        ReapplyRecipeUnlocks();
        QueueSave();
        PublishAll();
    }

    private void HandleCraftNodeManagerChanged(CraftNodeManager _)
    {
        ReapplyRecipeUnlocks();
        PublishAll();
    }

    private void HandleBlockBroken(string blockId, string biomeId, int amount)
    {
        TryAdvanceStepForBiome(biomeId, JournalGoalType.BreakBlock, blockId, amount);
    }

    private void HandleItemCollected(string itemId, int amount)
    {
        TryAdvanceStepForBiome(CurrentBiomeId, JournalGoalType.CollectItem, itemId, amount);
    }

    private void HandleItemCrafted(string itemId, int amount)
    {
        TryAdvanceStepForBiome(CurrentBiomeId, JournalGoalType.CraftItem, itemId, amount);
    }

    private void HandleBossKilled(string bossId, string biomeId)
    {
        TryAdvanceStepForBiome(biomeId, JournalGoalType.KillBoss, bossId, 1);
    }

    private void HandleBlockDiscovered(string blockId, string biomeId)
    {
        TryAdvanceStepForBiome(biomeId, JournalGoalType.DiscoverBlock, blockId, 1);
    }

    private void TryAdvanceStepForBiome(string biomeId, JournalGoalType goalType, string targetId, int amount)
    {
        if (!IsReady || string.IsNullOrWhiteSpace(biomeId))
            return;

        JournalStepData step = GetActiveStep(biomeId);
        if (step == null || step.goalType != goalType)
            return;

        if (!MatchesTarget(step.targetId, targetId))
            return;

        AddProgress(step, amount);
    }

    private void AddProgress(JournalStepData step, int amount)
    {
        if (step == null)
            return;

        JournalStepProgressSave stepSave = GetOrCreateStepSave(step);
        if (stepSave.completed)
            return;

        int required = Mathf.Max(1, step.requiredAmount);
        int before = Mathf.Clamp(stepSave.currentAmount, 0, required);
        int after = Mathf.Clamp(before + Mathf.Max(0, amount), 0, required);
        if (after == before && after < required)
            return;

        stepSave.currentAmount = after;
        if (after >= required)
            CompleteStep(step, stepSave);
        else
            OnJournalStateChanged();

        if (verboseLogs)
            Debug.Log($"[Journal] Progress step={step.id} {before}->{stepSave.currentAmount}/{required}");
    }

    private void CompleteStep(JournalStepData step, JournalStepProgressSave stepSave)
    {
        if (step == null || stepSave == null || stepSave.completed)
            return;

        stepSave.completed = true;
        stepSave.currentAmount = Mathf.Max(stepSave.currentAmount, Mathf.Max(1, step.requiredAmount));

        if (!stepSave.rewardGranted)
        {
            GrantRewards(step);
            stepSave.rewardGranted = true;
        }

        if (!stepSave.unlocksApplied)
        {
            if (unlocks.ApplyUnlocks(step, save))
                ApplyRuntimeUnlocks(step);

            stepSave.unlocksApplied = true;
        }

        RefreshCurrentStep();
        QueueSave();
        SaveNow();
        StepCompleted?.Invoke(step);
        EmitCompletionToast(step);
        PublishAll();
    }

    private void ApplyRuntimeUnlocks(JournalStepData step)
    {
        if (step?.unlocks == null)
        {
            ReapplyBiomeUnlocks();
            ReapplyRecipeUnlocks();
            return;
        }

        for (int i = 0; i < step.unlocks.Count; i++)
        {
            JournalUnlockData unlock = step.unlocks[i];
            if (unlock == null || string.IsNullOrWhiteSpace(unlock.targetId))
                continue;

            if (unlock.type == JournalUnlockType.Biome &&
                Enum.TryParse(unlock.targetId, true, out BlockSpawnLocation biome) &&
                LocationLoader.Ins != null)
            {
                LocationLoader.Ins.TryUnlockLocation(biome);
            }
        }

        ReapplyBiomeUnlocks();
        ReapplyRecipeUnlocks();
    }

    private void GrantRewards(JournalStepData step)
    {
        if (step?.rewards == null || step.rewards.Count == 0)
            return;

        var inventory = InventoryController.Instance;
        var itemsToGrant = new List<InventoryItem>();

        for (int i = 0; i < step.rewards.Count; i++)
        {
            JournalRewardData reward = step.rewards[i];
            if (reward == null)
                continue;

            if (reward.diamonds > 0 && StatsManager.Ins != null)
            {
                StatsManager.Ins.Add(StatType.Diamond, reward.diamonds);
                AnalyticsManager.Ins?.TrackCurrencyEarn("gems", reward.diamonds, $"journal:{step.id}");
            }

            if (reward.item == null || reward.item.Type == ItemType.None || reward.amount <= 0)
                continue;

            itemsToGrant.Add(new InventoryItem(reward.item, reward.amount));
        }

        if (itemsToGrant.Count == 0 || inventory == null)
            return;

        if (!inventory.CanFullyAddItems(itemsToGrant))
        {
            Debug.LogWarning($"[Journal] Inventory full while granting rewards for step '{step.id}'. Progress will still advance.");
            return;
        }

        for (int i = 0; i < itemsToGrant.Count; i++)
            inventory.TryAddItemToInventory(itemsToGrant[i], requireFullAdd: true);
    }

    private void RefreshCurrentStep()
    {
        CurrentStep = GetActiveStep(CurrentBiomeId);
        TryAutoAdvanceInventoryBackedStep();
        TryAutoCompleteCurrentStep();
        UpdateSavePointers();
    }

    private void TryAutoCompleteCurrentStep()
    {
        if (CurrentStep == null || CurrentStep.goalType != JournalGoalType.CompleteBiome)
            return;

        if (GetBiomeCompletionPercent(CurrentBiomeId) >= 100)
        {
            JournalStepProgressSave stepSave = GetOrCreateStepSave(CurrentStep);
            CompleteStep(CurrentStep, stepSave);
        }
    }

    private void OnJournalStateChanged()
    {
        RefreshCurrentStep();
        QueueSave();
        PublishAll();
    }

    private void QueueSave()
    {
        if (save == null || storage == null)
            return;

        pendingSave = true;
        nextSaveTime = Time.unscaledTime + Mathf.Max(0.05f, saveThrottleSeconds);
    }

    private void SaveNow()
    {
        if (save == null || storage == null)
            return;

        pendingSave = false;
        storage.Save(save);
    }

    private void PublishAll()
    {
        if (database == null)
            return;

        StateChanged?.Invoke();
        if (!IsReady)
            return;

        HudChanged?.Invoke(BuildHudView());
        MenuChanged?.Invoke(BuildMenuView());
    }

    private JournalHudViewModel BuildHudView()
    {
        string biomeTitle = ResolveBiomeTitle(CurrentBiomeId);
        int percent = GetBiomeCompletionPercent(CurrentBiomeId);
        bool hasBiomeData = database != null && database.GetBiome(CurrentBiomeId) != null;
        string stepTitle = CurrentStep != null
            ? ResolveStepTitle(CurrentStep)
            : (hasBiomeData ? "Journal complete" : "No journal data");
        string stepDescription = CurrentStep != null ? (CurrentStep.description ?? string.Empty) : string.Empty;
        IReadOnlyList<JournalIngredientProgressView> lines = CurrentStep != null
            ? BuildProgressLines(CurrentStep)
            : Array.Empty<JournalIngredientProgressView>();

        return new JournalHudViewModel(biomeTitle, percent, stepTitle, stepDescription, lines);
    }

    private IReadOnlyList<JournalStepViewModel> BuildMenuView()
    {
        menuBuffer.Clear();

        JournalBiomeData biome = database.GetBiome(CurrentBiomeId);
        if (biome?.steps == null)
            return menuBuffer;

        JournalStepData activeStep = GetActiveStep(CurrentBiomeId);
        bool passedActiveStep = false;

        foreach (JournalStepData step in biome.steps.OrderBy(step => step.order))
        {
            if (step == null)
                continue;

            JournalStepProgressSave stepSave = GetOrCreateStepSave(step);
            bool isCompleted = stepSave.completed;
            bool isActive = !isCompleted && activeStep != null && comparer.Equals(activeStep.id, step.id);
            bool isLocked = !isCompleted && !isActive && passedActiveStep;
            if (isActive)
                passedActiveStep = true;

            menuBuffer.Add(new JournalStepViewModel(
                step.id,
                ResolveStepTitle(step),
                step.description ?? string.Empty,
                isCompleted,
                isActive,
                isLocked,
                ResolveStepIcon(step),
                BuildProgressLines(step),
                BuildRewardPreview(step),
                BuildUnlockPreview(step)));
        }

        return menuBuffer;
    }

    private IReadOnlyList<JournalIngredientProgressView> BuildProgressLines(JournalStepData step)
    {
        lineBuffer.Clear();
        if (step == null)
            return lineBuffer.ToArray();

        if (step.goalType == JournalGoalType.CraftItem)
        {
            Recipe recipe = recipeDatabase != null ? recipeDatabase.FindFirstRecipeByResultName(step.targetId) : null;
            if (recipe != null)
            {
                InventoryData inventory = boundMainInventory ?? ResolveMainInventory();
                if (recipe.ingredients != null)
                {
                    for (int i = 0; i < recipe.ingredients.Count; i++)
                    {
                        InventoryItem ingredient = recipe.ingredients[i];
                        if (ingredient == null || ingredient.itemData == null || ingredient.itemData.Type == ItemType.None || ingredient.quantity == null)
                            continue;

                        int required = Mathf.Max(0, ingredient.quantity.Value);
                        if (required <= 0)
                            continue;

                        int current = GetInventoryAmount(inventory, ingredient.itemData);
                        lineBuffer.Add(new JournalIngredientProgressView(ingredient.itemData.itemName, current, required));
                    }
                }
            }
        }

        if (lineBuffer.Count == 0)
        {
            JournalStepProgressSave stepSave = GetOrCreateStepSave(step);
            lineBuffer.Add(new JournalIngredientProgressView(ResolveProgressLabel(step), stepSave.currentAmount, Mathf.Max(1, step.requiredAmount)));
        }

        return lineBuffer.ToArray();
    }

    private static int GetInventoryAmount(InventoryData inventory, Item item)
    {
        if (inventory == null || item == null || inventory.Items == null)
            return 0;

        int total = 0;
        for (int i = 0; i < inventory.Items.Count; i++)
        {
            InventoryItem slot = inventory.Items[i];
            if (slot?.itemData != item || slot.quantity == null)
                continue;

            total += Mathf.Max(0, slot.quantity.Value);
        }

        return total;
    }

    private string ResolveProgressLabel(JournalStepData step)
    {
        return step.goalType switch
        {
            JournalGoalType.BreakBlock => step.targetId,
            JournalGoalType.CollectItem => step.targetId,
            JournalGoalType.CraftItem => step.targetId,
            JournalGoalType.KillBoss => step.targetId,
            JournalGoalType.DiscoverBlock => step.targetId,
            JournalGoalType.CompleteBiome => ResolveBiomeTitle(step.biomeId),
            _ => step.targetId
        };
    }

    private string BuildRewardPreview(JournalStepData step)
    {
        if (step?.rewards == null || step.rewards.Count == 0)
            return string.Empty;

        List<string> parts = new();
        for (int i = 0; i < step.rewards.Count; i++)
        {
            JournalRewardData reward = step.rewards[i];
            if (reward == null)
                continue;

            if (reward.item != null && reward.item.Type != ItemType.None && reward.amount > 0)
                parts.Add($"{reward.item.itemName} x{reward.amount}");
            if (reward.diamonds > 0)
                parts.Add($"{reward.diamonds} Diamonds");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : string.Empty;
    }

    private string BuildUnlockPreview(JournalStepData step)
    {
        if (step?.unlocks == null || step.unlocks.Count == 0)
            return string.Empty;

        List<string> parts = new();
        for (int i = 0; i < step.unlocks.Count; i++)
        {
            JournalUnlockData unlock = step.unlocks[i];
            if (unlock == null || string.IsNullOrWhiteSpace(unlock.targetId))
                continue;

            parts.Add($"{unlock.type}: {unlock.targetId}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : string.Empty;
    }

    private Sprite ResolveStepIcon(JournalStepData step)
    {
        if (step == null)
            return null;

        if (step.goalType == JournalGoalType.CraftItem || step.goalType == JournalGoalType.CollectItem)
        {
            Item item = ResolveItemByName(step.targetId);
            if (item != null)
                return item.icon;
        }

        if (step.rewards != null)
        {
            for (int i = 0; i < step.rewards.Count; i++)
            {
                if (step.rewards[i]?.item != null)
                    return step.rewards[i].item.icon;
            }
        }

        return null;
    }

    private Item ResolveItemByName(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        if (recipeDatabase != null)
        {
            Recipe recipe = recipeDatabase.FindFirstRecipeByResultName(itemId);
            if (recipe?.result?.itemData != null)
                return recipe.result.itemData;
        }

        Item[] items = Resources.FindObjectsOfTypeAll<Item>();
        for (int i = 0; i < items.Length; i++)
        {
            Item item = items[i];
            if (item == null)
                continue;

            if (string.Equals(item.itemName, itemId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.name, itemId, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    private string ResolveBiomeTitle(string biomeId)
    {
        JournalBiomeData biome = database != null ? database.GetBiome(biomeId) : null;
        return !string.IsNullOrWhiteSpace(biome?.title) ? biome.title : (biomeId ?? string.Empty);
    }

    private string ResolveStepTitle(JournalStepData step)
    {
        if (step == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(step.title))
            return step.title;

        return step.goalType switch
        {
            JournalGoalType.BreakBlock => $"Break {step.requiredAmount} {step.targetId}",
            JournalGoalType.CollectItem => $"Collect {step.requiredAmount} {step.targetId}",
            JournalGoalType.CraftItem => $"Craft {step.targetId}",
            JournalGoalType.KillBoss => $"Defeat {step.targetId}",
            JournalGoalType.DiscoverBlock => $"Discover {step.targetId}",
            JournalGoalType.CompleteBiome => $"Complete {ResolveBiomeTitle(step.biomeId)}",
            _ => step.id
        };
    }

    private int GetBiomeCompletionPercent(string biomeId)
    {
        JournalBiomeData biome = database != null ? database.GetBiome(biomeId) : null;
        if (biome?.steps == null || biome.steps.Count == 0)
            return 0;

        int total = 0;
        int completed = 0;
        for (int i = 0; i < biome.steps.Count; i++)
        {
            JournalStepData step = biome.steps[i];
            if (step == null)
                continue;

            total++;
            if (GetOrCreateStepSave(step).completed)
                completed++;
        }

        if (total <= 0)
            return 0;

        return Mathf.Clamp(Mathf.RoundToInt((completed / (float)total) * 100f), 0, 100);
    }

    private JournalStepData GetActiveStep(string biomeId)
    {
        JournalBiomeData biome = database != null ? database.GetBiome(biomeId) : null;
        if (biome?.steps == null || biome.steps.Count == 0)
            return null;

        foreach (JournalStepData step in biome.steps.OrderBy(step => step.order))
        {
            if (step == null)
                continue;

            if (!GetOrCreateStepSave(step).completed)
                return step;
        }

        return null;
    }

    private JournalBiomeProgressSave GetOrCreateBiomeSave(string biomeId)
    {
        save ??= new JournalProgressSave();
        save.biomes ??= new List<JournalBiomeProgressSave>();

        for (int i = 0; i < save.biomes.Count; i++)
        {
            JournalBiomeProgressSave biome = save.biomes[i];
            if (biome != null && comparer.Equals(biome.biomeId, biomeId))
                return biome;
        }

        var created = new JournalBiomeProgressSave
        {
            biomeId = biomeId ?? string.Empty,
            steps = new List<JournalStepProgressSave>()
        };
        save.biomes.Add(created);
        return created;
    }

    private JournalStepProgressSave GetOrCreateStepSave(JournalStepData step)
    {
        JournalBiomeProgressSave biomeSave = GetOrCreateBiomeSave(step.biomeId);
        for (int i = 0; i < biomeSave.steps.Count; i++)
        {
            JournalStepProgressSave stepSave = biomeSave.steps[i];
            if (stepSave != null && comparer.Equals(stepSave.stepId, step.id))
                return stepSave;
        }

        var created = new JournalStepProgressSave
        {
            stepId = step.id ?? string.Empty,
            currentAmount = 0
        };
        biomeSave.steps.Add(created);
        return created;
    }

    private void MergeSaveWithDatabase(JournalProgressSave targetSave)
    {
        if (targetSave == null || database == null)
            return;

        foreach (JournalBiomeData biome in database.GetSortedBiomes())
        {
            JournalBiomeProgressSave biomeSave = GetOrCreateBiomeSave(biome.biomeId);
            if (biome.steps == null)
                continue;

            for (int i = 0; i < biome.steps.Count; i++)
            {
                JournalStepData step = biome.steps[i];
                if (step == null || string.IsNullOrWhiteSpace(step.id))
                    continue;

                GetOrCreateStepSave(step);
            }

            biomeSave.steps = biomeSave.steps
                .Where(stepSave => stepSave != null && database.GetStep(stepSave.stepId) != null)
                .ToList();
        }

        targetSave.biomes = targetSave.biomes
            .Where(biomeSave => biomeSave != null && database.GetBiome(biomeSave.biomeId) != null)
            .ToList();

        if (string.IsNullOrWhiteSpace(targetSave.currentBiomeId))
            targetSave.currentBiomeId = LocationLoader.Ins != null ? LocationLoader.Ins.currentLocation.ToString() : BlockSpawnLocation.Plain.ToString();

        JournalStepData activeStep = GetActiveStep(targetSave.currentBiomeId);
        string activeStepId = activeStep != null ? activeStep.id : string.Empty;
        if (!string.Equals(targetSave.currentJournalStepId, activeStepId, StringComparison.OrdinalIgnoreCase))
            targetSave.currentJournalStepId = activeStepId;
    }

    private void ReapplyRecipeUnlocks()
    {
        CraftNodeManager craftNodeManager = LocationLoader.Ins != null ? LocationLoader.Ins.CurrentCraftNodeManager : null;
        if (craftNodeManager == null)
            return;

        HashSet<string> allowedRecipes = new(StringComparer.OrdinalIgnoreCase);
        foreach (CraftNode node in craftNodeManager.allNodes)
        {
            if (node == null)
                continue;

            Item item = node.GetPrimaryRecipeItem();
            string recipeId = item != null ? item.itemName : node.nodeName;
            if (string.IsNullOrWhiteSpace(recipeId))
                continue;

            if (!unlocks.ControlledRecipes.Contains(recipeId) || unlocks.IsRecipeUnlocked(recipeId))
                allowedRecipes.Add(recipeId);
        }

        craftNodeManager.ApplyExternalRecipeUnlocks(allowedRecipes);
    }

    private void ReapplyBiomeUnlocks()
    {
        if (LocationLoader.Ins == null || unlocks == null)
            return;

        foreach (string biomeId in unlocks.UnlockedBiomes)
        {
            if (string.IsNullOrWhiteSpace(biomeId))
                continue;

            if (Enum.TryParse(biomeId, true, out BlockSpawnLocation biome))
                LocationLoader.Ins.TryUnlockLocation(biome);
        }
    }

    private void HandleInventoryStateChanged()
    {
        if (!IsReady)
            return;

        TryAutoAdvanceInventoryBackedStep();
        PublishAll();
    }

    private void TryAutoAdvanceInventoryBackedStep()
    {
        if (CurrentStep == null)
            return;

        int observedAmount = GetObservedStepAmount(CurrentStep);
        if (observedAmount <= 0)
            return;

        JournalStepProgressSave stepSave = GetOrCreateStepSave(CurrentStep);
        if (stepSave.completed)
            return;

        int required = Mathf.Max(1, CurrentStep.requiredAmount);
        int clampedObserved = Mathf.Clamp(observedAmount, 0, required);
        if (clampedObserved <= stepSave.currentAmount)
            return;

        stepSave.currentAmount = clampedObserved;
        if (clampedObserved >= required)
            CompleteStep(CurrentStep, stepSave);
    }

    private int GetObservedStepAmount(JournalStepData step)
    {
        if (step == null)
            return 0;

        if (step.goalType == JournalGoalType.CollectItem)
        {
            Item item = ResolveItemByName(step.targetId);
            return item != null ? GetInventoryAmount(boundMainInventory ?? ResolveMainInventory(), item) : 0;
        }

        if (step.goalType != JournalGoalType.CraftItem)
            return 0;

        Item craftedItem = ResolveItemByName(step.targetId);
        if (craftedItem != null)
        {
            int inventoryAmount = GetInventoryAmount(boundMainInventory ?? ResolveMainInventory(), craftedItem);
            if (inventoryAmount > 0)
                return inventoryAmount;
        }

        CraftNodeManager craftNodeManager = LocationLoader.Ins != null ? LocationLoader.Ins.CurrentCraftNodeManager : null;
        if (craftNodeManager?.allNodes == null)
            return 0;

        for (int i = 0; i < craftNodeManager.allNodes.Count; i++)
        {
            CraftNode node = craftNodeManager.allNodes[i];
            if (node == null || node.State != CraftNodeState.Finished)
                continue;

            Item nodeItem = node.GetPrimaryRecipeItem();
            string nodeRecipeId = nodeItem != null && !string.IsNullOrWhiteSpace(nodeItem.itemName)
                ? nodeItem.itemName
                : node.nodeName;
            if (MatchesTarget(step.targetId, nodeRecipeId))
                return Mathf.Max(1, step.requiredAmount);
        }

        return 0;
    }

    private void UpdateSavePointers()
    {
        if (save == null)
            return;

        save.currentBiomeId = CurrentBiomeId ?? string.Empty;
        save.currentJournalStepId = CurrentStep != null ? CurrentStep.id : string.Empty;
    }

    private bool MatchesTarget(string expected, string actual)
    {
        return comparer.Equals(expected ?? string.Empty, actual ?? string.Empty);
    }

    private void EmitCompletionToast(JournalStepData step)
    {
        string message = ResolveCompletionToast(step);
        if (string.IsNullOrWhiteSpace(message))
            return;

        TopNotificationManager.NotifyQuest(message);
    }

    private string ResolveCompletionToast(JournalStepData step)
    {
        if (step == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(step.completionToast))
            return step.completionToast;

        return $"Journal: {ResolveStepTitle(step)}";
    }

    public IReadOnlyList<JournalStepViewModel> GetCurrentMenuSteps()
    {
        return BuildMenuView();
    }

    public JournalHudViewModel GetCurrentHudView()
    {
        return BuildHudView();
    }

    public bool IsFeatureUnlocked(string featureId) => unlocks == null || unlocks.IsFeatureUnlocked(featureId);
    public bool IsBlockUnlocked(string blockId) => unlocks == null || unlocks.IsBlockUnlocked(blockId);
    public bool IsRecipeUnlocked(string recipeId) => unlocks == null || unlocks.IsRecipeUnlocked(recipeId);
    public bool IsBossUnlocked(string bossId) => unlocks == null || unlocks.IsBossUnlocked(bossId);
    public bool IsBiomeUnlocked(string biomeId) => unlocks == null || unlocks.IsBiomeUnlocked(biomeId);
}

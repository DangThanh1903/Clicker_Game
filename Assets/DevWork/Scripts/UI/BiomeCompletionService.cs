using System;
using System.Collections;
using System.Collections.Generic;
using Game.Discovery;
using UnityEngine;
using UniRx;

public enum BiomeCompletionCategory
{
    Quest,
    Boss,
    Recipe,
    BlockDiscovery,
    DropDiscovery
}

public struct BiomeCompletionPart
{
    public BiomeCompletionCategory Category;
    public int Completed;
    public int Total;
    public float Weight;

    public float Percent
    {
        get
        {
            if (Total <= 0)
                return 1f;

            return Mathf.Clamp01((float)Completed / Total);
        }
    }
}

public struct BiomeCompletionSnapshot
{
    public BlockSpawnLocation Biome;
    public float Percent;
    public List<BiomeCompletionPart> Parts;
}

public sealed class BiomeCompletionService : MonoBehaviour
{
    public static BiomeCompletionService Ins { get; private set; }

    private const float QuestWeight = 25f;
    private const float BossWeight = 20f;
    private const float RecipeWeight = 20f;
    private const float BlockDiscoveryWeight = 20f;
    private const float DropDiscoveryWeight = 15f;

    private readonly List<BiomeCompletionPart> partsBuffer = new();
    private readonly HashSet<string> dropKeyBuffer = new(StringComparer.OrdinalIgnoreCase);

    private IDisposable locationSubscription;
    private LocationLoader boundLocationLoader;
    private BlockDiscoveryService boundDiscoveryService;
    private QuestManager boundQuestManager;
    private CraftNodeManager boundCraftNodeManager;
    private Coroutine bindCo;

    public event Action<BiomeCompletionSnapshot> OnProgressChanged;

    public BiomeCompletionSnapshot CurrentSnapshot { get; private set; }

    public static BiomeCompletionService GetOrCreate()
    {
        if (Ins != null)
            return Ins;

        var go = new GameObject(nameof(BiomeCompletionService));
        return go.AddComponent<BiomeCompletionService>();
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
    }

    private void OnEnable()
    {
        TryBindDependencies();
        Recalculate();

        if (bindCo == null)
            bindCo = StartCoroutine(BindNextFrame());
    }

    private void OnDisable()
    {
        if (bindCo != null)
        {
            StopCoroutine(bindCo);
            bindCo = null;
        }

        RebindLocationLoader(null);
        RebindDiscoveryService(null);
        RebindQuestManager(null);
        BindCraftNodeManager(null);
    }

    public void Recalculate()
    {
        BlockSpawnLocation biome = ResolveCurrentBiome();
        partsBuffer.Clear();

        partsBuffer.Add(BuildQuestPart(biome));
        partsBuffer.Add(BuildBossPart(biome));
        partsBuffer.Add(BuildRecipePart());
        partsBuffer.Add(BuildBlockDiscoveryPart(biome));
        partsBuffer.Add(BuildDropDiscoveryPart(biome));

        float weightedSum = 0f;
        float totalWeight = 0f;

        for (int i = 0; i < partsBuffer.Count; i++)
        {
            BiomeCompletionPart part = partsBuffer[i];
            if (part.Total <= 0 || part.Weight <= 0f)
                continue;

            weightedSum += part.Percent * part.Weight;
            totalWeight += part.Weight;
        }

        CurrentSnapshot = new BiomeCompletionSnapshot
        {
            Biome = biome,
            Percent = totalWeight <= 0f ? 0f : Mathf.Clamp01(weightedSum / totalWeight),
            Parts = new List<BiomeCompletionPart>(partsBuffer)
        };

        OnProgressChanged?.Invoke(CurrentSnapshot);
    }

    private IEnumerator BindNextFrame()
    {
        yield return null;
        bindCo = null;
        TryBindDependencies();
        Recalculate();
    }

    private void TryBindDependencies()
    {
        RebindLocationLoader(LocationLoader.Ins);
        RebindDiscoveryService(BlockDiscoveryService.Ins);
        RebindQuestManager(QuestManager.Ins);
        BindCraftNodeManager(boundLocationLoader != null ? boundLocationLoader.CurrentCraftNodeManager : null);
    }

    private void RebindLocationLoader(LocationLoader loader)
    {
        if (boundLocationLoader == loader)
            return;

        if (boundLocationLoader != null)
        {
            boundLocationLoader.CurrentCraftNodeManagerChanged -= HandleCraftNodeManagerChanged;
            boundLocationLoader.LocationUnlocked -= HandleLocationUnlocked;
        }

        locationSubscription?.Dispose();
        locationSubscription = null;
        boundLocationLoader = loader;

        if (boundLocationLoader == null)
            return;

        boundLocationLoader.CurrentCraftNodeManagerChanged += HandleCraftNodeManagerChanged;
        boundLocationLoader.LocationUnlocked += HandleLocationUnlocked;

        if (boundLocationLoader.ReactiveLocation != null)
        {
            locationSubscription = boundLocationLoader.ReactiveLocation
                .DistinctUntilChanged()
                .Subscribe(_ => Recalculate());
        }
    }

    private void RebindDiscoveryService(BlockDiscoveryService service)
    {
        if (boundDiscoveryService == service)
            return;

        if (boundDiscoveryService != null)
        {
            boundDiscoveryService.OnBlockDiscovered -= HandleBlockDiscovered;
            boundDiscoveryService.OnDropDiscovered -= HandleDropDiscovered;
        }

        boundDiscoveryService = service;
        if (boundDiscoveryService == null)
            return;

        boundDiscoveryService.OnBlockDiscovered += HandleBlockDiscovered;
        boundDiscoveryService.OnDropDiscovered += HandleDropDiscovered;
    }

    private void RebindQuestManager(QuestManager manager)
    {
        if (boundQuestManager == manager)
            return;

        if (boundQuestManager != null)
        {
            boundQuestManager.QuestListChanged -= HandleQuestListChanged;
            boundQuestManager.QuestChanged -= HandleQuestChanged;
        }

        boundQuestManager = manager;
        if (boundQuestManager == null)
            return;

        boundQuestManager.QuestListChanged += HandleQuestListChanged;
        boundQuestManager.QuestChanged += HandleQuestChanged;
    }

    private void BindCraftNodeManager(CraftNodeManager manager)
    {
        if (boundCraftNodeManager == manager)
            return;

        if (boundCraftNodeManager != null)
            boundCraftNodeManager.OnNodeFinished -= HandleCraftNodeFinished;

        boundCraftNodeManager = manager;

        if (boundCraftNodeManager != null)
            boundCraftNodeManager.OnNodeFinished += HandleCraftNodeFinished;

        if (isActiveAndEnabled)
            Recalculate();
    }

    private void HandleCraftNodeManagerChanged(CraftNodeManager manager)
    {
        BindCraftNodeManager(manager);
    }

    private void HandleCraftNodeFinished(CraftNode _)
    {
        Recalculate();
    }

    private void HandleQuestListChanged(QuestType _)
    {
        Recalculate();
    }

    private void HandleQuestChanged(QuestRuntimeEntry _)
    {
        Recalculate();
    }

    private void HandleLocationUnlocked(BlockSpawnLocation _)
    {
        Recalculate();
    }

    private void HandleBlockDiscovered(string _)
    {
        Recalculate();
    }

    private void HandleDropDiscovered(string _, string __)
    {
        Recalculate();
    }

    private BiomeCompletionPart BuildQuestPart(BlockSpawnLocation biome)
    {
        int total = 0;
        int completed = 0;
        BlockUVDatabase blockDb = ResolveBlockDatabase();

        if (boundQuestManager != null)
        {
            foreach (var entry in boundQuestManager.GetProgressEntries())
            {
                QuestDef def = entry.Def;
                if (!IsQuestForBiome(def, biome, blockDb))
                    continue;

                total++;
                if (entry.IsCompleted)
                    completed++;
            }
        }

        return CreatePart(BiomeCompletionCategory.Quest, completed, total, QuestWeight);
    }

    private BiomeCompletionPart BuildBossPart(BlockSpawnLocation biome)
    {
        BossSO bossDb = ResolveBossDatabase();
        int total = 0;

        if (bossDb != null && bossDb.bosses != null)
        {
            for (int i = 0; i < bossDb.bosses.Count; i++)
            {
                BossEntry boss = bossDb.bosses[i];
                if (boss != null && boss.biome == biome)
                    total++;
            }
        }

        int completed = total > 0 && IsBossBiomeCleared(biome) ? total : 0;
        return CreatePart(BiomeCompletionCategory.Boss, completed, total, BossWeight);
    }

    private BiomeCompletionPart BuildRecipePart()
    {
        CraftNodeManager manager = boundCraftNodeManager;
        int total = 0;
        int completed = 0;

        if (manager != null && manager.allNodes != null)
        {
            for (int i = 0; i < manager.allNodes.Count; i++)
            {
                CraftNode node = manager.allNodes[i];
                if (node == null)
                    continue;

                total++;
                if (node.State == CraftNodeState.Finished)
                    completed++;
            }
        }

        return CreatePart(BiomeCompletionCategory.Recipe, completed, total, RecipeWeight);
    }

    private BiomeCompletionPart BuildBlockDiscoveryPart(BlockSpawnLocation biome)
    {
        BlockUVDatabase blockDb = ResolveBlockDatabase();
        BlockDiscoveryService discovery = boundDiscoveryService;
        int total = 0;
        int completed = 0;

        if (blockDb != null && blockDb.blocks != null)
        {
            for (int i = 0; i < blockDb.blocks.Count; i++)
            {
                BlockUVEntry block = blockDb.blocks[i];
                if (!IsBlockInBiome(block, biome))
                    continue;

                total++;
                if (discovery != null && discovery.IsBlockDiscovered(block.blockName))
                    completed++;
            }
        }

        return CreatePart(BiomeCompletionCategory.BlockDiscovery, completed, total, BlockDiscoveryWeight);
    }

    private BiomeCompletionPart BuildDropDiscoveryPart(BlockSpawnLocation biome)
    {
        BlockUVDatabase blockDb = ResolveBlockDatabase();
        BlockDiscoveryService discovery = boundDiscoveryService;
        int total = 0;
        int completed = 0;
        dropKeyBuffer.Clear();

        if (blockDb != null && blockDb.blocks != null)
        {
            for (int i = 0; i < blockDb.blocks.Count; i++)
            {
                BlockUVEntry block = blockDb.blocks[i];
                if (!IsBlockInBiome(block, biome) || block.drops == null)
                    continue;

                for (int j = 0; j < block.drops.Count; j++)
                {
                    ItemDrop drop = block.drops[j];
                    if (drop == null || drop.item == null)
                        continue;

                    string itemId = BlockDiscoveryService.GetItemId(drop.item);
                    if (string.IsNullOrWhiteSpace(itemId))
                        continue;

                    string key = BlockDiscoveryService.MakeDropKey(block.blockName, itemId);
                    if (!dropKeyBuffer.Add(key))
                        continue;

                    total++;
                    if (discovery != null && discovery.IsDropDiscovered(block.blockName, itemId))
                        completed++;
                }
            }
        }

        return CreatePart(BiomeCompletionCategory.DropDiscovery, completed, total, DropDiscoveryWeight);
    }

    private static BiomeCompletionPart CreatePart(BiomeCompletionCategory category, int completed, int total, float weight)
    {
        return new BiomeCompletionPart
        {
            Category = category,
            Completed = Mathf.Clamp(completed, 0, Mathf.Max(0, total)),
            Total = Mathf.Max(0, total),
            Weight = Mathf.Max(0f, weight)
        };
    }

    private static BlockSpawnLocation ResolveCurrentBiome()
    {
        if (LocationLoader.Ins != null)
            return LocationLoader.Ins.currentLocation;

        if (DataSaver.Ins != null && DataSaver.Ins.currentLocation.HasValue)
            return DataSaver.Ins.currentLocation.Value;

        return BlockSpawnLocation.Plain;
    }

    private static BlockUVDatabase ResolveBlockDatabase()
    {
        if (BlockManager.Ins != null && BlockManager.Ins.CurrentBlock != null)
            return BlockManager.Ins.CurrentBlock.blockUVDatabase;

        return null;
    }

    private static BossSO ResolveBossDatabase()
    {
        return BlockManager.Ins != null ? BlockManager.Ins.BossDatabase : null;
    }

    private static bool IsBlockInBiome(BlockUVEntry block, BlockSpawnLocation biome)
    {
        if (block == null || string.IsNullOrWhiteSpace(block.blockName))
            return false;

        return block.locationCondition == biome || block.locationCondition == BlockSpawnLocation.Any;
    }

    private static bool IsQuestForBiome(QuestDef def, BlockSpawnLocation biome, BlockUVDatabase blockDb)
    {
        if (def == null || def.IsAchievement || def.steps == null)
            return false;

        for (int i = 0; i < def.steps.Count; i++)
        {
            QuestStepDef step = def.steps[i];
            if (step == null)
                continue;

            if (DoesTargetPointToBiome(step.targetId, biome))
                return true;

            if (step.goalType == GoalType.BreakBlock && IsBreakBlockTargetInBiome(step.targetId, biome, blockDb))
                return true;
        }

        return false;
    }

    private static bool DoesTargetPointToBiome(string targetId, BlockSpawnLocation biome)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            return false;

        int atIndex = targetId.LastIndexOf('@');
        if (atIndex < 0 || atIndex >= targetId.Length - 1)
            return false;

        string biomePart = targetId.Substring(atIndex + 1).Trim();
        return Enum.TryParse(biomePart, true, out BlockSpawnLocation targetBiome) && targetBiome == biome;
    }

    private static bool IsBreakBlockTargetInBiome(string targetId, BlockSpawnLocation biome, BlockUVDatabase blockDb)
    {
        if (blockDb == null || string.IsNullOrWhiteSpace(targetId))
            return false;

        string blockName = StripBiomeSuffix(targetId);
        BlockUVEntry block = blockDb.GetByName(blockName);
        return IsBlockInBiome(block, biome);
    }

    private static string StripBiomeSuffix(string targetId)
    {
        int atIndex = targetId.LastIndexOf('@');
        if (atIndex < 0)
            return targetId.Trim();

        return targetId.Substring(0, atIndex).Trim();
    }

    private static bool IsBossBiomeCleared(BlockSpawnLocation biome)
    {
        if (LocationLoader.Ins == null)
            return false;

        int nextIndex = (int)biome + 1;
        if (!Enum.IsDefined(typeof(BlockSpawnLocation), nextIndex))
            return false;

        BlockSpawnLocation nextBiome = (BlockSpawnLocation)nextIndex;
        return nextBiome != BlockSpawnLocation.Any && LocationLoader.Ins.IsLocationUnlocked(nextBiome);
    }
}

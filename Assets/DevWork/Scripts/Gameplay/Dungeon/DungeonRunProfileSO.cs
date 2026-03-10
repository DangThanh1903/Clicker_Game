using System;
using System.Collections.Generic;
using UnityEngine;

public enum DungeonStageType
{
    MonsterWave = 0,
    MiniGame = 1
}

[Serializable]
public class DungeonRewardEntry
{
    public Item item;
    [Min(1)] public int minAmount = 1;
    [Min(1)] public int maxAmount = 1;
    [Range(0f, 1f)] public float dropChance = 1f;
}

[Serializable]
public class DungeonMonsterEntry
{
    public MonsterDef monster;
    [Min(1)] public int count = 1;
}

[Serializable]
public class DungeonStageData
{
    public string stageId = "stage";
    public DungeonStageType stageType = DungeonStageType.MonsterWave;
    [Min(0.1f)] public float duration = 20f;

    [Header("Monster Wave")]
    public bool requireKillAllMonsters = true;
    public List<DungeonMonsterEntry> monsters = new List<DungeonMonsterEntry>();

    [Header("Mini Game")]
    [Tooltip("External minigame system can read this id to load a minigame.")]
    public string miniGameId;
    [Tooltip("If true, stage is considered success when timeout is reached.")]
    public bool miniGameAutoSuccessOnTimeout = false;
}

[CreateAssetMenu(fileName = "DungeonRunProfile", menuName = "Dungeon/Run Profile")]
public class DungeonRunProfileSO : ScriptableObject
{
    [Header("Identity")]
    public string profileId = "dungeon_default";
    [Tooltip("Biome used to select this profile when entering dungeon run from that biome.")]
    public BlockSpawnLocation sourceBiome = BlockSpawnLocation.Any;

    [Header("Run")]
    [Min(1f)] public float runTimeLimit = 90f;
    public List<DungeonStageData> stages = new List<DungeonStageData>();

    [Header("Success Rewards")]
    public List<DungeonRewardEntry> successRewards = new List<DungeonRewardEntry>();
}

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "QuestSO", menuName = "Quests/QuestSO")]
public class QuestSO : ScriptableObject
{
    [Header("Progress Quests (one-time, long-term)")]
    public List<QuestDef> progressQuests = new();

    [Header("Daily Quests (rotates/reset each day)")]
    public List<QuestDef> dailyQuests = new();

    [Header("Daily Settings")]
    [Tooltip("Số daily quest pick mỗi ngày (<= dailyQuests.Count)")]
    public int dailyPickCount = 3;
}

public enum QuestType { Progress, Daily }

public enum GoalType
{
    CollectItem,
    CraftItem,
    BreakBlock,
    ReachStat,
    Custom // bạn tự report bằng mã targetId bất kỳ
}

[Serializable]
public class QuestDef
{
    public string id;                // unique (stable) e.g. "progress_mine_1000"
    public QuestType type;
    public string title;
    [TextArea] public string description;
    public Sprite icon;
    [Header("Achievement")]
    public bool isAchievement;

    public List<QuestStepDef> steps = new();
    public List<RewardDef> rewards = new();

    [Header("Optional unlock gating")]
    public string requiredQuestId;   // để chain nhiệm vụ (optional)
}

[Serializable]
public class QuestStepDef
{
    public string stepId;            // unique trong quest
    public GoalType goalType;
    public string targetId;          // "Dirt", "Stone", "Craft:SunpetalStaff", "Stat:DPS"
    public int requiredAmount = 1;
}

[Serializable]
public class RewardDef
{
    public int gemAmount;
    public List<InventoryItem> items = new();
}

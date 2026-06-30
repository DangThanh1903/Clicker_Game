using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class QuestStepState
{
    public string stepId;
    public int currentAmount;
    public bool completed;
}

[Serializable]
public class QuestState
{
    public string questId;
    public bool completed;
    public List<QuestStepState> steps = new();
    public bool rewardClaimed; // cho UI claim phần thưởng
}

public interface IQuestStorage
{
    // progress quests
    List<QuestState> LoadProgressStates();
    void SaveProgressStates(List<QuestState> states);

    List<QuestState> LoadAchievementStates();
    void SaveAchievementStates(List<QuestState> states);

    // daily quests of the day (selection + state)
    string LoadDailyKey(); // yyyyMMdd key đã áp dụng
    void SaveDailyKey(string key);

    List<string> LoadDailySelectedIds();
    void SaveDailySelectedIds(List<string> ids);

    List<QuestState> LoadDailyStates();
    void SaveDailyStates(List<QuestState> states);
}

public class JsonQuestStorage : IQuestStorage
{
    private const string FileName = "quest_save.json";
    private const string LegacyKeyProgress = "Q_PROGRESS_STATES";
    private const string LegacyKeyAchievement = "Q_ACHIEVEMENT_STATES";
    private const string LegacyKeyDailyKey = "Q_DAILY_KEY";
    private const string LegacyKeyDailyIds = "Q_DAILY_IDS";
    private const string LegacyKeyDailyStates = "Q_DAILY_STATES";

    private QuestStorageData data;
    private readonly SaveCoordinator saveCoordinator = SaveCoordinator.Ins;

    public List<QuestState> LoadProgressStates()
    {
        EnsureLoaded();
        return CopyStates(data.progressStates);
    }

    public void SaveProgressStates(List<QuestState> states)
    {
        EnsureLoaded();
        data.progressStates = CopyStates(states);
        Save();
    }

    public List<QuestState> LoadAchievementStates()
    {
        EnsureLoaded();
        return CopyStates(data.achievementStates);
    }

    public void SaveAchievementStates(List<QuestState> states)
    {
        EnsureLoaded();
        data.achievementStates = CopyStates(states);
        Save();
    }

    public string LoadDailyKey()
    {
        EnsureLoaded();
        return data.dailyKey ?? string.Empty;
    }

    public void SaveDailyKey(string key)
    {
        EnsureLoaded();
        data.dailyKey = key ?? string.Empty;
        Save();
    }

    public List<string> LoadDailySelectedIds()
    {
        EnsureLoaded();
        return data.dailySelectedIds != null ? new List<string>(data.dailySelectedIds) : new List<string>();
    }

    public void SaveDailySelectedIds(List<string> ids)
    {
        EnsureLoaded();
        data.dailySelectedIds = ids != null ? new List<string>(ids) : new List<string>();
        Save();
    }

    public List<QuestState> LoadDailyStates()
    {
        EnsureLoaded();
        return CopyStates(data.dailyStates);
    }

    public void SaveDailyStates(List<QuestState> states)
    {
        EnsureLoaded();
        data.dailyStates = CopyStates(states);
        Save();
    }

    private void EnsureLoaded()
    {
        if (data != null)
            return;

        data = LoadFromFile();
        if (data != null)
        {
            NormalizeData(data);
            return;
        }

        data = new QuestStorageData();
        if (TryImportLegacyPlayerPrefs(data))
        {
            NormalizeData(data);
            Save();
            DeleteLegacyPlayerPrefs();
        }
    }

    private QuestStorageData LoadFromFile()
    {
        saveCoordinator.TryLoadJson(FileName, out QuestStorageData storageData, "QuestStorage");
        return storageData;
    }

    private void Save()
    {
        NormalizeData(data);
        saveCoordinator.TrySaveJson(FileName, data ?? new QuestStorageData(), "QuestStorage");
    }

    private static bool TryImportLegacyPlayerPrefs(QuestStorageData target)
    {
        bool imported = false;

        if (PlayerPrefs.HasKey(LegacyKeyProgress))
        {
            target.progressStates = LegacyJsonLoad<List<QuestState>>(LegacyKeyProgress) ?? new List<QuestState>();
            imported = true;
        }

        if (PlayerPrefs.HasKey(LegacyKeyAchievement))
        {
            target.achievementStates = LegacyJsonLoad<List<QuestState>>(LegacyKeyAchievement) ?? new List<QuestState>();
            imported = true;
        }

        if (PlayerPrefs.HasKey(LegacyKeyDailyStates))
        {
            target.dailyStates = LegacyJsonLoad<List<QuestState>>(LegacyKeyDailyStates) ?? new List<QuestState>();
            imported = true;
        }

        if (PlayerPrefs.HasKey(LegacyKeyDailyKey))
        {
            target.dailyKey = PlayerPrefs.GetString(LegacyKeyDailyKey, string.Empty);
            imported = true;
        }

        if (PlayerPrefs.HasKey(LegacyKeyDailyIds))
        {
            target.dailySelectedIds = LegacyJsonLoad<List<string>>(LegacyKeyDailyIds) ?? new List<string>();
            imported = true;
        }

        return imported;
    }

    private static void DeleteLegacyPlayerPrefs()
    {
        PlayerPrefs.DeleteKey(LegacyKeyProgress);
        PlayerPrefs.DeleteKey(LegacyKeyAchievement);
        PlayerPrefs.DeleteKey(LegacyKeyDailyKey);
        PlayerPrefs.DeleteKey(LegacyKeyDailyIds);
        PlayerPrefs.DeleteKey(LegacyKeyDailyStates);
        PlayerPrefs.Save();
    }

    private static void NormalizeData(QuestStorageData storageData)
    {
        if (storageData == null)
            return;

        storageData.progressStates ??= new List<QuestState>();
        storageData.achievementStates ??= new List<QuestState>();
        storageData.dailyStates ??= new List<QuestState>();
        storageData.dailyKey ??= string.Empty;
        storageData.dailySelectedIds ??= new List<string>();
    }

    private static T LegacyJsonLoad<T>(string key)
    {
        if (!PlayerPrefs.HasKey(key)) return default;
        var json = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(json)) return default;

        try
        {
            return JsonUtility.FromJson<Wrapper<T>>(json).value;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[QuestStorage] Failed to migrate legacy PlayerPrefs key '{key}': {ex.Message}");
            return default;
        }
    }

    private static List<QuestState> CopyStates(List<QuestState> source)
    {
        var result = new List<QuestState>();
        if (source == null)
            return result;

        foreach (var state in source)
        {
            if (state == null)
                continue;

            var copy = new QuestState
            {
                questId = state.questId,
                completed = state.completed,
                rewardClaimed = state.rewardClaimed,
                steps = new List<QuestStepState>()
            };

            if (state.steps != null)
            {
                foreach (var step in state.steps)
                {
                    if (step == null)
                        continue;

                    copy.steps.Add(new QuestStepState
                    {
                        stepId = step.stepId,
                        currentAmount = step.currentAmount,
                        completed = step.completed
                    });
                }
            }

            result.Add(copy);
        }

        return result;
    }

    [Serializable]
    private class QuestStorageData
    {
        public List<QuestState> progressStates = new();
        public List<QuestState> achievementStates = new();
        public List<QuestState> dailyStates = new();
        public string dailyKey = string.Empty;
        public List<string> dailySelectedIds = new();
    }

    [Serializable]
    private class Wrapper<T>
    {
        public T value = default;
    }
}

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

    // daily quests of the day (selection + state)
    string LoadDailyKey(); // yyyyMMdd key đã áp dụng
    void SaveDailyKey(string key);

    List<string> LoadDailySelectedIds();
    void SaveDailySelectedIds(List<string> ids);

    List<QuestState> LoadDailyStates();
    void SaveDailyStates(List<QuestState> states);
}

public class PlayerPrefsQuestStorage : IQuestStorage
{
    const string KEY_PROGRESS = "Q_PROGRESS_STATES";
    const string KEY_DAILY_KEY = "Q_DAILY_KEY";
    const string KEY_DAILY_IDS = "Q_DAILY_IDS";
    const string KEY_DAILY_STATES = "Q_DAILY_STATES";

    public List<QuestState> LoadProgressStates()
        => JsonLoad<List<QuestState>>(KEY_PROGRESS) ?? new List<QuestState>();

    public void SaveProgressStates(List<QuestState> states)
        => JsonSave(KEY_PROGRESS, states);

    public string LoadDailyKey()
        => PlayerPrefs.GetString(KEY_DAILY_KEY, "");

    public void SaveDailyKey(string key)
    {
        PlayerPrefs.SetString(KEY_DAILY_KEY, key);
        PlayerPrefs.Save();
    }

    public List<string> LoadDailySelectedIds()
        => JsonLoad<List<string>>(KEY_DAILY_IDS) ?? new List<string>();

    public void SaveDailySelectedIds(List<string> ids)
        => JsonSave(KEY_DAILY_IDS, ids);

    public List<QuestState> LoadDailyStates()
        => JsonLoad<List<QuestState>>(KEY_DAILY_STATES) ?? new List<QuestState>();

    public void SaveDailyStates(List<QuestState> states)
        => JsonSave(KEY_DAILY_STATES, states);

    // Helpers
    void JsonSave<T>(string key, T obj)
    {
        var json = JsonUtility.ToJson(new Wrapper<T> { value = obj });
        PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();
    }
    T JsonLoad<T>(string key)
    {
        if (!PlayerPrefs.HasKey(key)) return default;
        var json = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(json)) return default;
        return JsonUtility.FromJson<Wrapper<T>>(json).value;
    }
    [Serializable] class Wrapper<T> { public T value; }
}

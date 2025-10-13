using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public enum BossType { Normal, Mini, Special }

[System.Serializable]
public class BossEntry
{
    [PreviewField(ObjectFieldAlignment.Left)]
    [HorizontalGroup("Row", 70)]
    [HideLabel]
    public GameObject bossPrefab;

    [VerticalGroup("Row/Right"), LabelWidth(60)]
    public BlockSpawnLocation biome;

    [VerticalGroup("Row/Right"), LabelWidth(60)]
    public string bossName;

    [VerticalGroup("Row/Right"), TextArea(1, 3)]
    public string description;

    [VerticalGroup("Row/Right"), LabelWidth(60)]
    public BossType type = BossType.Normal;

    // ---- Spawn Conditions ----
    [FoldoutGroup("Conditions"), EnumToggleButtons, LabelWidth(100)]
    public TimeState timeRequired = TimeState.Any;

    [FoldoutGroup("Conditions"), LabelWidth(100)]
    public NormalWeatherName normalWeatherRequired = NormalWeatherName.Any;

    [FoldoutGroup("Conditions"), LabelWidth(100)]
    public SpecialWeatherName specialWeatherRequired = SpecialWeatherName.Any;

    public bool Matches(TimeState time, WeatherData currentNormal, WeatherData currentSpecial)
    {
        if (timeRequired != TimeState.Any && timeRequired != time)
            return false;

        if (normalWeatherRequired != NormalWeatherName.Any)
        {
            var n = currentNormal as NormalWeatherData;
            if (n == null || n.weatherName != normalWeatherRequired)
                return false;
        }

        if (specialWeatherRequired != SpecialWeatherName.Any)
        {
            var s = currentSpecial as SpecialWeatherData;
            if (s == null || s.weatherName != specialWeatherRequired)
                return false;
        }

        return true;
    }
}

[CreateAssetMenu(fileName = "BossDatabase", menuName = "Game/Boss Database", order = 0)]
public class BossSO : ScriptableObject
{
    [TableList]
    public List<BossEntry> bosses = new List<BossEntry>();

    /// <summary>
    /// Find exactly one boss for location + type. Returns null if not found.
    /// </summary>
    public BossEntry FindOne(BlockSpawnLocation location, BossType type)
    {
        return bosses.FirstOrDefault(b => b.biome == location && b.type == type);
    }
}

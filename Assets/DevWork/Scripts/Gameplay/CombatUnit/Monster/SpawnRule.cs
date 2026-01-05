using UnityEngine;

[CreateAssetMenu(menuName = "Monsters/SpawnRule")]
public class MonsterSpawnRule : ScriptableObject
{
    public int priority = 0;

    public bool anyLocation = true;
    public BlockSpawnLocation location;

    public TimeState timeState = TimeState.Any;

    public NormalWeatherName normalWeather = NormalWeatherName.Any;
    public SpecialWeatherName specialWeather = SpecialWeatherName.Any;

    public bool Matches(SpawnContext ctx)
    {
        if (!anyLocation && ctx.location != location) return false;
        if (timeState != TimeState.Any && ctx.timeState != timeState) return false;
        if (normalWeather != NormalWeatherName.Any && ctx.normalWeather != normalWeather) return false;
        if (specialWeather != SpecialWeatherName.Any && ctx.specialWeather != specialWeather) return false;
        return true;
    }
}

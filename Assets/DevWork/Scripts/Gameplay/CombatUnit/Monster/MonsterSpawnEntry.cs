using System;
using UnityEngine;

[Serializable]
public class MonsterSpawnEntry
{
    [Tooltip("Monster to spawn. Leave NULL to mean 'spawn nothing'.")]
    public MonsterDef monster;

    [Min(0)] public float weight = 1f;
}


public struct SpawnContext
{
    public BlockSpawnLocation location;
    public TimeState timeState;
    public NormalWeatherName normalWeather;
    public SpecialWeatherName specialWeather;
}

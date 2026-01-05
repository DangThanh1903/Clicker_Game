using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Monsters/MonsterSpawnTable")]
public class SpawnTable : ScriptableObject
{
    public List<MonsterSpawnPool> pools = new();
}


[System.Serializable]
public class MonsterSpawnPool
{
    public MonsterSpawnRule rule;
    public List<MonsterSpawnEntry> entries = new();
}
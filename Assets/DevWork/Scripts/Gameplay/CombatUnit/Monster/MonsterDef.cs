using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Monsters/MonsterDef")]
public class MonsterDef : ScriptableObject
{
    public string id;
    public int MaxHP = 100;

    [Header("Spawn Visual")]
    public GameObject prefab;
    public float lifetime = 3f;

    [Header("Reward")]
    public BuffSO buffReward;

    [Header("Drop")]
    public List<ItemDrop> drops = new();

    [Header("Optional")]
    public AudioClip appearSfx;
    public AudioClip successSfx;

    public List<(Item item, int amount)> GetDroppedItems(float luck)
    {
        return DropRollService.RollResolvedItemDrops(drops, luck);
    }
}

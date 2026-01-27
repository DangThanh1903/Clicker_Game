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
        var droppedItems = new List<(Item item, int amount)>();
        if (drops == null || drops.Count == 0) return droppedItems;

        foreach (var drop in drops)
        {
            if (drop == null) continue;
            if (drop.item == null || drop.item.Type == ItemType.None) continue;

            float chance = drop.dropChance;
            if (luck > 0f && chance < 1f)
                chance = LuckMath.BoostChance(chance, luck);

            if (Random.value <= chance)
            {
                int amount = Random.Range(drop.minAmount, drop.maxAmount + 1);
                if (amount > 0)
                    droppedItems.Add((drop.item, amount));
            }
        }

        return droppedItems;
    }
}

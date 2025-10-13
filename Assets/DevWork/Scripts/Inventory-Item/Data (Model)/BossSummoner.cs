using UnityEngine;
using UnityEngine.AddressableAssets;


[CreateAssetMenu(fileName = "BossSummoner", menuName = "Inventory/Items/BossSummoner")]
public class BossSummoner : Item
{
    public BlockSpawnLocation bossLocation;
    public BossType bossType;
    public override ItemType Type => ItemType.BossSummoner;
    public override int MaxStack => 1;
}

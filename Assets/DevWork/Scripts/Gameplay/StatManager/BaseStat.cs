using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "BaseStat", menuName = "Inventory/BaseStat")]
public class BaseStat : ScriptableObject
{
    public List<ReactiveStat> statsList;
    public List<ReactiveStat> baseStats;
    public List<ConditionalBuffSO> starterBuff;
}

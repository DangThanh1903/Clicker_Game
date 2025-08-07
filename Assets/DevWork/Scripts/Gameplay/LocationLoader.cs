using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LocationLoader : MonoBehaviour
{
    public BlockSpawnLocation currentLocation;

    public void SetLocation(BlockSpawnLocation blockSpawnLocation)
    {
        currentLocation = blockSpawnLocation;
        DataSaver.Ins.currentLocation = blockSpawnLocation;
    }
}

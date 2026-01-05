using System;
using System.Collections.Generic;

namespace Game.Discovery
{
    [Serializable]
    public class BlockDiscoverySaveData
    {
        public List<string> discoveredBlocks = new();
        public List<string> discoveredDrops  = new(); // "blockName:itemId"
    }
}

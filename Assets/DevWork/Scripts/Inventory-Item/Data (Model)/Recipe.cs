using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Recipe
{
    public List<InventoryItem> ingredients; // Always 4 for 2x2 grid
    public InventoryItem result;
}

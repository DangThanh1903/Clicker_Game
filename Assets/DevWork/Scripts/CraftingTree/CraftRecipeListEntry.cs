using System.Collections.Generic;

public sealed class CraftRecipeListEntry
{
    public CraftNode node;
    public Recipe recipe;
    public Item resultItem;
    public int resultQuantity;
    public string statLine;
    public List<RecipeIngredientStatus> ingredients;
    public bool canCraft;
    public bool canAddResult;

    public bool IsLocked => node != null && node.State == CraftNodeState.Locked;
    public bool IsFinished => node != null && node.State == CraftNodeState.Finished;
}

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "WeaponMergeDatabase",
    menuName = "Inventory/Merge/Weapon Merge Database")]
public class WeaponMergeDatabaseSO : ScriptableObject
{
    [SerializeField] private List<WeaponMergeRecipeEntry> recipes = new List<WeaponMergeRecipeEntry>();

    public bool TryGetRecipe(Pickaxe fromWeapon, out WeaponMergeRecipeEntry recipe)
    {
        recipe = default;
        if (fromWeapon == null || recipes == null)
            return false;

        for (int i = 0; i < recipes.Count; i++)
        {
            var candidate = recipes[i];
            if (candidate.fromWeapon != fromWeapon)
                continue;

            recipe = candidate;
            return recipe.toWeapon != null;
        }

        return false;
    }

    public bool TryGetMergeResult(Pickaxe fromWeapon, out Pickaxe toWeapon)
    {
        toWeapon = null;
        if (!TryGetRecipe(fromWeapon, out WeaponMergeRecipeEntry recipe))
            return false;

        toWeapon = recipe.toWeapon;
        return toWeapon != null;
    }
}

[Serializable]
public struct WeaponMergeRecipeEntry
{
    public Pickaxe fromWeapon;
    public Pickaxe toWeapon;
    public Item rareItem;
    [Min(0)] public int rareAmount;
}

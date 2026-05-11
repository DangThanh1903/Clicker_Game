using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PetItem", menuName = "Inventory/Items/Pet")]
public class PetItem : Item, IStatProvider
{
    [SerializeField] private List<StatModifier> modifiers;
    [SerializeField] private List<BuffSO> passiveBuffs;
    [SerializeField] private GameObject petVisualPrefab;
    [SerializeField] private Vector3 petSpawnLocalEuler = Vector3.zero;

    public override ItemType Type => ItemType.Pet;
    public GameObject PetVisualPrefab => petVisualPrefab;
    public Vector3 PetSpawnLocalEuler => petSpawnLocalEuler;

    public IEnumerable<StatModifier> GetStatModifiers()
    {
        foreach (var modifier in modifiers)
            yield return modifier;
    }

    public IEnumerable<BuffSO> GetPassiveBuffs()
    {
        return passiveBuffs;
    }

    protected override string GetBodyText()
    {
        string modsText = StatModifier.GetFormattedModifiers(GetStatModifiers());
        var sb = new System.Text.StringBuilder(description);
        if (modsText.Length > 0)
            sb.Append($"\n\n<b>Stats:</b>\n{modsText}");
        return sb.ToString();
    }
}

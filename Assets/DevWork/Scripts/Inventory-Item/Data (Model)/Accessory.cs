using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Accessory", menuName = "Inventory/Items/Accessory")]
public class Accessory : Item, IStatProvider
{
    [SerializeField] private List<StatModifier> modifiers;
    [SerializeField] private List<BuffSO> passiveBuffs;
    public override ItemType Type => ItemType.Accessory;

    public IEnumerable<StatModifier> GetStatModifiers()
    {
        foreach (var m in modifiers)
            yield return m;
    }

    public IEnumerable<BuffSO> GetPassiveBuffs() => passiveBuffs;

    protected override string GetBodyText()
    {
        string modsText = StatModifier.GetFormattedModifiers(GetStatModifiers());

        string buffText = "";
        foreach (var buff in passiveBuffs)
            buffText += $"• {buff.buffName}\n";

        var sb = new System.Text.StringBuilder(description);

        if (modsText.Length > 0)
            sb.Append($"\n\n<b>Stats:</b>\n{modsText}");

        if (buffText.Length > 0)
            sb.Append($"\n<b>Buffs:</b>\n{buffText}");

        return sb.ToString();
    }
}

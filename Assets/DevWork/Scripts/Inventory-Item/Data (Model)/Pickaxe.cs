using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public enum PickaxeType
{
    Normal,
    Hold,
    Idle
}

[CreateAssetMenu(fileName = "Pickaxe", menuName = "Inventory/Items/Pickaxe")]
public class Pickaxe : Item, IStatProvider
{
    [SerializeField] private List<StatModifier> modifiers;
    [SerializeField] private List<BuffSO> passiveBuffs;
    [FoldoutGroup("State Visuals/Hold VFX"), ShowIf(nameof(IsHoldState))]
    [SerializeField] private GameObject holdBeamVfxPrefab;
    [FoldoutGroup("State Visuals/Hold VFX"), ShowIf(nameof(IsHoldState))]
    [SerializeField] private Vector3 holdBeamStartOffset = new Vector3(0f, 0f, 0.1f);
    [FoldoutGroup("State Visuals/Idle Pet"), ShowIf(nameof(IsIdleState))]
    [SerializeField] private GameObject idlePetVisualPrefab;
    [FoldoutGroup("State Visuals/Idle Pet"), ShowIf(nameof(IsIdleState))]
    [SerializeField] private Vector3 idlePetSpawnLocalEuler = Vector3.zero;

    public override ItemType Type => ItemType.Pickaxe;
    public PickaxeType currentState = PickaxeType.Normal;
    private bool IsHoldState => currentState == PickaxeType.Hold;
    private bool IsIdleState => currentState == PickaxeType.Idle;
    public GameObject HoldBeamVfxPrefab => holdBeamVfxPrefab;
    public Vector3 HoldBeamStartOffset => holdBeamStartOffset;
    public GameObject IdlePetVisualPrefab => idlePetVisualPrefab;
    public Vector3 IdlePetSpawnLocalEuler => idlePetSpawnLocalEuler;

    public IEnumerable<StatModifier> GetStatModifiers()
    {
        // ONLY base stats
        foreach (var m in modifiers)
            yield return m;
    }

    public IEnumerable<BuffSO> GetPassiveBuffs() => passiveBuffs;

    protected override string GetBodyText()
    {
        // This shows BASE stats only (prefix is instance data)
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

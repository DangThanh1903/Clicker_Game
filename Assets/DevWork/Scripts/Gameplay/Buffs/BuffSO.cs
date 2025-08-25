using UnityEngine;

[CreateAssetMenu(menuName = "Buff System/BuffSO")]
public abstract class BuffSO : ScriptableObject
{
    public string buffName;
    public StatType statType;
    public float amount;
    public Sprite buffIcon;
    public float duration;
    public bool isStackable = false;

    public abstract bool IsPermanent { get; }
}
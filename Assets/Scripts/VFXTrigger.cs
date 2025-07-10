using UnityEngine;


public enum VFXTriggerType
{
    Milestone,
    InGame
    // You can add: Timed, Combo, Manual, etc.
}

[System.Serializable]
public class VFXTrigger
{
    public string name;
    public VFXTriggerType triggerType;
    public StatType watchStat;
    public float triggerThreshold;
    public bool triggerOnce = true;
    public GameObject vfxPrefab;

    // Runtime-only data
    [HideInInspector] public bool triggered;
    [HideInInspector] public GameObject spawnedVFX; // for looping/stop control
}

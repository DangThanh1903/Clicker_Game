using UnityEngine;
using System.Collections.Generic;
using UniRx;
using Lean.Pool;

public class VFXManager : MonoBehaviour
{
    [SerializeField] private List<VFXTrigger> triggers;

    void Start()
    {
        foreach (var trigger in triggers)
        {
            var reactiveStat = StatsManager.Ins.GetReactive(trigger.watchStat);

            switch (trigger.triggerType)
            {
                case VFXTriggerType.Achivement:
                    reactiveStat
                        .Where(val => val >= trigger.triggerThreshold)
                        .Where(_ => !trigger.triggered)
                        .Subscribe(_ => PlayAchivementVFX(trigger))
                        .AddTo(this);
                    break;

                case VFXTriggerType.InGame:
                    reactiveStat
                        .Subscribe(val => HandleInGameVFX(trigger, val))
                        .AddTo(this);
                    break;
            }
        }
    }

    private void PlayAchivementVFX(VFXTrigger trigger)
    {
        Debug.Log($"Achivement VFX played: {trigger.name}");
        if (trigger.vfxPrefab)
        {
            LeanPool.Spawn(trigger.vfxPrefab, transform.position, Quaternion.identity);
        }

        trigger.triggered = true;
    }

    private void HandleInGameVFX(VFXTrigger trigger, float value)
    {
        bool shouldPlay = value >= trigger.triggerThreshold;

        if (shouldPlay && trigger.spawnedVFX == null)
        {
            trigger.spawnedVFX = LeanPool.Spawn(trigger.vfxPrefab, transform.position, Quaternion.identity, transform);
            Debug.Log($"Started in-game VFX: {trigger.name}");
        }
        else if (!shouldPlay && trigger.spawnedVFX != null)
        {
            LeanPool.Despawn(trigger.spawnedVFX);
            trigger.spawnedVFX = null;
            Debug.Log($"Stopped in-game VFX: {trigger.name}");
        }
    }

}

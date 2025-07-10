using UnityEngine;
using System.Collections.Generic;
using UniRx;

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
                case VFXTriggerType.Milestone:
                    reactiveStat
                        .Where(val => val >= trigger.triggerThreshold)
                        .Where(_ => !trigger.triggered)
                        .Subscribe(_ => PlayMilestoneVFX(trigger))
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

    private void PlayMilestoneVFX(VFXTrigger trigger)
    {
        if (trigger.vfxPrefab)
        {
            Instantiate(trigger.vfxPrefab, transform.position, Quaternion.identity);
            Debug.Log($"Milestone VFX played: {trigger.name}");
        }

        trigger.triggered = true;
    }

    private void HandleInGameVFX(VFXTrigger trigger, float value)
    {
        bool shouldPlay = value >= trigger.triggerThreshold;

        if (shouldPlay && trigger.spawnedVFX == null)
        {
            trigger.spawnedVFX = Instantiate(trigger.vfxPrefab, transform.position, Quaternion.identity, transform);
            Debug.Log($"Started in-game VFX: {trigger.name}");
        }
        else if (!shouldPlay && trigger.spawnedVFX != null)
        {
            Destroy(trigger.spawnedVFX);
            trigger.spawnedVFX = null;
            Debug.Log($"Stopped in-game VFX: {trigger.name}");
        }
    }
}

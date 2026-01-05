using System.Collections.Generic;
using UnityEngine;
using UniRx;
using Lean.Pool;

public class BuffUIController : MonoBehaviour
{
    [SerializeField] private BuffManager buffManager;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private BuffUIElement prefab;

    // Track which UI element belongs to which buff instance
    private readonly Dictionary<BuffInstance, BuffUIElement> _uiByBuff =
        new Dictionary<BuffInstance, BuffUIElement>();

    void Start()
    {
        // Refresh UI every 0.2s (good enough for buff icons)
        Observable.Interval(System.TimeSpan.FromSeconds(0.2f))
            .Subscribe(_ => RefreshUI())
            .AddTo(this);
    }

    void RefreshUI()
    {
        if (buffManager == null) return;

        var displayBuffs = new HashSet<BuffInstance>(buffManager.GetDisplayBuffs());

        // 1) Despawn UI for buffs that no longer exist or are inactive
        var toRemove = new List<BuffInstance>();
        foreach (var kv in _uiByBuff)
        {
            var buff = kv.Key;
            var ui   = kv.Value;

            if (!displayBuffs.Contains(buff) || !buff.IsActive)
            {
                LeanPool.Despawn(ui.gameObject);
                toRemove.Add(buff);
            }
        }
        foreach (var buff in toRemove)
        {
            _uiByBuff.Remove(buff);
        }

        // 2) Ensure UI exists for each active buff
        foreach (var buff in displayBuffs)
        {
            if (!_uiByBuff.ContainsKey(buff))
            {
                var ui = LeanPool.Spawn(prefab, contentRoot);
                ui.Bind(buff);
                _uiByBuff[buff] = ui;
            }
        }
    }
}

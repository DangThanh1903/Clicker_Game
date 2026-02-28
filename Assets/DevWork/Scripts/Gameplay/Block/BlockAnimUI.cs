using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BlockAnimUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private BlockAnimationController blockAnimationController;

    [Header("Buttons")]
    public List<Button> spawnButtons = new();
    public List<Button> idleButtons  = new();
    public List<Button> clickButtons = new();
    public List<Button> deathButtons = new();

    [Header("Optional UI")]
    [SerializeField] private Button closeButton; // wire in inspector if this is a popup

    void Awake()
    {
        if (!blockAnimationController)
            Debug.LogWarning("[BlockAnimUI] BlockAnimationController is not assigned.", this);
    }

    void Start()
    {
        if (!blockAnimationController) return;

        BindChannel(spawnButtons, AnimChannel.Spawn,
            setter: i => blockAnimationController.SetSpawnIndex(i));

        BindChannel(idleButtons, AnimChannel.Idle,
            setter: i => blockAnimationController.SetIdleIndex(i));

        BindChannel(clickButtons, AnimChannel.Click,
            setter: i => blockAnimationController.SetClickIndex(i));

        BindChannel(deathButtons, AnimChannel.Death,
            setter: i => blockAnimationController.SetDeathIndex(i));

        if (closeButton)
            closeButton.onClick.AddListener(() =>
            {
                // Graceful close if using PopupController + PopupView
                PopupController.Instance?.CloseTop();
            });
    }

    void BindChannel(List<Button> buttons, AnimChannel channel, Action<int> setter)
    {
        if (buttons == null) return;

        for (int i = 0; i < buttons.Count; i++)
        {
            var idx = i; // capture
            var btn = buttons[i];
            if (!btn) continue;

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                setter?.Invoke(idx);
            });
        }
    }
}

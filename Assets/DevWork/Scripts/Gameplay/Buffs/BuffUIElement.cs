using UniRx;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuffUIElement : MonoBehaviour
{
    [Header("UI")]
    public Image icon;            // ← Icon itself used as fill image
    public TMP_Text stackText;    // ← x3, x4, etc.

    private BuffInstance buff;

    public void Bind(BuffInstance instance)
    {
        buff = instance;

        if (buff.buffData.buffIcon)
            icon.sprite = buff.buffData.buffIcon;

        // Ensure icon is a filled image
        icon.type = Image.Type.Filled;
        icon.fillMethod = Image.FillMethod.Radial360; // or horizontal if you want

        UpdateUI();

        // Update frequently
        Observable.Interval(System.TimeSpan.FromSeconds(0.1f))
            .TakeUntilDestroy(this)
            .Subscribe(_ => UpdateUI());
    }

    void UpdateUI()
    {
        if (buff == null) return;

        // -----------------------
        // STACK TEXT
        // -----------------------
        if (buff.StackCount > 1)
        {
            stackText.gameObject.SetActive(true);
            stackText.text = $"x{buff.StackCount}";
        }
        else
        {
            stackText.gameObject.SetActive(false);
        }

        // -----------------------
        // ICON FILL (cooldown-like)
        // -----------------------
        if (buff.HasDuration && buff.Duration > 0f)
        {
            float remaining = Mathf.Max(0, buff.RemainingTime);
            float fill = Mathf.Clamp01(remaining / buff.Duration);

            icon.fillAmount = fill;
        }
        else
        {
            // Full icon for permanent buffs
            icon.fillAmount = 1f;
        }
    }
}

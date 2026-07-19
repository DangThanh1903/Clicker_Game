using System.Text;
using TMPro;
using UnityEngine;

public sealed class GameplayJournalHudBinder
{
    private readonly TMP_Text combinedText;
    private JournalManager boundJournalManager;
    private readonly StringBuilder builder = new StringBuilder(128);

    public GameplayJournalHudBinder(TMP_Text combinedText)
    {
        this.combinedText = combinedText;
    }

    public void Bind()
    {
        JournalManager.GetOrCreate();
        TryBind();
        RefreshImmediate();
    }

    public void Tick()
    {
        if (boundJournalManager == null && JournalManager.Ins != null)
            TryBind();
    }

    public void Dispose()
    {
        if (boundJournalManager != null)
            boundJournalManager.HudChanged -= HandleHudChanged;

        boundJournalManager = null;
    }

    public void RefreshImmediate()
    {
        if (boundJournalManager == null || combinedText == null || !boundJournalManager.IsReady)
            return;

        Apply(boundJournalManager.GetCurrentHudView());
    }

    private void TryBind()
    {
        if (boundJournalManager == JournalManager.Ins && boundJournalManager != null)
            return;

        Dispose();
        boundJournalManager = JournalManager.Ins;
        if (boundJournalManager == null)
            return;

        boundJournalManager.HudChanged += HandleHudChanged;
    }

    private void HandleHudChanged(JournalHudViewModel view)
    {
        Apply(view);
    }

    private void Apply(JournalHudViewModel view)
    {
        if (combinedText == null)
            return;

        builder.Clear();
        if (!string.IsNullOrWhiteSpace(view.BiomeTitle))
            builder.Append(view.BiomeTitle).Append(' ').Append(view.BiomePercent).Append('%');

        builder.AppendLine();
        builder.Append("Journal");

        if (!string.IsNullOrWhiteSpace(view.StepTitle))
        {
            builder.AppendLine();
            builder.Append(view.StepTitle);
        }

        if (view.Lines != null)
        {
            for (int i = 0; i < view.Lines.Count; i++)
            {
                JournalIngredientProgressView line = view.Lines[i];
                builder.AppendLine();
                builder.Append(line.Label)
                    .Append(' ')
                    .Append(line.Current)
                    .Append('/')
                    .Append(line.Required);
            }
        }

        combinedText.SetText(builder.ToString());
    }
}

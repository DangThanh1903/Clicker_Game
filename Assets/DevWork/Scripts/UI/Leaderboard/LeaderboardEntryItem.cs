using TMPro;
using UnityEngine;

public class LeaderboardEntryItem : MonoBehaviour
{
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text valueText;

    public void Bind(int rank, string displayName, float value, LeaderboardMetric metric)
    {
        if (rankText != null)
            rankText.text = rank.ToString();

        if (nameText != null)
            nameText.text = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName;

        if (valueText != null)
        {
            valueText.text = metric == LeaderboardMetric.Clicks
                ? FormatNumber(value)
                : FormatTime(value);
        }
    }

    private static string FormatNumber(float value)
    {
        int v = Mathf.Max(0, Mathf.FloorToInt(value));
        return v.ToString("N0");
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int hours = total / 3600;
        int minutes = (total % 3600) / 60;
        int secs = total % 60;
        return $"{hours:00}:{minutes:00}:{secs:00}";
    }
}

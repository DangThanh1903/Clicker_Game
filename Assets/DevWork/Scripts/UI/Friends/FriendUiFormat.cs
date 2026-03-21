using System;
using Firebase.Firestore;
using UnityEngine;

public static class FriendUiFormat
{
    public static string FormatNumber(float value)
    {
        int number = Mathf.Max(0, Mathf.FloorToInt(value));
        return number.ToString("N0");
    }

    public static string FormatDuration(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int hours = total / 3600;
        int minutes = (total % 3600) / 60;
        int secs = total % 60;
        return $"{hours:00}:{minutes:00}:{secs:00}";
    }

    public static string FormatDate(Timestamp timestamp)
    {
        try
        {
            DateTime utc = timestamp.ToDateTime();
            return utc.ToLocalTime().ToString("yyyy-MM-dd");
        }
        catch
        {
            return "-";
        }
    }

    public static string ShortUid(string uid)
    {
        if (string.IsNullOrEmpty(uid))
            return "Player";
        if (uid.Length <= 8)
            return uid;
        return $"{uid.Substring(0, 4)}...{uid.Substring(uid.Length - 4)}";
    }
}

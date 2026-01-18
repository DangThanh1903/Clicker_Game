using System;
using UnityEngine;
#if FIREBASE_ANALYTICS
using Firebase.Analytics;
#endif

public static class AnalyticsService
{
    public struct AnalyticsParam
    {
        public string Key;
        public object Value;

        public AnalyticsParam(string key, object value)
        {
            Key = key;
            Value = value;
        }
    }

    public static void LogEvent(string name, params AnalyticsParam[] parameters)
    {
        if (string.IsNullOrEmpty(name)) return;

#if FIREBASE_ANALYTICS
        if (parameters == null || parameters.Length == 0)
        {
            FirebaseAnalytics.LogEvent(name);
            return;
        }

        var list = new Parameter[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            if (p.Value is string s)
                list[i] = new Parameter(p.Key, s);
            else if (p.Value is int i32)
                list[i] = new Parameter(p.Key, i32);
            else if (p.Value is long i64)
                list[i] = new Parameter(p.Key, i64);
            else if (p.Value is float f)
                list[i] = new Parameter(p.Key, (double)f);
            else if (p.Value is double d)
                list[i] = new Parameter(p.Key, d);
            else if (p.Value is bool b)
                list[i] = new Parameter(p.Key, b ? 1 : 0);
            else
                list[i] = new Parameter(p.Key, p.Value?.ToString() ?? "null");
        }

        FirebaseAnalytics.LogEvent(name, list);
#else
        _ = parameters;
#endif
    }

    public static void SetUserProperty(string name, string value)
    {
        if (string.IsNullOrEmpty(name)) return;
#if FIREBASE_ANALYTICS
        FirebaseAnalytics.SetUserProperty(name, value ?? string.Empty);
#else
        _ = value;
#endif
    }

    public static void SetUserProperty(string name, int value) =>
        SetUserProperty(name, value.ToString());

    public static void SetUserProperty(string name, float value) =>
        SetUserProperty(name, value.ToString("F2"));

    public static void SetUserProperty(string name, bool value) =>
        SetUserProperty(name, value ? "true" : "false");
}

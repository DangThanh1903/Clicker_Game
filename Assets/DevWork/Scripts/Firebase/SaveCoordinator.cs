using System;
using System.IO;
using UnityEngine;

public sealed class SaveCoordinator
{
    // Allowed global owner: local JSON persistence gateway.
    public static SaveCoordinator Ins { get; } = new SaveCoordinator();

    private SaveCoordinator() { }

    public bool Exists(string fileName)
    {
        return File.Exists(GetPath(fileName));
    }

    public string GetPath(string fileName)
    {
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    public bool TryLoadJson<T>(string fileName, out T data, string ownerTag = "Save") where T : class
    {
        data = null;
        string path = GetPath(fileName);
        if (!File.Exists(path))
            return false;

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrEmpty(json))
                return false;

            data = JsonUtility.FromJson<T>(json);
            return data != null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[{ownerTag}] Failed to read '{fileName}': {ex.Message}");
            return false;
        }
    }

    public bool TrySaveJson<T>(string fileName, T data, string ownerTag = "Save", bool prettyPrint = false)
    {
        try
        {
            string path = GetPath(fileName);
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, JsonUtility.ToJson(data, prettyPrint));
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[{ownerTag}] Failed to write '{fileName}': {ex.Message}");
            return false;
        }
    }

    public bool Delete(string fileName, string ownerTag = "Save")
    {
        string path = GetPath(fileName);
        if (!File.Exists(path))
            return true;

        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[{ownerTag}] Failed to delete '{fileName}': {ex.Message}");
            return false;
        }
    }
}

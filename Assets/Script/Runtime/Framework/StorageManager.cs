using System;
using UnityEngine;

public class StorageManager : Singleton<StorageManager>
{
    public void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        return PlayerPrefs.GetInt(key, defaultValue);
    }

    public void SetFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
    }

    public float GetFloat(string key, float defaultValue = 0f)
    {
        return PlayerPrefs.GetFloat(key, defaultValue);
    }

    public void SetString(string key, string value)
    {
        PlayerPrefs.SetString(key, value);
    }

    public string GetString(string key, string defaultValue = "")
    {
        return PlayerPrefs.GetString(key, defaultValue);
    }

    public void SetBool(string key, bool value)
    {
        PlayerPrefs.SetInt(key, value ? 1 : 0);
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        int defaultInt = defaultValue ? 1 : 0;
        return PlayerPrefs.GetInt(key, defaultInt) == 1;
    }

    public void SetObject<T>(string key, T data)
    {
        if (data == null)
        {
            PlayerPrefs.DeleteKey(key);
            return;
        }
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(key, json);
    }

    public T GetObject<T>(string key, T defaultValue = default)
    {
        if (!PlayerPrefs.HasKey(key))
            return defaultValue;
        string json = PlayerPrefs.GetString(key);
        if (string.IsNullOrEmpty(json))
            return defaultValue;

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[StorageManager] 反序列化键 {key} 失败: {e.Message}");
            return defaultValue;
        }
    }

    public bool HasKey(string key)
    {
        return PlayerPrefs.HasKey(key);
    }

    public void DeleteKey(string key)
    {
        PlayerPrefs.DeleteKey(key);
    }

    public void DeleteAll()
    {
        PlayerPrefs.DeleteAll();
    }

    public void Save()
    {
        PlayerPrefs.Save();
    }
}

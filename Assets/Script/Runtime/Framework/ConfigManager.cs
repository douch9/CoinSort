using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public interface IConfigItem
{
    int Id { get; }
}

public class ConfigManager : Singleton<ConfigManager>
{
    private readonly Dictionary<Type, Dictionary<int, IConfigItem>> _tableDict =
        new Dictionary<Type, Dictionary<int, IConfigItem>>();

    public bool LoadConfig<T>(string path)
        where T : IConfigItem
    {
        Type type = typeof(T);
        if (_tableDict.ContainsKey(type))
            return true;
        TextAsset textAsset = ResourcesManager.Instance.Load<TextAsset>(path);
        if (textAsset == null)
        {
            Debug.LogError($"[ConfigManager] 未找到配置文件: {path}");
            return false;
        }
        return ParseConfig<T>(textAsset.text); //解析Json文本并返回最终解析结果
    }

    public void LoadConfigAsync<T>(string path, Action<bool> callback)
        where T : IConfigItem
    {
        Type type = typeof(T);
        if (_tableDict.ContainsKey(type))
        {
            callback?.Invoke(true);
            return;
        }
        ResourcesManager.Instance.LoadAsync<TextAsset>(
            path,
            textAsset =>
            {
                if (textAsset == null)
                {
                    Debug.LogError($"[ConfigManager] 异步加载配置文件失败: {path}");
                    callback?.Invoke(false);
                    return;
                }
                bool success = ParseConfig<T>(textAsset.text);
                callback?.Invoke(success);
            }
        );
    }

    /// <summary>
    /// Json文本反序列化与字典构建方法
    /// </summary>
    private bool ParseConfig<T>(string jsonText)
        where T : IConfigItem
    {
        try
        {
            List<T> list = JsonConvert.DeserializeObject<List<T>>(jsonText);
            if (list == null)
            {
                Debug.LogError($"[ConfigManager] 解析配置表 {typeof(T).Name} 得到空列表");
                return false;
            }
            Dictionary<int, IConfigItem> itemDict = new Dictionary<int, IConfigItem>();
            for (int i = 0; i < list.Count; i++)
            {
                T item = list[i];
                if (itemDict.ContainsKey(item.Id))
                {
                    Debug.LogWarning(
                        $"[ConfigManager] 配置表 {typeof(T).Name} 发现重复ID: {item.Id}，已覆盖"
                    );
                }
                itemDict[item.Id] = item;
            }
            _tableDict[typeof(T)] = itemDict;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ConfigManager] 解析配置表 {typeof(T).Name} 出现异常: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 根据配置项唯一主键ID快速查询当行配置数据
    /// </summary>
    public T GetConfig<T>(int id)
        where T : class, IConfigItem
    {
        Type type = typeof(T);
        if (_tableDict.TryGetValue(type, out Dictionary<int, IConfigItem> itemDict))
        {
            if (itemDict.TryGetValue(id, out IConfigItem item))
            {
                return item as T;
            }
        }
        Debug.LogWarning($"[ConfigManager] 未找到配置表 {typeof(T).Name} 中ID为 {id} 的数据");
        return null;
    }

    /// <summary>
    /// 获取指定配置表的所有配置项字典副本
    /// </summary>
    public Dictionary<int, T> GetAllConfigs<T>()
        where T : class, IConfigItem
    {
        Type type = typeof(T);
        if (_tableDict.TryGetValue(type, out Dictionary<int, IConfigItem> itemDict))
        {
            Dictionary<int, T> result = new Dictionary<int, T>(itemDict.Count);
            foreach (var pair in itemDict)
            {
                result[pair.Key] = pair.Value as T;
            }
            return result;
        }
        Debug.LogWarning($"[ConfigManager] 配置表 {typeof(T).Name} 尚未加载");
        return null;
    }

    /// <summary>
    /// 清除并卸载指定类型配置表的内存缓存
    /// /// </summary>
    public void ClearConfig<T>()
    {
        Type type = typeof(T);
        if (_tableDict.ContainsKey(type))
            _tableDict.Remove(type);
    }

    /// <summary>
    /// 清除所有配置表
    /// </summary>
    public void ClearAll()
    {
        _tableDict.Clear();
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 资源管理器：统一管理资源同步/异步加载与简单缓存
/// </summary>
public class ResourcesManager : MonoSingleton<ResourcesManager>
{
    private readonly Dictionary<string, UnityEngine.Object> _assetCache =
        new Dictionary<string, UnityEngine.Object>();

    protected override void OnInit()
    {
        base.OnInit();
    }

    /// <summary>
    /// 同步加载资源（适合小文件或者初始化预加载）
    /// </summary>
    public T Load<T>(string path)
        where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("[ResourcesManager]路径加载为空");
            return null;
        }

        if (_assetCache.TryGetValue(path, out var cachedAsset))
        {
            return cachedAsset as T;
        }

        T asset = Resources.Load<T>(path);
        if (asset != null)
        {
            _assetCache[path] = asset;
        }
        else
        {
            Debug.LogWarning($"[ResourcesManager]未找到资源：{path}");
        }

        return asset;
    }

    /// <summary>
    /// 异步加载资源（协程/回调模式）
    /// </summary>
    public void LoadAsync<T>(string path, Action<T> callback)
        where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("[ResourceManager] 加载路径不能为空");
            callback?.Invoke(null);
            return;
        }

        if (_assetCache.TryGetValue(path, out var cachedAsset))
        {
            callback?.Invoke(cachedAsset as T);
            return;
        }
        
        //外层 LoadAsync<T> 方法中的 callback 参数类型本身就是 Action<T>。
        //当把 callback 传入 CoLoadAsync 时，编译器可以直接从实参类型 Action<T> 中精确推导出该方法的泛型参数就是外层的 T
        StartCoroutine(CoLoadAsync(path, callback));
    }

    private IEnumerator CoLoadAsync<T>(string path, Action<T> callback)
        where T : UnityEngine.Object
    {
        ResourceRequest request = Resources.LoadAsync<T>(path);
        yield return request;

        if (request.asset != null)
        {
            _assetCache[path] = request.asset;
            callback?.Invoke(request.asset as T);
        }
        else
        {
            Debug.LogWarning($"[ResourceManager] 异步加载未找到资源: {path}");
            callback?.Invoke(null);
        }
    }

    /// <summary>
    /// 实例化预制体对象
    /// </summary>
    public GameObject InstantiatePrefab(string path, Transform parent = null)
    {
        GameObject prefab = Load<GameObject>(path);
        if (prefab == null)
            return null;
        return Instantiate(prefab, parent);
    }

    /// <summary>
    /// 异步实例化对象
    /// </summary>
    public void InstantiatePrefabAsync(
        string path,
        Action<GameObject> callback,
        Transform parent = null
    )
    {
        LoadAsync<GameObject>(
            path,
            prefab =>
            {
                if (prefab == null)
                {
                    callback?.Invoke(null);
                    return;
                }
                GameObject go = Instantiate(prefab, parent);
                callback?.Invoke(go);
            }
        );
    }

    /// <summary>
    /// 释放指定路径的缓存引用
    /// </summary>
    public void Release(string path)
    {
        if (_assetCache.ContainsKey(path))
            _assetCache.Remove(path);
    }

    /// <summary>
    /// 清理全部缓存并触发未引用资源释放
    /// </summary>
    public void ClearAll()
    {
        _assetCache.Clear();
        Resources.UnloadUnusedAssets();
    }
}

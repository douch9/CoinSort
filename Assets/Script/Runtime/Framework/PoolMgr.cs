using System;
using System.Collections;
using System.Collections.Generic;
using System.Resources;
using UnityEngine;

public interface IPoolable
{
    void OnSpawn();
    void OnRecycle();
}

public class PoolMgr : MonoSingleton<PoolMgr>
{
    private readonly Dictionary<string, Queue<GameObject>> _poolDict =
        new Dictionary<string, Queue<GameObject>>();
    private readonly Dictionary<int, string> _instanceToKeyMap = new Dictionary<int, string>();
    private Transform _poolRoot;

    protected override void OnInit()
    {
        base.OnInit();
        GameObject rootGo = new GameObject("PoolRoot");
        rootGo.transform.SetParent(transform);
        _poolRoot = rootGo.transform;
    }

    /// <summary>
    /// 同步获取/创建游戏对象
    /// </summary>
    public GameObject Get(string path, Transform parent = null)
    {
        string poolKey = path;
        GameObject target = null;
        //检查有没有闲置对象
        if (_poolDict.TryGetValue(poolKey, out Queue<GameObject> queue) && queue.Count > 0)
        {
            target = queue.Dequeue();
        }
        else
        {
            GameObject prefab = ResourcesManager.Instance.Load<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[PoolMgr]找不到该预制体：{path}");
                return null;
            }
            target = Instantiate(prefab);
            target.name = prefab.name;
        }
        _instanceToKeyMap[target.GetInstanceID()] = poolKey;
        target.transform.SetParent(parent);
        target.SetActive(true);
        target.GetComponent<IPoolable>()?.OnSpawn();
        return target;
    }

    /// <summary>
    /// 异步获取游戏对象
    /// </summary>
    public void GetAsync(string path, Action<GameObject> callback, Transform parent = null)
    {
        string poolKey = path;
        if (_poolDict.TryGetValue(poolKey, out Queue<GameObject> queue) && queue.Count > 0)
        {
            GameObject target = queue.Dequeue();
            _instanceToKeyMap[target.GetInstanceID()] = poolKey;
            target.transform.SetParent(parent);
            target.SetActive(true);
            target.GetComponent<IPoolable>()?.OnSpawn();
            callback?.Invoke(target);
            return;
        }
        ResourcesManager.Instance.LoadAsync<GameObject>(
            path,
            prefab =>
            {
                if (prefab == null)
                {
                    Debug.LogError($"[PoolMgr] 异步加载失败: {path}");
                    callback?.Invoke(null);
                    return;
                }
                GameObject target = Instantiate(prefab);
                target.name = prefab.name;
                _instanceToKeyMap[target.GetInstanceID()] = poolKey;
                target.transform.SetParent(parent);
                target.SetActive(true);
                target.GetComponent<IPoolable>()?.OnSpawn();
                callback?.Invoke(target);
            }
        );
    }

    public void Push(GameObject target)
    {
        if (target == null)
        {
            return;
        }
        int instanceId = target.GetInstanceID();
        if (!_instanceToKeyMap.TryGetValue(instanceId, out string poolKey))
        {
            Debug.LogWarning($"[PoolMgr] 试图回收非PoolMgr创建的物体: {target.name}，执行销毁");
            Destroy(target);
            return;
        }
        target.SetActive(false);
        target.transform.SetParent(_poolRoot);
        target.GetComponent<IPoolable>()?.OnRecycle();
        if (!_poolDict.TryGetValue(poolKey, out Queue<GameObject> queue))
        {
            queue = new Queue<GameObject>();
            _poolDict[poolKey] = queue;
        }
        queue.Enqueue(target);
    }

    public void ClearPool(string path)
    {
        if (_poolDict.TryGetValue(path, out Queue<GameObject> queue))
        {
            while (queue.Count > 0)
            {
                GameObject go = queue.Dequeue();
                if (go != null)
                {
                    _instanceToKeyMap.Remove(go.GetInstanceID());
                    Destroy(go);
                }
            }
            _poolDict.Remove(path);
        }
    }

    public void ClearAll()
    {
        foreach (var pair in _poolDict)
        {
            Queue<GameObject> queue = pair.Value;
            while (queue.Count > 0)
            {
                GameObject go = queue.Dequeue();
                if (go != null)
                {
                    Destroy(go);
                }
            }
        }
        _poolDict.Clear();
        _instanceToKeyMap.Clear();
    }
}

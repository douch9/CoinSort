using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IPoolObject
{
    void Reset(); // 回池或复用前清空并重置内部属性字段的方法
}

public static class ClassPool<T>
    where T : class, new()
{
    private static readonly Queue<T> _pool = new Queue<T>();
    private static int _maxCapacity = 100; //单个对象池允许驻留缓存的最大容量安全水位

    public static void SetMaxCapacity(int capacity)
    {
        _maxCapacity = capacity;
    }

    public static T Get()
    {
        if (_pool.Count > 0)
        {
            return _pool.Dequeue();
        }
        return new T();
    }

    public static void Release(T item)
    {
        if (item == null)
            return;
        if (item is IPoolObject poolObj) //判定对象是否实现了状态重置接口
            poolObj.Reset();
        if (_pool.Count < _maxCapacity)
            _pool.Enqueue(item);
    }

    public static void Clear()
    {
        _pool.Clear();
    }
}

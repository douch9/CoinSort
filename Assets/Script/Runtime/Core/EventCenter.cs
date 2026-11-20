using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class EventCenter
{
    private static readonly Dictionary<Type, Delegate> _eventDict =
        new Dictionary<Type, Delegate>();

    /// <summary>
    /// 注册指定结构体或类型事件的监听回调接口
    /// </summary>
    public static void AddListener<T>(Action<T> handler)
    {
        if (handler == null)
        {
            return;
        }
        Type type = typeof(T);
        if (_eventDict.TryGetValue(type, out Delegate del))
        {
            //.Combine将已有的委托 del 与新传入的回调 handler 拼接为一个新的多播委托（效果等同于委托的 += 运算符）
            _eventDict[type] = Delegate.Combine(del, handler);
        }
        else
        {
            _eventDict[type] = handler;
        }
    }

    /// <summary>
    /// 注销指定类型事件的监听回调接口
    /// </summary>
    public static void RemoveListener<T>(Action<T> handler)
    {
        if (handler == null)
        {
            return;
        }
        Type type = typeof(T);
        if (_eventDict.TryGetValue(type, out Delegate del))
        {
            Delegate current = Delegate.Remove(del, handler); //从多播委托链中剥离指定的回调方法
            if (current == null)
            {
                _eventDict.Remove(type);
            }
            else
            {
                _eventDict[type] = current; //将缩减后的最新委托链写回字典更新
            }
        }
    }

    /// <summary>
    /// 广播派发事件通知所有订阅该类型的系统执行响应
    /// </summary>
    public static void Broadcast<T>(T eventArgs)
    {
        Type type = typeof(T);
        if (_eventDict.TryGetValue(type, out Delegate del))
        {
            try
            {
                if (del is Action<T> action)
                {
                    action.Invoke(eventArgs);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[EventCenter] 广播事件 {type.Name} 发生异常: {e.Message}\n{e.StackTrace}"
                );
            }
        }
    }

    /// <summary>
    /// 无参数纯信号事件的快捷广播重载方法
    /// </summary>
    public static void Broadcast<T>()
        where T : struct
    {
        Broadcast(default(T));
    }

    public static void ClearAll()
    {
        _eventDict.Clear();
    }
}

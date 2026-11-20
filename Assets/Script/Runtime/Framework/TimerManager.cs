using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单Update驱动全局,统一计时器
/// </summary>
public class TimerManager : MonoSingleton<TimerManager>
{
    private class TimerTask
    {
        public int Id;
        public float Interval; //触发间隔时间（秒）
        public float RemainingTime; //距离下一次触发剩余倒计时时间（秒）
        public int LoopCount;
        public bool UseUnscaledTime; //是否使用不受TimeScale暂停影响的真实时间
        public Action Callback;
    }

    //全局递增的定时器唯一分配序号
    private int _idCounter = 0;

    //当前正在运行的所有定时器任务列表
    private readonly List<TimerTask> _activeTimers = new List<TimerTask>();

    //任务对象复用池避免频繁创建产生GC垃圾
    private readonly Queue<TimerTask> _taskPool = new Queue<TimerTask>();

    //缓冲待加入列表防止在Update遍历中修改集合
    private readonly List<TimerTask> _pendingAdd = new List<TimerTask>();

    //缓冲待取消的定时器ID集合 HashSet不会存在重复的元素
    private readonly HashSet<int> _pendingRemove = new HashSet<int>();

    private void Update()
    {
        if (_pendingAdd.Count > 0)
        {
            _activeTimers.AddRange(_pendingAdd); //将缓冲列表中的新任务所有元素一次性追加到主运行列表
            _pendingAdd.Clear();
        }

        float deltaTime = Time.deltaTime;
        float unscaledDeltaTime = Time.unscaledDeltaTime;

        for (int i = _activeTimers.Count - 1; i >= 0; i--)
        {
            TimerTask task = _activeTimers[i];
            if (_pendingRemove.Contains(task.Id))
            {
                RecycleTask(i); //将被取消的任务从列表移除并回收至对象池
                continue;
            }

            float dt = task.UseUnscaledTime ? unscaledDeltaTime : deltaTime;
            task.RemainingTime -= dt; //扣减本帧流逝的倒计时时间

            if (task.RemainingTime <= 0f)
            { //判定当前定时任务是否已到达触发时效
                try
                {
                    task.Callback?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError(
                        $"[TimerManager] 计时器回调发生异常 ID: {task.Id}, 信息: {e.Message}\n{e.StackTrace}"
                    );
                }
                
                if (task.LoopCount == -1)
                {
                    task.RemainingTime += task.Interval; //重置剩余时间并补齐时间误差
                }
                else if (task.LoopCount > 1)
                {
                    task.LoopCount--;
                    task.RemainingTime += task.Interval;
                }
                else
                {
                    RecycleTask(i); //将已完成的任务移出运行列表并回收入池
                }
            }
        }

        _pendingRemove.Clear();
    }

    /// <summary>
    /// 添加单次执行延迟定时器
    /// </summary>
    public int AddTimer(float delay, Action callback, bool useUnscaledTime = false)
    {
        return AddLoopTimer(delay, callback, 1, useUnscaledTime);
    }

    /// <summary>
    /// 添加循环定时器通用
    /// </summary>
    public int AddLoopTimer(
        float interval,
        Action callback,
        int loopCount = -1,
        bool useUnscaledTime = false
    )
    {
        if (callback == null || interval < 0f)
        {
            return -1;
        }

        TimerTask task = _taskPool.Count > 0 ? _taskPool.Dequeue() : new TimerTask();
        task.Id = ++_idCounter;
        task.Interval = interval;
        task.RemainingTime = interval;
        task.LoopCount = loopCount;
        task.UseUnscaledTime = useUnscaledTime;
        task.Callback = callback;

        _pendingAdd.Add(task);
        return task.Id;
    }

    /// <summary>
    /// 根据定时器ID取消任务执行
    /// </summary>
    public void CancelTimer(int timerId)
    {
        if (timerId <= 0)
        {
            return;
        }
        _pendingRemove.Add(timerId);
    }

    private void RecycleTask(int index)
    {
        TimerTask task = _activeTimers[index];
        _activeTimers.RemoveAt(index);
        task.Callback = null;
        _taskPool.Enqueue(task);
    }

    public void ClearAll()
    {
        for (int i = 0; i < _activeTimers.Count; i++)
        {
            _activeTimers[i].Callback = null;
            _taskPool.Enqueue(_activeTimers[i]);
        }
        _activeTimers.Clear();
        _pendingAdd.Clear();
        _pendingRemove.Clear();
    }
}

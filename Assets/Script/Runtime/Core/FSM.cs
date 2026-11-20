using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IState<T>
{
    void OnInit(T owner); //状态初次加入状态机时触发的初始化方法（传入状态机所属主体）
    void OnEnter();
    void OnUpdate();
    void OnExit();
}

public abstract class StateBase<T> : IState<T>
{
    protected T Owner;

    public virtual void OnInit(T owner)
    {
        Owner = owner;
    }

    public virtual void OnEnter() { }

    public virtual void OnUpdate() { }

    public virtual void OnExit() { }
}

public class FSM<T>
{
    private readonly T _owner;

    //缓存所有已注册状态实例的字典
    private readonly Dictionary<Type, IState<T>> _stateDict = new Dictionary<Type, IState<T>>();
    private IState<T> _currentState;

    public IState<T> CurrentState => _currentState;

    public FSM(T owner)
    {
        _owner = owner;
    }

    /// <summary>
    /// // 注册添加新状态实例至状态机接口
    /// </summary>
    public void AddState<TState>()
        where TState : IState<T>, new()
    {
        Type stateType = typeof(TState);
        if (!_stateDict.ContainsKey(stateType))
        {
            TState state = new TState();
            state.OnInit(_owner);
            _stateDict[stateType] = state;
        }
    }

    public void ChangeState<TState>()
        where TState : IState<T>
    {
        Type stateType = typeof(TState);
        if (!_stateDict.TryGetValue(stateType, out IState<T> targetState))
        {
            AppLog.LogError("FSM", $"状态机尚未注册状态: {stateType.Name}，无法完成切换！");
            return;
        }
        if (_currentState == targetState)
        {
            return;
        }
        _currentState?.OnExit();
        _currentState = targetState;
        _currentState.OnEnter();
    }

    public void Update()
    {
        _currentState?.OnUpdate();
    }

    public void Clear()
    {
        _currentState?.OnExit();
        _currentState = null;
        _stateDict.Clear();
    }
}

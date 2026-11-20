using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 框架启动总入口，用于全局硬件与环境配置、有序预热各大管理器、驱动游戏业务首流程
/// 需要挂载在初始场景的空GameObject上，命名为GameEntry
/// </summary>
public class GameEntry : MonoSingleton<GameEntry>
{
    [Header("基础环境配置")]
    [SerializeField]
    private int _targetFrameRate = 60;

    [SerializeField]
    private bool _neverSleep = true;

    private FSM<GameEntry> _flowFsm;
    public FSM<GameEntry> FlowFsm => _flowFsm;

    protected override void Awake()
    {
        base.Awake();
        InitAppEnvironment();
    }

    private void Start()
    {
        InitFlowFsm();
    }

    private void Update()
    {
        _flowFsm?.Update();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _flowFsm?.Clear();
    }

    /// <summary>
    /// 初始化全局帧率、屏幕常亮与后台运行策略
    /// </summary>
    protected void InitAppEnvironment()
    {
        Application.targetFrameRate = _targetFrameRate;
        if (_neverSleep)
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }

    private void InitFlowFsm()
    {
        _flowFsm = new FSM<GameEntry>(this);
        _flowFsm.AddState<ProcedureLaunch>();
        _flowFsm.AddState<ProcedureMain>();
        _flowFsm.ChangeState<ProcedureLaunch>();
    }
}

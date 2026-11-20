using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//继承StateBase以GameEntry为宿主定义启动引导流程
//状态初始化（OnInit）时，状态机会把当前 GameEntry 实例传递进来，这样在 ProcedureLaunch 内部就能直接通过 Owner 获取全局入口的上下文或调用相关接口
public class ProcedureLaunch : StateBase<GameEntry>
{
    public override void OnEnter()
    {
        base.OnEnter();
        AppLog.Log("Procedure", "=== 进入游戏启动引导流程 ===");
        InitManagers();
        Owner.FlowFsm.ChangeState<ProcedureMain>();
    }

    private void InitManagers()
    {
        //_ = ... 是 C# 的弃元语法，用于接收右侧表达式的返回值并立即丢弃
        _ = StorageManager.Instance;
        _ = ResourcesManager.Instance;
        _ = PoolMgr.Instance;
        _ = ConfigManager.Instance;
        _ = AudioManager.Instance;
        _ = TimerManager.Instance;
        _ = UIManager.Instance;
        _ = SceneManager.Instance;
        _ = NetworkManager.Instance;
        _ = PlatformManager.Instance;
        _ = ToastManager.Instance;
        _ = DialogManager.Instance;
        _ = RedDotManager.Instance;
        AppLog.Log("Procedure", "全系统核心服务预热完毕！");
    }
}

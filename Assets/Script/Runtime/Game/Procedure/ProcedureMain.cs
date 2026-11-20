using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProcedureMain : StateBase<GameEntry>
{
    public override void OnEnter()
    {
        base.OnEnter();
        AppLog.Log("Procedure", "=== 进入主城大厅流程 ===");
        OpenMainView();
    }

    private void OpenMainView()
    {
        UIManager.Instance.OpenUIAsync<MainView>(
            "UI/MainView",
            null,
            view =>
            {
                if (view != null)
                {
                    ToastManager.Instance.Show("欢迎来到游戏！");
                }
            }
        );
    }

    public override void OnExit()
    {
        base.OnExit();
        UIManager.Instance.CloseUI<MainView>();
    }
}

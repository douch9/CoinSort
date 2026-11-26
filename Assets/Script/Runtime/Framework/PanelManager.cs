using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用提示/确认弹窗管理器，提供便捷的全局确认框与警示框呼出接口
/// </summary>
public class PanelManager : Singleton<PanelManager>
{
    private const string PANEL_PATH = "UI/CommonPanel";

    public void ShowConfirm(
        string title,
        string content,
        Action onConfirm,
        Action onCancel = null,
        string confirmText = "确定",
        string cancelText = "取消"
    )
    {
        CommonPanelData data = new CommonPanelData
        {
            Title = title,
            Content = content,
            ConfirmText = confirmText,
            CancelText = cancelText,
            OnConfirm = onConfirm,
            OnCancel = onCancel,
            IsAlert = false,
        };
        UIManager.Instance.OpenUIAsync<CommonPanel>(PANEL_PATH, data);
    }

    public void ShowAlert(
        string title,
        string content,
        Action onConfirm = null,
        string confirmText = "我知道了"
    )
    {
        CommonPanelData data = new CommonPanelData
        {
            Title = title,
            Content = content,
            OnConfirm = onConfirm,
            ConfirmText = confirmText,
            IsAlert = true,
        };
        UIManager.Instance.OpenUIAsync<CommonPanel>(PANEL_PATH, data);
    }

    public void Close()
    {
        UIManager.Instance.CloseUI<CommonPanel>();
    }
}

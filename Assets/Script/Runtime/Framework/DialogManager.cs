using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DialogManager : Singleton<DialogManager>
{
    private const string DIALOG_PATH = "UI/CommonDialogView";

    public void ShowConfirm(
        string title,
        string content,
        Action onConfirm,
        Action onCancel = null,
        string confirmText = "确定",
        string cancelText = "取消"
    )
    {
        CommonDialogData data = new CommonDialogData
        {
            Title = title,
            Content = content,
            ConfirmText = confirmText,
            CancelText = cancelText,
            OnConfirm = onConfirm,
            OnCancel = onCancel,
            IsAlert = false,
        };
        UIManager.Instance.OpenUIAsync<CommonDialogView>(DIALOG_PATH, data);
    }

    public void ShowAlert(
        string title,
        string content,
        Action onConfirm = null,
        string confirmText = "我知道了"
    )
    {
        CommonDialogData data = new CommonDialogData
        {
            Title = title,
            Content = content,
            OnConfirm = onConfirm,
            ConfirmText = confirmText,
            IsAlert = true,
        };
        UIManager.Instance.OpenUIAsync<CommonDialogView>(DIALOG_PATH, data);
    }

    public void Close()
    {
        UIManager.Instance.CloseUI<CommonDialogView>();
    }
}

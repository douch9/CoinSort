using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CommonDialogData
{
    public string Title;
    public string Content;
    public string ConfirmText = "确定";
    public string CancelText = "取消";
    public Action OnConfirm;
    public Action OnCancel;
    public bool IsAlert = false;
}

public class CommonDialogView : UIBase
{
    [Header("文本显示组件")]
    [SerializeField]
    private Text _titleText;

    [SerializeField]
    private Text _contentText;

    [Header("交互按钮组件")]
    [SerializeField]
    private Button _confirmBtn;

    [SerializeField]
    private Text _confirmText;

    [SerializeField]
    private Button _cancelBtn;

    [SerializeField]
    private Text _cancelText;

    [SerializeField]
    private Button _maskBtn;

    private CommonDialogData _data;

    public override void OnInit()
    {
        base.OnInit();
        _confirmBtn?.onClick.AddListener(OnConfirmClicked);
        _cancelBtn?.onClick.AddListener(OnCancelClicked);
        _maskBtn?.onClick.AddListener(OnMaskClicked);
    }

    public override void OnOpen(object args = null)
    {
        base.OnOpen(args);
        _data = args as CommonDialogData;
        RefreshView();
    }

    private void RefreshView()
    {
        if (_data == null)
            return;
        if (_titleText != null)
            _titleText.text = _data.Title;
        if (_contentText != null)
            _contentText.text = _data.Content;
        if (_confirmText != null)
            _confirmText.text = _data.ConfirmText;
        if (_data.IsAlert) //判断是否为单按钮警告模式
        {
            if (_cancelBtn != null)
                _cancelBtn.gameObject.SetActive(false);
        }
        else //双按钮分支
        {
            if (_cancelBtn != null)
                _cancelBtn.gameObject.SetActive(true);
            if (_cancelText != null)
                _cancelText.text = string.IsNullOrEmpty(_data.CancelText)
                    ? "取消"
                    : _data.CancelText;
        }
    }

    private void OnConfirmClicked()
    {
        Action callback = _data?.OnConfirm;
        UIManager.Instance.CloseUI<CommonDialogView>(() =>
        {
            callback?.Invoke();
        });
    }

    private void OnCancelClicked()
    {
        Action callback = _data?.OnCancel;
        UIManager.Instance.CloseUI<CommonDialogView>(() =>
        {
            callback?.Invoke();
        });
    }

    private void OnMaskClicked()
    {
        if (_data != null && _data.IsAlert)
            return; //禁止点击遮罩关闭以防用户漏看重要警告
        OnCancelClicked(); //普通模式下点击遮罩等同于点击取消关闭弹窗
    }
}

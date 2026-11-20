using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainView : UIBase
{
    [Header("UI 文本组件")]
    [SerializeField]
    private Text _coinText;

    [Header("UI 交互按钮")]
    [SerializeField]
    private Button _watchAdBtn;

    [SerializeField]
    private Button _costCoinBtn;

    [SerializeField]
    private Button _soundBtn;

    [SerializeField]
    private Button _closeBtn;

    private MainPresenter _presenter; //持有业务控制中介Presenter的引用

    public override void OnInit()
    {
        base.OnInit();
        _presenter = new MainPresenter(this);
        //绑定按钮的用户点击事件
        InitButtonEvents();
    }

    public override void OnOpen(object args = null)
    {
        base.OnOpen(args);
        _presenter.OnViewOpen(); //通知Presenter界面已打开准备就绪以拉取初始数据
    }

    public override void OnClose(Action onClosed = null)
    {
        base.OnClose(onClosed);
        _presenter.OnViewClose();
    }

    private void InitButtonEvents()
    {
        _watchAdBtn?.onClick.AddListener(() => _presenter.OnClickWatchAd());
        _costCoinBtn?.onClick.AddListener(() => _presenter.OnClickCostCoin());
        _soundBtn?.onClick.AddListener(() => _presenter.OnClickPlaySound());
        _closeBtn?.onClick.AddListener(() => CloseSelf());
    }

    public void UpdateCoinText(int coin)
    {
        if (_coinText != null)
        {
            _coinText.text = $"金币:{coin}";
        }
    }
}
